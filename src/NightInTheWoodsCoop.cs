using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace NightInTheWoodsCoop
{
    [BepInPlugin("coop-anything.nitw", "Night in the Woods Co-op", "0.6.8")]
    public sealed class CoopPlugin : BaseUnityPlugin
    {
        internal static CoopPlugin Instance;
        internal static readonly InputState HostInput = new InputState();
        internal static readonly InputState GuestInput = new InputState();

        private ConfigEntry<string> _role;
        private ConfigEntry<string> _hostAddress;
        private ConfigEntry<int> _port;
        private Harmony _harmony;
        private Socket _socket;
        private IPEndPoint _host;
        private IPEndPoint _peer;
        private float _nextSend;
        private float _lastGuestPacket;
        private uint _sequence;
        private uint _lastSequence;
        private bool _haveSequence;
        private bool _connected;
        private string _saveRoot;
        private string _status = "starting";
        private string _testStatusFile;
        private float _nextStatusWrite;
        private string _botCommandFile;
        private string _botStatus = "idle";
        private InputState _lastLocalInput = new InputState();
        private int _botConfirmFrames;
        private int _botMoveFrames;
        private float _botHorizontal;
        private float _botVertical;
        private int _testConfirmObserved;
        private int _testBotConfirmConsumed;
        private int _testGuestConfirmPackets;
        private int _testHostConfirmPackets;
        private int _testUdpSent;
        private int _testUdpReceived;
        private bool _guestConfirmHeld;
        private bool _hostConfirmHeld;
        private bool _pendingGuestConfirm;
        private bool _pendingHostConfirm;
        private bool _pendingBotConfirm;
        private bool _waitingChoiceConfirm;
        private uint _waitingChoiceCommitId;
        private uint _waitingChoiceSessionId;
        private string _waitingChoiceNode = "";
        private float _waitingChoiceDeadline;
        private float _broadcastConfirmUntil;
        private string _hostSceneName = "";
        private string _requestedHostScene = "";
        private float _nextSceneRetryAt;
        private bool _hostOptionsActive;
        private int _hostOptionIndex;
        private string _hostOptionNode = "";
        private uint _hostOptionSessionId;
        private bool _hostOptionsWasActive;
        private uint _clientActiveOptionSessionId;
        private uint _hostChoiceCommitId;
        private uint _hostChoiceCommitSessionId;
        private int _hostChoiceCommitIndex;
        private string _hostChoiceCommitNode = "";
        private uint _lastAppliedChoiceCommitId;
        private bool _hasHostPosition;
        private Vector3 _hostPosition;
        private int _storyVariableCount;
        private string _storyVariableSync = "waiting";
        private string _storyVariableHash = "00000000";
        private string _storyVariableProbe = "none";
        private string _lastStoryVariableHash = "";
        private uint _storySnapshotId;
        private float _nextStorySnapshot;
        private uint _pendingStorySnapshotId;
        private ushort _pendingStoryFragmentCount;
        private List<bool> _receivedStoryFragments;
        private Dictionary<string, float> _pendingStoryVariables;

        internal bool IsHost { get { return !string.Equals(EnvironmentValue("NITW_COOP_ROLE", _role.Value), "Client", StringComparison.OrdinalIgnoreCase); } }
        internal bool IsClient { get { return !IsHost; } }
        internal bool Connected { get { return _connected; } }
        internal string RoleName { get { return IsHost ? "Host" : "Client"; } }

        private void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
            _role = Config.Bind("Network", "Role", "Host", "Host or Client. Use one host and one client.");
            _hostAddress = Config.Bind("Network", "HostAddress", "127.0.0.1", "IPv4 address of the host; clients only.");
            _port = Config.Bind("Network", "Port", 39751, "UDP port used for direct co-op.");
            string saveRootOverride = Environment.GetEnvironmentVariable("NITW_COOP_SAVE_ROOT");
            _saveRoot = string.IsNullOrEmpty(saveRootOverride)
                ? Path.Combine(Path.Combine(Paths.ConfigPath, "NightInTheWoodsCoop"), "Saves")
                : Path.GetFullPath(saveRootOverride);
            Directory.CreateDirectory(_saveRoot);
            _testStatusFile = Environment.GetEnvironmentVariable("NITW_COOP_TEST_STATUS");
            if (string.IsNullOrEmpty(_testStatusFile))
                _testStatusFile = Path.Combine(Paths.ConfigPath, "CoopStatus-" + RoleName + ".txt");
            _botCommandFile = Path.Combine(Paths.ConfigPath, "CoopBot-" + RoleName + ".txt");

            _harmony = new Harmony("coop-anything.nitw");
            Patch("Continuity", "GetSaveDataPath", "SavePathPrefix", true);
            if (IsClient)
                Patch("Continuity", "Save", "GuestSavePrefix", true);
            Patch("Player", "GetAxisRaw", "AxisPostfix", false);
            Patch("Player", "GetJumpInputDown", "JumpDownPostfix", false);
            Patch("Player", "GetJumpInputHeld", "JumpHeldPostfix", false);
            Patch("Player", "SetInteractInput", "InteractPostfix", false);
            Patch("Player", "Update", "PlayerStatePostfix", false);
            Patch("Player", "LateUpdate", "PlayerStatePostfix", false);
            Patch("Global", "IsConfirmOrInteractDown", "ConfirmInputPostfix", false);
            PatchDialogueOptionIterator();
            PatchDialogueOptionCommit();

            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.Blocking = false;
                if (IsHost)
                {
                    _socket.Bind(new IPEndPoint(IPAddress.Any, PortValue()));
                    _status = "waiting on UDP " + PortValue();
                }
                else
                {
                    _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                    _host = new IPEndPoint(IPAddress.Parse(EnvironmentValue("NITW_COOP_HOST", _hostAddress.Value)), PortValue());
                    _status = "connecting to " + _host;
                    Logger.LogInfo("Bound client UDP socket to " + _socket.LocalEndPoint);
                }
            }
            catch (Exception e)
            {
                _status = "network error: " + e.Message;
                Logger.LogError(_status);
            }

            Logger.LogInfo("Co-op plugin loaded as " + RoleName + "; isolated save folder: " + _saveRoot);
        }

        private void Patch(string typeName, string methodName, string patchMethod, bool prefix)
        {
            Type targetType = typeof(global::Continuity).Assembly.GetType(typeName, false);
            MethodInfo target = targetType == null ? null : AccessTools.Method(targetType, methodName, null, null);
            MethodInfo patch = AccessTools.Method(typeof(CoopPlugin), patchMethod, null, null);
            if (target == null || patch == null)
            {
                Logger.LogWarning("Patch not installed: " + typeName + "." + methodName);
                return;
            }
            _harmony.Patch(target, prefix ? new HarmonyMethod(patch) : null, prefix ? null : new HarmonyMethod(patch), null, null, null);
            Logger.LogInfo("Patched " + typeName + "." + methodName);
        }

        private void Update()
        {
            InputState local = ReadLocalInput();
            _lastLocalInput.CopyFrom(local);
            if (IsHost)
            {
                PollHost();
                HostInput.CopyFrom(local);
                if (_connected && Time.realtimeSinceStartup - _lastGuestPacket > 0.5f)
                {
                    _connected = false;
                    _status = "peer timed out; hosting locally";
                    GuestInput.Clear();
                }
                if (Time.realtimeSinceStartup >= _nextSend && _peer != null)
                {
                    InputState shared = Merge(local, GuestInput);
                    if (Time.realtimeSinceStartup < _broadcastConfirmUntil) shared.ConfirmDown = true;
                    Vector3 playerPosition;
                    bool hasPlayer = TryGetPlayerPosition(out playerPosition);
                    Dictionary<string, float> variables = ReadStoryVariables();
                    if (variables != null)
                    {
                        _storyVariableCount = variables.Count;
                        _storyVariableHash = StoryVariableHash(variables);
                        if (_peer != null && (_storyVariableHash != _lastStoryVariableHash ||
                            Time.realtimeSinceStartup >= _nextStorySnapshot))
                        {
                            SendStoryVariableSnapshot(variables);
                            _lastStoryVariableHash = _storyVariableHash;
                            _nextStorySnapshot = Time.realtimeSinceStartup + 1f;
                        }
                    }
                    else _storyVariableSync = "Continuity.vars unavailable";
                    bool optionsActive;
                    int optionIndex;
                    string optionNode;
                    ReadDialogueOptionState(out optionsActive, out optionIndex, out optionNode);
                    ObserveHostOptionActive(optionsActive);
                    byte[] snapshot = Packet.Create(2, ++_sequence, shared,
                        Application.loadedLevelName, hasPlayer, playerPosition, null, optionsActive, optionIndex, optionNode,
                        _hostOptionSessionId, _hostChoiceCommitId, _hostChoiceCommitSessionId,
                        _hostChoiceCommitIndex, _hostChoiceCommitNode);
                    Send(_peer, snapshot);
                    _nextSend = Time.realtimeSinceStartup + 0.033f;
                }
            }
            else
            {
                PollClient();
                if (Time.realtimeSinceStartup >= _nextSend)
                {
                    Send(_host, Packet.Create(1, ++_sequence, local, "", false, Vector3.zero, null, false, 0, "", 0, 0, 0, 0, ""));
                    _nextSend = Time.realtimeSinceStartup + 0.033f;
                }
            }
            WriteTestStatus();
        }

        private void WriteTestStatus()
        {
            if (string.IsNullOrEmpty(_testStatusFile) || Time.realtimeSinceStartup < _nextStatusWrite) return;
            _nextStatusWrite = Time.realtimeSinceStartup + 0.25f;
            try
            {
                Dictionary<string, float> currentStoryVariables = ReadStoryVariables();
                if (currentStoryVariables != null)
                {
                    _storyVariableCount = currentStoryVariables.Count;
                    _storyVariableHash = StoryVariableHash(currentStoryVariables);
                }
                string scene = Application.loadedLevelName;
                InputState sharedInput = IsHost ? Merge(HostInput, GuestInput) : HostInput;
                string line = "role=" + RoleName + "\nconnected=" + _connected + "\nstatus=" + _status +
                    "\nscene=" + scene + "\nframe=" + Time.frameCount + "\nbot=" + _botStatus +
                    "\nconfirmObserved=" + _testConfirmObserved + "\nbotConfirmConsumed=" + _testBotConfirmConsumed +
                    "\nguestConfirmPackets=" + _testGuestConfirmPackets +
                    "\nhostConfirmPackets=" + _testHostConfirmPackets +
                    "\nudpPacketsSent=" + _testUdpSent + "\nudpPacketsReceived=" + _testUdpReceived + "\nlocalAxis=" +
                    _lastLocalInput.Horizontal.ToString("F2", CultureInfo.InvariantCulture) + "," +
                    _lastLocalInput.Vertical.ToString("F2", CultureInfo.InvariantCulture) +
                    "\nsharedAxis=" + sharedInput.Horizontal.ToString("F2", CultureInfo.InvariantCulture) + "," +
                    sharedInput.Vertical.ToString("F2", CultureInfo.InvariantCulture) + "\n";
                line += "storyVariables=" + _storyVariableCount + "\nstoryVariableHash=" + _storyVariableHash +
                    "\nstoryVariableProbe=" + _storyVariableProbe + "\nstoryVariableSync=" + _storyVariableSync + "\n";
                if (currentStoryVariables != null && currentStoryVariables.Count > 0)
                {
                    List<string> variableKeys = new List<string>(currentStoryVariables.Keys);
                    variableKeys.Sort(StringComparer.Ordinal);
                    string firstKey = variableKeys[0];
                    line += "firstStoryVariable=" + firstKey + ":" +
                        currentStoryVariables[firstKey].ToString("R", CultureInfo.InvariantCulture) + "\n";
                }
                bool optionsActive;
                int optionIndex;
                string optionNode;
                ReadDialogueOptionState(out optionsActive, out optionIndex, out optionNode);
                line += "optionsActive=" + optionsActive + "\noptionIndex=" + optionIndex +
                    "\noptionNode=" + optionNode + "\noptionSession=" + _hostOptionSessionId +
                    "\nchoiceCommitId=" + _hostChoiceCommitId + "\nchoiceCommitSession=" +
                    _hostChoiceCommitSessionId + "\nchoiceCommitIndex=" + _hostChoiceCommitIndex +
                    "\nchoiceCommitNode=" + _hostChoiceCommitNode + "\nlastAppliedChoiceCommitId=" +
                    _lastAppliedChoiceCommitId + "\nclientActiveOptionSession=" +
                    _clientActiveOptionSessionId + "\nwaitingChoiceConfirm=" + _waitingChoiceConfirm +
                    "\nwaitingChoiceCommitId=" + _waitingChoiceCommitId + "\nwaitingChoiceSession=" +
                    _waitingChoiceSessionId + "\n";
                global::Player player = UnityEngine.Object.FindObjectOfType(typeof(global::Player)) as global::Player;
                if (player != null)
                {
                    Vector3 position = player.transform.position;
                    line += "player=" + position.x.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        position.y.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        position.z.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                        "playerControl=" + player.canMove + "," + player.movementEnabled + "," + player.moveState + "\n";
                }
                else line += "player=none\nplayerControl=none\n";
                List<string> activeTypes = new List<string>();
                UnityEngine.Object[] behaviours = UnityEngine.Object.FindObjectsOfType(typeof(MonoBehaviour));
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i] as MonoBehaviour;
                    if (behaviour == null || behaviour.GetType().Assembly != typeof(global::Continuity).Assembly) continue;
                    string typeName = behaviour.GetType().Name;
                    if (!activeTypes.Contains(typeName)) activeTypes.Add(typeName);
                }
                activeTypes.Sort(StringComparer.Ordinal);
                line += "scripts=" + string.Join(",", activeTypes.ToArray()) + "\n";
                File.WriteAllText(_testStatusFile, line);
            }
            catch (Exception e)
            {
                if (Time.frameCount % 300 == 0) Logger.LogWarning("Test status write failed: " + e.Message);
            }
        }

        private void LateUpdate()
        {
            if (IsHost) GuestInput.ClearEdges();
            else HostInput.ClearEdges();
            if (IsClient && _connected) ReconcileHostState();
        }

        private static bool TryGetPlayerPosition(out Vector3 position)
        {
            global::Player player = UnityEngine.Object.FindObjectOfType(typeof(global::Player)) as global::Player;
            if (player == null)
            {
                position = Vector3.zero;
                return false;
            }
            position = player.transform.position;
            return true;
        }

        private void ReconcileHostState()
        {
            if (string.IsNullOrEmpty(_hostSceneName)) return;
            string localScene = Application.loadedLevelName;
            if (!string.Equals(localScene, _hostSceneName, StringComparison.Ordinal))
            {
                if (!string.Equals(_requestedHostScene, _hostSceneName, StringComparison.Ordinal))
                {
                    _requestedHostScene = _hostSceneName;
                    _nextSceneRetryAt = 0f;
                }
                if (Time.realtimeSinceStartup >= _nextSceneRetryAt)
                {
                    _nextSceneRetryAt = Time.realtimeSinceStartup + 1.5f;
                    if (global::Global.dialogue != null)
                    {
                        global::Global.dialogue.Stop();
                        global::Global.dialogue.Clear();
                    }
                    global::Global.LoadLevel(_hostSceneName);
                }
                return;
            }

            _requestedHostScene = "";
            _nextSceneRetryAt = 0f;
        }

        private void PatchDialogueOptionIterator()
        {
            Type implementationType = typeof(global::Continuity).Assembly.GetType("DialogueImplementation", false);
            Type iteratorType = implementationType == null ? null : implementationType.GetNestedType(
                "<RunOptions>c__Iterator5", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo target = iteratorType == null ? null : AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "DialogueOptionStepPrefix");
            if (target == null || prefix == null)
            {
                Logger.LogWarning("Dialogue option cursor sync unavailable: RunOptions iterator not found");
                return;
            }
            _harmony.Patch(target, new HarmonyMethod(prefix), null, null, null, null);
            Logger.LogInfo("Patched dialogue option iterator for host-authoritative choice cursor");
        }

        private void PatchDialogueOptionCommit()
        {
            Type dialogueUiType = typeof(global::Continuity).Assembly.GetType("Yarn.Unity.Example.DialogueUI", false);
            MethodInfo target = dialogueUiType == null ? null : AccessTools.Method(dialogueUiType, "SetOption", new Type[] { typeof(int) });
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "DialogueOptionCommitPrefix");
            if (target == null || prefix == null)
            {
                Logger.LogWarning("Dialogue choice commit sync unavailable: DialogueUI.SetOption not found");
                return;
            }
            _harmony.Patch(target, new HarmonyMethod(prefix), null, null, null, null);
            Logger.LogInfo("Patched DialogueUI.SetOption for host-authoritative Yarn choice commits");
        }

        private static void ReadDialogueOptionState(out bool active, out int index, out string node)
        {
            active = false;
            index = 0;
            node = "";
            try
            {
                object dialogue = global::Global.dialogue;
                PropertyInfo implementationProperty = dialogue == null ? null : AccessTools.Property(dialogue.GetType(), "implementation");
                object implementation = implementationProperty == null ? null : implementationProperty.GetValue(dialogue, null);
                node = ReadDialogueNode(dialogue);
                if (implementation == null) return;
                FieldInfo activeField = AccessTools.Field(implementation.GetType(), "runningOptions");
                FieldInfo indexField = AccessTools.Field(implementation.GetType(), "currentOption");
                if (activeField == null || indexField == null) return;
                active = (bool)activeField.GetValue(implementation);
                index = (int)indexField.GetValue(implementation);
            }
            catch { }
        }

        private static void DialogueOptionStepPrefix(object __instance)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.Connected) return;
            try
            {
                FieldInfo ownerField = AccessTools.Field(__instance.GetType(), "$this");
                object implementation = ownerField == null ? null : ownerField.GetValue(__instance);
                if (implementation == null) return;
                FieldInfo activeField = AccessTools.Field(implementation.GetType(), "runningOptions");
                if (activeField == null) return;
                bool active = (bool)activeField.GetValue(implementation);
                if (plugin.IsHost)
                {
                    plugin.ObserveHostOptionActive(active);
                    return;
                }
                if (!plugin.IsClient || !plugin._hostOptionsActive) return;
                object dialogue = global::Global.dialogue;
                string localNode = ReadDialogueNode(dialogue);
                if (!SameChoiceSession(plugin._hostOptionSessionId, plugin._hostOptionSessionId,
                    plugin._hostOptionNode, localNode)) return;
                FieldInfo indexField = AccessTools.Field(implementation.GetType(), "currentOption");
                FieldInfo nextField = AccessTools.Field(implementation.GetType(), "inputArrowNext");
                FieldInfo previousField = AccessTools.Field(implementation.GetType(), "inputArrowPrev");
                if (indexField == null || !active) return;
                plugin._clientActiveOptionSessionId = plugin._hostOptionSessionId;
                int index = Math.Max(0, plugin._hostOptionIndex);
                FieldInfo optionsField = AccessTools.Field(__instance.GetType(), "options");
                System.Collections.IList options = optionsField == null ? null : optionsField.GetValue(__instance) as System.Collections.IList;
                if (options != null && options.Count > 0) index = Math.Min(index, options.Count - 1);
                indexField.SetValue(implementation, index);
                if (nextField != null) nextField.SetValue(implementation, false);
                if (previousField != null) previousField.SetValue(implementation, false);
            }
            catch { }
        }

        private static void DialogueOptionCommitPrefix(ref int __0)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.Connected) return;
            string node = "";
            try
            {
                object dialogue = global::Global.dialogue;
                node = ReadDialogueNode(dialogue);
            }
            catch { return; }

            if (plugin.IsHost)
            {
                bool optionsActive;
                int currentOption;
                string currentNode;
                ReadDialogueOptionState(out optionsActive, out currentOption, out currentNode);
                plugin.ObserveHostOptionActive(optionsActive);
                plugin._hostChoiceCommitIndex = Math.Max(0, __0);
                plugin._hostChoiceCommitNode = node;
                plugin._hostChoiceCommitSessionId = plugin._hostOptionSessionId;
                unchecked { plugin._hostChoiceCommitId++; }
                if (plugin._hostChoiceCommitId == 0) plugin._hostChoiceCommitId = 1;
                return;
            }

            if (plugin._clientActiveOptionSessionId == 0)
            {
                if (!SameChoiceSession(plugin._hostOptionSessionId,
                    plugin._hostChoiceCommitSessionId, node, plugin._hostChoiceCommitNode)) return;
                plugin._clientActiveOptionSessionId = plugin._hostChoiceCommitSessionId;
            }

            if (plugin._hostChoiceCommitId == 0 || plugin._hostChoiceCommitId == plugin._lastAppliedChoiceCommitId ||
                !SameChoiceSession(plugin._clientActiveOptionSessionId,
                    plugin._hostChoiceCommitSessionId, node, plugin._hostChoiceCommitNode)) return;
            __0 = plugin._hostChoiceCommitIndex;
            plugin._lastAppliedChoiceCommitId = plugin._hostChoiceCommitId;
            plugin._clientActiveOptionSessionId = 0;
            plugin.Logger.LogInfo("Applied host dialogue choice commit " + plugin._lastAppliedChoiceCommitId +
                " at index " + __0 + " for option session " + plugin._hostChoiceCommitSessionId);
        }

        private void ObserveHostOptionActive(bool active)
        {
            if (active && !_hostOptionsWasActive)
            {
                unchecked { _hostOptionSessionId++; }
                if (_hostOptionSessionId == 0) _hostOptionSessionId = 1;
            }
            _hostOptionsWasActive = active;
        }

        private static string ReadDialogueNode(object dialogue)
        {
            if (dialogue == null) return "";
            try
            {
                Type dialogueType = dialogue.GetType();
                PropertyInfo runningNode = AccessTools.Property(dialogueType, "runningNode");
                string value = runningNode == null ? "" : runningNode.GetValue(dialogue, null) as string ?? "";
                if (!string.IsNullOrEmpty(value)) return value;

                MethodInfo getCurrentNodeName = AccessTools.Method(dialogueType, "GetCurrentNodeName");
                if (getCurrentNodeName != null)
                {
                    value = getCurrentNodeName.Invoke(dialogue, null) as string ?? "";
                    if (!string.IsNullOrEmpty(value)) return value;
                }

                FieldInfo runnerField = AccessTools.Field(dialogueType, "dialogueRunner");
                object runner = runnerField == null ? null : runnerField.GetValue(dialogue);
                PropertyInfo currentNodeName = runner == null ? null : AccessTools.Property(runner.GetType(), "currentNodeName");
                return currentNodeName == null ? "" : currentNodeName.GetValue(runner, null) as string ?? "";
            }
            catch { return ""; }
        }

        internal static bool SameChoiceSession(uint optionSessionId, uint commitSessionId,
            string optionNode, string commitNode)
        {
            if (optionSessionId == 0 || commitSessionId != optionSessionId) return false;
            bool optionNodeEmpty = string.IsNullOrEmpty(optionNode);
            bool commitNodeEmpty = string.IsNullOrEmpty(commitNode);
            if (optionNodeEmpty || commitNodeEmpty) return optionNodeEmpty && commitNodeEmpty;
            return string.Equals(optionNode, commitNode, StringComparison.Ordinal);
        }

        private static void PlayerStatePostfix(global::Player __instance)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected || !plugin._hasHostPosition ||
                !string.Equals(Application.loadedLevelName, plugin._hostSceneName, StringComparison.Ordinal)) return;
            if ((__instance.transform.position - plugin._hostPosition).sqrMagnitude > 0.001f)
                __instance.transform.position = plugin._hostPosition;
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12, 12, 620, 28), "Night in the Woods Co-op | " + RoleName + " | " + _status);
            if (IsHost && _peer == null) GUI.Label(new Rect(12, 32, 620, 24), "Share this PC's LAN/VPN IPv4 address; UDP " + PortValue());
        }

        private InputState ReadLocalInput()
        {
            InputState state = new InputState();
            try
            {
                Rewired.Player player = ReInput.players.GetPlayer(0);
                if (player != null)
                {
                    state.Horizontal = player.GetAxisRaw("Horizontal");
                    state.Vertical = player.GetAxisRaw("Vertical");
                    state.JumpDown = player.GetButtonDown("Jump");
                    state.JumpHeld = player.GetButton("Jump");
                    state.InteractDown = player.GetButtonDown("Interact");
                    state.InteractHeld = player.GetButton("Interact");
                    state.ConfirmDown = player.GetButtonDown("Interact");
                }
            }
            catch (Exception e)
            {
                if (Time.frameCount % 300 == 0) Logger.LogWarning("Could not sample Rewired input: " + e.Message);
            }
            ApplyBotInput(state);
            return state;
        }

        private void ApplyBotInput(InputState state)
        {
            try
            {
                if (File.Exists(_botCommandFile))
                {
                    string command = File.ReadAllText(_botCommandFile).Trim();
                    File.Delete(_botCommandFile);
                    if (string.Equals(command, "confirm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command, "interact", StringComparison.OrdinalIgnoreCase))
                    {
                        _botConfirmFrames = 4;
                        _pendingBotConfirm = IsHost;
                        _botStatus = "confirm queued";
                    }
                    else if (command.StartsWith("move_left:", StringComparison.OrdinalIgnoreCase) ||
                        command.StartsWith("move_right:", StringComparison.OrdinalIgnoreCase) ||
                        command.StartsWith("move_up:", StringComparison.OrdinalIgnoreCase) ||
                        command.StartsWith("move_down:", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = command.Split(':');
                        int frames;
                        if (parts.Length == 2 && int.TryParse(parts[1], out frames) && frames > 0 && frames <= 600)
                        {
                            _botMoveFrames = frames;
                            _botHorizontal = command.StartsWith("move_left:", StringComparison.OrdinalIgnoreCase) ? -1f :
                                command.StartsWith("move_right:", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
                            _botVertical = command.StartsWith("move_down:", StringComparison.OrdinalIgnoreCase) ? -1f :
                                command.StartsWith("move_up:", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
                            _botStatus = "moving for " + frames + " frames";
                        }
                        else _botStatus = "rejected command";
                    }
                    else if (command.StartsWith("scene:", StringComparison.OrdinalIgnoreCase))
                    {
                        string sceneName = command.Substring("scene:".Length).Trim();
                        if (sceneName == "Title" || sceneName == "BusStation" || sceneName == "BusStationExterior" ||
                            sceneName == "Playground" || sceneName == "MaeRoom")
                        {
                            _botStatus = "loading scene " + sceneName;
                            if (global::Global.dialogue != null)
                            {
                                global::Global.dialogue.Stop();
                                global::Global.dialogue.Clear();
                            }
                            global::Global.LoadLevel(sceneName);
                        }
                        else _botStatus = "rejected scene";
                    }
                    else if (string.Equals(command, "mutate_first_var", StringComparison.OrdinalIgnoreCase))
                    {
                        Dictionary<string, float> vars = StoryVariableStore();
                        if (vars == null || vars.Count == 0) _botStatus = "no story variable to mutate";
                        else
                        {
                            List<string> keys = new List<string>(vars.Keys);
                            keys.Sort(StringComparer.Ordinal);
                            _storyVariableProbe = keys[0];
                            vars[keys[0]] += 1234.5f;
                            _botStatus = "mutated story variable " + keys[0];
                        }
                    }
                    else _botStatus = "rejected command";
                }

                if (_botConfirmFrames > 0)
                {
                    state.ConfirmDown = true;
                    state.InteractDown = true;
                    _botConfirmFrames--;
                }
                if (_botMoveFrames > 0)
                {
                    state.Horizontal = _botHorizontal;
                    state.Vertical = _botVertical;
                    _botMoveFrames--;
                    if (_botMoveFrames == 0) _botStatus = "idle";
                }
            }
            catch (Exception e)
            {
                _botStatus = "bot error: " + e.GetType().Name;
                if (Time.frameCount % 300 == 0) Logger.LogWarning("In-game bot command failed: " + e.Message);
            }
        }

        private void PollHost()
        {
            byte[] bytes;
            IPEndPoint from;
            while (TryReceive(out bytes, out from))
            {
                Packet p;
                if (!Packet.TryRead(bytes, out p) || p.Type != 1) continue;
                if (_peer != null && !_peer.Equals(from)) continue;
                _peer = from;
                if (_haveSequence && !IsNewer(p.Sequence, _lastSequence)) continue;
                _haveSequence = true;
                _lastSequence = p.Sequence;
                GuestInput.CopyFrom(p.State);
                if (p.State.ConfirmDown) _testGuestConfirmPackets++;
                if (p.State.ConfirmDown && !_guestConfirmHeld)
                {
                    _pendingGuestConfirm = true;
                    _broadcastConfirmUntil = Time.realtimeSinceStartup + 0.3f;
                }
                _guestConfirmHeld = p.State.ConfirmDown;
                _lastGuestPacket = Time.realtimeSinceStartup;
                if (!_connected)
                {
                    _status = "connected to " + _peer;
                    Logger.LogInfo("Connected to peer " + _peer);
                }
                _connected = true;
            }
        }

        private void PollClient()
        {
            byte[] bytes;
            IPEndPoint from;
            while (TryReceive(out bytes, out from))
            {
                Packet p;
                if (!Packet.TryRead(bytes, out p) || (p.Type != 2 && p.Type != 3) || !_host.Equals(from)) continue;
                if (p.Type == 3)
                {
                    ReceiveStoryVariableFragment(p);
                    continue;
                }
                if (_haveSequence && !IsNewer(p.Sequence, _lastSequence)) continue;
                _haveSequence = true;
                _lastSequence = p.Sequence;
                uint previousChoiceCommitId = _hostChoiceCommitId;
                bool newChoiceCommit = p.ChoiceCommitId != 0 &&
                    (previousChoiceCommitId == 0 || IsNewer(p.ChoiceCommitId, previousChoiceCommitId));
                HostInput.CopyFrom(p.State);
                _hostSceneName = p.SceneName;
                _hasHostPosition = p.HasPosition;
                _hostPosition = p.Position;
                _hostOptionsActive = p.OptionsActive;
                _hostOptionIndex = p.OptionIndex;
                _hostOptionNode = p.OptionNode;
                _hostOptionSessionId = p.OptionSessionId;
                if (p.ChoiceCommitId != 0 && (_hostChoiceCommitId == 0 || IsNewer(p.ChoiceCommitId, _hostChoiceCommitId)))
                {
                    _hostChoiceCommitId = p.ChoiceCommitId;
                    _hostChoiceCommitSessionId = p.ChoiceCommitSessionId;
                    _hostChoiceCommitIndex = p.ChoiceCommitIndex;
                    _hostChoiceCommitNode = p.ChoiceCommitNode;
                }
                if (p.Variables != null) ApplyStoryVariables(p.Variables);
                if (p.State.ConfirmDown) _testHostConfirmPackets++;
                if (p.State.ConfirmDown && !_hostConfirmHeld)
                {
                    bool commitMatchesActiveSession = newChoiceCommit && SameChoiceSession(
                        p.OptionSessionId, p.ChoiceCommitSessionId, p.OptionNode, p.ChoiceCommitNode);
                    if (p.OptionsActive && !commitMatchesActiveSession)
                    {
                        _waitingChoiceConfirm = true;
                        _waitingChoiceCommitId = previousChoiceCommitId;
                        _waitingChoiceSessionId = p.OptionSessionId;
                        _waitingChoiceNode = p.OptionNode ?? "";
                        _waitingChoiceDeadline = Time.realtimeSinceStartup + 0.5f;
                    }
                    else _pendingHostConfirm = true;
                }
                if (_waitingChoiceConfirm && newChoiceCommit &&
                    IsNewer(p.ChoiceCommitId, _waitingChoiceCommitId) &&
                    SameChoiceSession(_waitingChoiceSessionId, p.ChoiceCommitSessionId,
                        _waitingChoiceNode, p.ChoiceCommitNode))
                {
                    _waitingChoiceConfirm = false;
                    _pendingHostConfirm = true;
                }
                _hostConfirmHeld = p.State.ConfirmDown;
                if (!_connected)
                {
                    _status = "connected to host";
                    Logger.LogInfo("Connected to host " + _host);
                }
                _connected = true;
            }
            if (_waitingChoiceConfirm && Time.realtimeSinceStartup >= _waitingChoiceDeadline)
            {
                _waitingChoiceConfirm = false;
                _pendingHostConfirm = true;
                _status = "choice commit missing; continuing with local option";
            }
        }

        private bool TryReceive(out byte[] bytes, out IPEndPoint from)
        {
            bytes = null;
            from = new IPEndPoint(IPAddress.Any, 0);
            if (_socket == null) return false;
            try
            {
                EndPoint endpoint = from;
                byte[] buffer = new byte[1200];
                int size = _socket.ReceiveFrom(buffer, ref endpoint);
                _testUdpReceived++;
                bytes = new byte[size];
                Buffer.BlockCopy(buffer, 0, bytes, 0, size);
                from = (IPEndPoint)endpoint;
                return true;
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock && e.SocketErrorCode != SocketError.IOPending && e.SocketErrorCode != SocketError.Interrupted)
                    Logger.LogWarning("UDP receive error: " + e.Message);
                return false;
            }
        }

        private void Send(IPEndPoint endpoint, byte[] packet)
        {
            try { if (_socket != null && endpoint != null) { _socket.SendTo(packet, endpoint); _testUdpSent++; } }
            catch (Exception e) { Logger.LogWarning("UDP send error: " + e.Message); }
        }

        private static Dictionary<string, float> ReadStoryVariables()
        {
            try
            {
                Dictionary<string, float> vars = StoryVariableStore();
                return vars == null ? null : new Dictionary<string, float>(vars);
            }
            catch { return null; }
        }

        private static Dictionary<string, float> StoryVariableStore()
        {
            try
            {
                global::Continuity continuity = global::Global.continuity;
                FieldInfo field = AccessTools.Field(typeof(global::Continuity), "vars");
                return continuity == null || field == null
                    ? null : field.GetValue(continuity) as Dictionary<string, float>;
            }
            catch { return null; }
        }

        private static string StoryVariableHash(Dictionary<string, float> variables)
        {
            unchecked
            {
                uint hash = 2166136261;
                List<string> keys = new List<string>(variables.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    byte[] name = Encoding.UTF8.GetBytes(keys[i]);
                    for (int j = 0; j < name.Length; j++) { hash ^= name[j]; hash *= 16777619; }
                    hash ^= 0; hash *= 16777619;
                    byte[] value = BitConverter.GetBytes(variables[keys[i]]);
                    for (int j = 0; j < value.Length; j++) { hash ^= value[j]; hash *= 16777619; }
                }
                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private void SendStoryVariableSnapshot(Dictionary<string, float> variables)
        {
            List<byte[]> fragments = Packet.CreateVariableFragments(++_storySnapshotId, variables);
            for (int i = 0; i < fragments.Count; i++) Send(_peer, fragments[i]);
            _storyVariableSync = "sent " + fragments.Count + " variable fragments";
        }

        private void ReceiveStoryVariableFragment(Packet packet)
        {
            if (packet.Variables == null || packet.FragmentCount == 0 || packet.FragmentIndex >= packet.FragmentCount) return;
            if (_pendingStoryVariables == null || packet.SnapshotId != _pendingStorySnapshotId)
            {
                if (_pendingStoryVariables != null && !IsNewer(packet.SnapshotId, _pendingStorySnapshotId)) return;
                _pendingStorySnapshotId = packet.SnapshotId;
                _pendingStoryFragmentCount = packet.FragmentCount;
                _receivedStoryFragments = new List<bool>(packet.FragmentCount);
                for (int i = 0; i < packet.FragmentCount; i++) _receivedStoryFragments.Add(false);
                _pendingStoryVariables = new Dictionary<string, float>();
            }
            if (packet.FragmentCount != _pendingStoryFragmentCount || _receivedStoryFragments[packet.FragmentIndex]) return;
            foreach (KeyValuePair<string, float> item in packet.Variables) _pendingStoryVariables[item.Key] = item.Value;
            _receivedStoryFragments[packet.FragmentIndex] = true;
            for (int i = 0; i < _receivedStoryFragments.Count; i++) if (!_receivedStoryFragments[i]) return;
            ApplyStoryVariables(_pendingStoryVariables);
            _pendingStoryVariables = null;
            _receivedStoryFragments = null;
        }

        private void ApplyStoryVariables(Dictionary<string, float> hostVariables)
        {
            if (IsHost) return;
            if (hostVariables == null) { _storyVariableSync = "host snapshot unavailable"; return; }
            try
            {
                global::Continuity continuity = global::Global.continuity;
                FieldInfo field = AccessTools.Field(typeof(global::Continuity), "vars");
                Dictionary<string, float> vars = continuity == null || field == null
                    ? null : field.GetValue(continuity) as Dictionary<string, float>;
                if (vars == null) { _storyVariableSync = "Continuity.vars unavailable"; return; }
                vars.Clear();
                foreach (KeyValuePair<string, float> item in hostVariables) vars[item.Key] = item.Value;
                _storyVariableCount = vars.Count;
                _storyVariableHash = StoryVariableHash(vars);
                _storyVariableSync = "host snapshot applied";
            }
            catch (Exception e) { _storyVariableSync = "apply failed: " + e.GetType().Name; }
        }

        private static InputState Merge(InputState host, InputState guest)
        {
            InputState value = new InputState();
            value.Horizontal = Mathf.Clamp(host.Horizontal + guest.Horizontal, -1f, 1f);
            value.Vertical = Mathf.Clamp(host.Vertical + guest.Vertical, -1f, 1f);
            value.JumpDown = host.JumpDown || guest.JumpDown;
            value.JumpHeld = host.JumpHeld || guest.JumpHeld;
            value.InteractDown = host.InteractDown || guest.InteractDown;
            value.InteractHeld = host.InteractHeld || guest.InteractHeld;
            value.ConfirmDown = host.ConfirmDown || guest.ConfirmDown;
            return value;
        }

        internal static float SharedAxis(string axis, float local)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.Connected) return local;
            InputState state = plugin.IsHost ? Merge(HostInput, GuestInput) : HostInput;
            if (axis == "Horizontal") return state.Horizontal;
            if (axis == "Vertical") return state.Vertical;
            return local;
        }

        internal static bool SharedJumpDown(bool local)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return local;
            return p.IsHost ? (local || GuestInput.JumpDown) : HostInput.JumpDown;
        }

        internal static bool SharedJumpHeld(bool local)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return local;
            return p.IsHost ? (local || GuestInput.JumpHeld) : HostInput.JumpHeld;
        }

        internal static bool SharedInteract(bool local)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return local;
            return p.IsHost ? (local || GuestInput.InteractDown) : HostInput.InteractDown;
        }

        internal static bool SharedConfirm(bool local)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return local;
            return p.IsHost ? (local || GuestInput.ConfirmDown) : HostInput.ConfirmDown;
        }

        private static bool SavePathPrefix(string filename, ref string __result)
        {
            CoopPlugin p = Instance;
            if (p == null) return true;
            __result = Path.Combine(p._saveRoot, Path.GetFileName(filename));
            return false;
        }

        private static bool GuestSavePrefix()
        {
            return Instance == null || Instance.IsHost;
        }

        private static void AxisPostfix(string axisName, ref float __result)
        {
            __result = SharedAxis(axisName, __result);
        }

        private static void JumpDownPostfix(ref bool __result)
        {
            __result = SharedJumpDown(__result);
        }

        private static void JumpHeldPostfix(ref bool __result)
        {
            __result = SharedJumpHeld(__result);
        }

        private static void InteractPostfix(global::Player __instance)
        {
            FieldInfo f = AccessTools.Field(typeof(global::Player), "interactInput");
            if (f != null) f.SetValue(__instance, SharedInteract((bool)f.GetValue(__instance)));
        }

        private static void ConfirmInputPostfix(ref bool __result)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return;
            bool local = __result;
            if (p.IsHost) p.PollHost(); else p.PollClient();
            if (p.IsHost && p._pendingGuestConfirm)
            {
                p._pendingGuestConfirm = false;
                __result = true;
            }
            else if (p.IsHost && p._pendingBotConfirm)
            {
                p._pendingBotConfirm = false;
                p._testBotConfirmConsumed++;
                __result = true;
            }
            else if (p.IsClient && p._pendingHostConfirm)
            {
                p._pendingHostConfirm = false;
                __result = true;
            }
            else __result = p.IsHost && local;
            if (__result) p._testConfirmObserved++;
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
            if (_socket != null) _socket.Close();
            if (Instance == this) Instance = null;
        }

        private int PortValue()
        {
            int port;
            string value = Environment.GetEnvironmentVariable("NITW_COOP_PORT");
            return int.TryParse(value, out port) && port > 0 && port < 65536 ? port : _port.Value;
        }

        private static string EnvironmentValue(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static bool IsNewer(uint candidate, uint previous)
        {
            return unchecked((int)(candidate - previous)) > 0;
        }
    }

    internal sealed class InputState
    {
        internal float Horizontal;
        internal float Vertical;
        internal bool JumpDown;
        internal bool JumpHeld;
        internal bool InteractDown;
        internal bool InteractHeld;
        internal bool ConfirmDown;

        internal void CopyFrom(InputState other)
        {
            Horizontal = other.Horizontal;
            Vertical = other.Vertical;
            JumpDown = other.JumpDown;
            JumpHeld = other.JumpHeld;
            InteractDown = other.InteractDown;
            InteractHeld = other.InteractHeld;
            ConfirmDown = other.ConfirmDown;
        }

        internal void Clear()
        {
            Horizontal = Vertical = 0f;
            JumpDown = JumpHeld = InteractDown = InteractHeld = ConfirmDown = false;
        }

        internal void ClearEdges()
        {
            JumpDown = InteractDown = ConfirmDown = false;
        }
    }

    internal sealed class Packet
    {
        internal byte Type;
        internal uint Sequence;
        internal InputState State;
        internal string SceneName;
        internal bool HasPosition;
        internal Vector3 Position;
        internal Dictionary<string, float> Variables;
        internal uint SnapshotId;
        internal ushort FragmentIndex;
        internal ushort FragmentCount;
        internal bool OptionsActive;
        internal int OptionIndex;
        internal string OptionNode;
        internal uint OptionSessionId;
        internal uint ChoiceCommitId;
        internal uint ChoiceCommitSessionId;
        internal int ChoiceCommitIndex;
        internal string ChoiceCommitNode;
        private const byte ProtocolVersion = 6;
        private const int MaxPacketBytes = 1200;
        private const int FragmentHeaderBytes = 13;

        internal static byte[] Create(byte type, uint sequence, InputState state,
            string sceneName, bool hasPosition, Vector3 position, Dictionary<string, float> variables,
            bool optionsActive, int optionIndex, string optionNode,
            uint optionSessionId, uint choiceCommitId, uint choiceCommitSessionId,
            int choiceCommitIndex, string choiceCommitNode)
        {
            byte[] sceneBytes = Encoding.UTF8.GetBytes(sceneName ?? "");
            if (sceneBytes.Length > 255) throw new InvalidOperationException("Scene name too long for co-op packet.");
            List<string> keys = new List<string>();
            if (type == 2 && variables != null)
            {
                foreach (string key in variables.Keys) keys.Add(key);
                keys.Sort(StringComparer.Ordinal);
            }
            if (keys.Count > ushort.MaxValue) throw new InvalidOperationException("Too many story variables for co-op packet.");
            using (MemoryStream stream = new MemoryStream(33 + sceneBytes.Length))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((byte)'N'); writer.Write((byte)'W'); writer.Write(ProtocolVersion); writer.Write(type);
                writer.Write(sequence);
                writer.Write(state.Horizontal); writer.Write(state.Vertical);
                byte flags = 0;
                if (state.JumpDown) flags |= 1;
                if (state.JumpHeld) flags |= 2;
                if (state.InteractDown) flags |= 4;
                if (state.InteractHeld) flags |= 8;
                if (state.ConfirmDown) flags |= 16;
                writer.Write(flags);
                writer.Write(hasPosition);
                writer.Write(position.x); writer.Write(position.y); writer.Write(position.z);
                writer.Write((byte)sceneBytes.Length);
                writer.Write(sceneBytes);
                bool hasVariables = type == 2 && variables != null;
                writer.Write(hasVariables);
                writer.Write((ushort)keys.Count);
                for (int i = 0; i < keys.Count; i++)
                {
                    byte[] keyBytes = Encoding.UTF8.GetBytes(keys[i]);
                    if (keyBytes.Length > 255) throw new InvalidOperationException("Story variable key too long.");
                    writer.Write((byte)keyBytes.Length);
                    writer.Write(keyBytes);
                    writer.Write(variables[keys[i]]);
                    if (stream.Length > MaxPacketBytes) throw new InvalidOperationException("Story variable snapshot exceeds safe UDP packet size.");
                }
                if (type == 2)
                {
                    writer.Write(optionsActive);
                    writer.Write(optionIndex);
                    byte[] nodeBytes = Encoding.UTF8.GetBytes(optionNode ?? "");
                    if (nodeBytes.Length > 255) throw new InvalidOperationException("Dialogue node name too long for co-op packet.");
                    writer.Write((byte)nodeBytes.Length);
                    writer.Write(nodeBytes);
                    writer.Write(optionSessionId);
                    byte[] commitNodeBytes = Encoding.UTF8.GetBytes(choiceCommitNode ?? "");
                    if (commitNodeBytes.Length > 255) throw new InvalidOperationException("Dialogue commit node name too long for co-op packet.");
                    writer.Write(choiceCommitId);
                    writer.Write(choiceCommitSessionId);
                    writer.Write(choiceCommitIndex);
                    writer.Write((byte)commitNodeBytes.Length);
                    writer.Write(commitNodeBytes);
                }
                return stream.ToArray();
            }
        }

        internal static List<byte[]> CreateVariableFragments(uint snapshotId, Dictionary<string, float> variables)
        {
            List<string> keys = new List<string>(variables.Keys);
            keys.Sort(StringComparer.Ordinal);
            List<List<byte[]>> chunks = new List<List<byte[]>>();
            List<byte[]> chunk = new List<byte[]>();
            int chunkBytes = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(keys[i]);
                if (keyBytes.Length > 255) throw new InvalidOperationException("Story variable key too long.");
                byte[] record;
                using (MemoryStream recordStream = new MemoryStream())
                using (BinaryWriter recordWriter = new BinaryWriter(recordStream))
                {
                    recordWriter.Write((byte)keyBytes.Length);
                    recordWriter.Write(keyBytes);
                    recordWriter.Write(variables[keys[i]]);
                    record = recordStream.ToArray();
                }
                if (record.Length + FragmentHeaderBytes > MaxPacketBytes) throw new InvalidOperationException("Story variable record exceeds UDP packet size.");
                if (chunk.Count > 0 && (chunkBytes + record.Length + FragmentHeaderBytes > MaxPacketBytes || chunk.Count == byte.MaxValue))
                {
                    chunks.Add(chunk);
                    chunk = new List<byte[]>();
                    chunkBytes = 0;
                }
                chunk.Add(record);
                chunkBytes += record.Length;
            }
            if (chunk.Count > 0 || chunks.Count == 0) chunks.Add(chunk);
            if (chunks.Count > ushort.MaxValue) throw new InvalidOperationException("Too many story variable fragments.");
            List<byte[]> result = new List<byte[]>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                using (MemoryStream stream = new MemoryStream(FragmentHeaderBytes + chunkBytes))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)'N'); writer.Write((byte)'W'); writer.Write(ProtocolVersion); writer.Write((byte)3);
                    writer.Write(snapshotId); writer.Write((ushort)i); writer.Write((ushort)chunks.Count);
                    writer.Write((byte)chunks[i].Count);
                    for (int j = 0; j < chunks[i].Count; j++) writer.Write(chunks[i][j]);
                    result.Add(stream.ToArray());
                }
            }
            return result;
        }

        internal static bool TryRead(byte[] bytes, out Packet packet)
        {
            packet = null;
            if (bytes == null || bytes.Length < 4 || bytes.Length > MaxPacketBytes) return false;
            using (MemoryStream stream = new MemoryStream(bytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadByte() != (byte)'N' || reader.ReadByte() != (byte)'W' || reader.ReadByte() != ProtocolVersion) return false;
                Packet p = new Packet();
                p.Type = reader.ReadByte();
                if (p.Type == 3)
                {
                    if (bytes.Length < FragmentHeaderBytes) return false;
                    p.SnapshotId = reader.ReadUInt32();
                    p.FragmentIndex = reader.ReadUInt16();
                    p.FragmentCount = reader.ReadUInt16();
                    if (p.FragmentCount == 0 || p.FragmentIndex >= p.FragmentCount) return false;
                    int entryCount = reader.ReadByte();
                    p.Variables = new Dictionary<string, float>();
                    for (int i = 0; i < entryCount; i++)
                    {
                        if (stream.Length - stream.Position < 1) return false;
                        int keyLength = reader.ReadByte();
                        if (stream.Length - stream.Position < keyLength + 4) return false;
                        p.Variables[Encoding.UTF8.GetString(reader.ReadBytes(keyLength))] = reader.ReadSingle();
                    }
                    if (stream.Position != stream.Length) return false;
                    packet = p;
                    return true;
                }
                if (bytes.Length < 34) return false;
                p.Sequence = reader.ReadUInt32();
                p.State = new InputState();
                p.State.Horizontal = reader.ReadSingle();
                p.State.Vertical = reader.ReadSingle();
                byte flags = reader.ReadByte();
                p.State.JumpDown = (flags & 1) != 0;
                p.State.JumpHeld = (flags & 2) != 0;
                p.State.InteractDown = (flags & 4) != 0;
                p.State.InteractHeld = (flags & 8) != 0;
                p.State.ConfirmDown = (flags & 16) != 0;
                p.HasPosition = reader.ReadBoolean();
                p.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                int sceneLength = reader.ReadByte();
                if (stream.Length - stream.Position < sceneLength + 3) return false;
                p.SceneName = Encoding.UTF8.GetString(reader.ReadBytes(sceneLength));
                bool hasVariables = reader.ReadBoolean();
                int variableCount = reader.ReadUInt16();
                if (!hasVariables && variableCount != 0) return false;
                p.Variables = hasVariables ? new Dictionary<string, float>() : null;
                for (int i = 0; i < variableCount; i++)
                {
                    if (stream.Length - stream.Position < 1) return false;
                    int keyLength = reader.ReadByte();
                    if (stream.Length - stream.Position < keyLength + 4) return false;
                    string key = Encoding.UTF8.GetString(reader.ReadBytes(keyLength));
                    float value = reader.ReadSingle();
                    p.Variables[key] = value;
                }
                if (p.Type == 2)
                {
                    if (stream.Length - stream.Position < 5) return false;
                    p.OptionsActive = reader.ReadBoolean();
                    p.OptionIndex = reader.ReadInt32();
                    if (p.OptionIndex < 0) return false;
                    if (stream.Length - stream.Position < 1) return false;
                    int nodeLength = reader.ReadByte();
                    if (stream.Length - stream.Position < nodeLength) return false;
                    p.OptionNode = Encoding.UTF8.GetString(reader.ReadBytes(nodeLength));
                    if (stream.Length - stream.Position < 4) return false;
                    p.OptionSessionId = reader.ReadUInt32();
                    if (stream.Length - stream.Position < 13) return false;
                    p.ChoiceCommitId = reader.ReadUInt32();
                    p.ChoiceCommitSessionId = reader.ReadUInt32();
                    p.ChoiceCommitIndex = reader.ReadInt32();
                    if (p.ChoiceCommitIndex < 0) return false;
                    if (stream.Length - stream.Position < 1) return false;
                    int commitNodeLength = reader.ReadByte();
                    if (stream.Length - stream.Position < commitNodeLength) return false;
                    p.ChoiceCommitNode = Encoding.UTF8.GetString(reader.ReadBytes(commitNodeLength));
                }
                if (stream.Position != stream.Length) return false;
                packet = p;
                return true;
            }
        }
    }
}

