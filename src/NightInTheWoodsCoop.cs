using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace NightInTheWoodsCoop
{
    [BepInPlugin("coop-anything.nitw", "Night in the Woods Co-op", "0.6.190")]
    public sealed class CoopPlugin : BaseUnityPlugin
    {
        private const float RemotePuppetRedTint = 0.78f;
        private const float RemotePuppetGreenTint = 0.90f;
        private const float RemotePuppetBlueTint = 1.00f;
        private const float RemotePuppetOpacity = 0.72f;
        private const float RemotePlayerSeparationRadius = 0.55f;
        private const float RemotePlayerSeparationNetworkMargin = 0.20f;
        private const int MaxDialogueLineTextBytes = 512;
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
        private float _lastHostPacket;
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
        private int _botInteractFrame = -1;
        private int _botJumpFrame = -1;
        private int _testBotJumpInjected;
        private int _testBotJumpInjectedFrame = -1;
        private bool _readingLocalInput;
        private float _hostInteractBroadcastUntil;
        private bool _guestInteractHeld;
        private bool _hostInteractHeld;
        private bool _pendingGuestInteract;
        private float _pendingGuestInteractDeadline;
        private bool _suppressGuestInteractUntilRelease;
        private float _hostRemoteInteractInjectedUntil;
        private bool _hostDialogueStartedByMirroredGuestInteract;
        private bool _pendingHostInteract;
        private bool _pendingHostDialogueConfirm;
        private bool _pendingHostGlobalConfirm;
        private bool _loggedPendingInputNext;
        private bool _loggedPendingGlobalConfirm;
        private int _testDialogueInputNextCalls;
        private int _testPendingInputNextCalls;
        private int _testPendingGlobalConfirmCalls;
        private string _testDialogueConfirmDiagnostics = "none";
        private float _botConfirmUntil;
        private int _botMoveFrames;
        private float _botHorizontal;
        private float _botVertical;
        private int _testConfirmObserved;
        private int _testBotConfirmConsumed;
        private int _testBotDialogueNextSignals;
        private int _testInteractSetCalls;
        private int _testBotInteractInjected;
        private int _testRewiredInteractCalls;
        private int _testBotRewiredInteractCalls;
        private int _testBotRewiredInteractDownQueries;
        private int _testGuestConfirmPackets;
        private int _testHostConfirmPackets;
        private int _testUdpSent;
        private int _testUdpReceived;
        private int _testChoiceCallbacks;
        private int _testChoiceCallbacksApplied;
        private string _testChoiceCallbackStatus = "none";
        private int _testDialogueRunCalls;
        private string _testDialogueRunStatus = "none";
        private int _testDialogueLineCalls;
        private string _testDialogueLineStatus = "none";
        private string _testLastDialogueLineLogged = "";
        private int _testYarnRunNextLogged;
        private int _testYarnCoroutineLogged;
        private int _testDialogueSayLogged;
        private int _testDialogueSayLastPc = int.MinValue;
        private int _testDialogueSayLastLogFrame = -1;
        private int _testDialogueCommandLogged;
        private int _testDialogueCommandLastPc = int.MinValue;
        private int _testDialogueCommandLastLogFrame = -1;
        private string _testYarnSetNodeStatus = "none";
        private string _testLastConfirm = "none";
        private int _testLastConfirmLogFrame = -1;
        private int _testOptionIteratorSteps;
        private int _testOptionIteratorFrame = -1;
        private int _testChoiceAxisDirection;
        private int _testChoiceAxisFrames;
        private string _testOptionIteratorStatus = "none";
        private bool _optionConfirmReady;
        private string _optionConfirmReadyStatus = "not sampled";
        private string _testMoveVector = "unobserved";
        private string _testAxisResult = "unobserved";
        private string _testHorizontalAxis = "unobserved";
        private string _testVerticalAxis = "unobserved";
        private int _testMoveVectorCalls;
        private int _testRemoteSeparationBlocks;
        private object _testOptionDialogue;
        private object _testOptionImplementation;
        private bool _guestConfirmHeld;
        private bool _guestConfirmBlockedUntilRelease;
        private uint _lastGuestInputDialogueRunId;
        private bool _hostConfirmHeld;
        private bool _pendingGuestConfirm;
        private bool _pendingHostConfirm;
        private bool _pendingBotConfirm;
        private bool _pendingBotIntroPoemConfirm;
        private bool _insideIntroPoemStartMoveNext;
        private int _testIntroPoemConfirmQueries;
        private int _testIntroPoemProgramCounter = -1;
        private string _testIntroPoemConfirmStatus = "idle";
        private bool _dialogueConfirmConsumedUntilRelease;
        private bool _dialogueConfirmPending;
        private bool _pendingClientChoiceConfirm;
        private bool _ignoreHostChoiceConfirmUntilRelease;
        private bool _lastLocalDialogueConfirm;
        private float _suppressLocalConfirmUntil;
        private int _testDialogueConfirmInjected;
        private int _testGuestStoryWritesBlocked;
        private bool _waitingChoiceConfirm;
        private bool _hasDeferredChoiceCallback;
        private bool _applyingDeferredChoiceCallback;
        private object _deferredChoiceTarget;
        private int _deferredLocalChoiceIndex;
        private string _deferredChoiceNode = "";
        private float _deferredChoiceDeadline;
        private uint _waitingChoiceCommitId;
        private uint _waitingChoiceSessionId;
        private string _waitingChoiceNode = "";
        private float _waitingChoiceDeadline;
        private float _hostConfirmBroadcastUntil;
        private string _hostSceneName = "";
        private readonly Dictionary<string, byte> _hostSceneBootstrapNodes = new Dictionary<string, byte>(StringComparer.Ordinal);
        private string _requestedHostScene = "";
        private string _sceneSyncStatus = "waiting";
        private string _sceneRootBootstrapStatus = "not observed";
        private string _lastLoggedSceneRootBootstrapStatus = "";
        private string _ravineLogDiagnosticScene = "";
        private readonly Dictionary<int, string> _ravineLogStompDiagnostics = new Dictionary<int, string>();
        private float _nextSceneRetryAt;
        private bool _hostOptionsActive;
        private int _hostOptionIndex;
        private string _hostOptionNode = "";
        private uint _hostOptionSessionId;
        private int _hostOptionCount;
        private List<string> _hostOptionLabels = new List<string>();
        private int _pendingGuestOptionIndex = -1;
        private uint _pendingGuestOptionSessionId;
        private int _clientChoiceSelectionRequestIndex = -1;
        private uint _clientChoiceSelectionRequestSessionId;
        private List<string> _clientActiveOptionLabels = new List<string>();
        private string _hostOptionText = "";
        private bool _hostOptionsWasActive;
        private uint _clientActiveOptionSessionId;
        private uint _lastAppliedChoiceSessionId;
        private string _clientActiveOptionNode = "";
        private int _clientActiveOptionCount;
        private uint _clientChoiceAxisSessionId;
        private float _clientChoicePreviousHorizontal;
        private uint _hostChoiceCommitId;
        private uint _hostChoiceCommitSessionId;
        private int _hostChoiceCommitIndex;
        private string _hostChoiceCommitNode = "";
        private string _hostChoiceCommitText = "";
        private uint _hostDialogueRunId;
        private uint _hostDialogueEndSentForRunId;
        private int _hostDialogueRunStartFrame;
        private uint _lastAppliedHostDialogueRunId;
        private uint _lastGuestDialogueControlOverrideRunId;
        private string _testGuestDialogueControlStatus = "inactive";
        private string _hostDialogueAsset = "";
        private string _hostDialogueNode = "";
        private string _hostDialogueWireNode = "";
        private bool _hostDialogueWireNodeReady;
        private float _hostDialogueFallbackHoldUntil;
        private string _hostDialogueRunStream = "";
        private int _hostDialogueOption;
        private uint _hostDialogueLineSequence;
        private uint _hostGlobalDialogueLineSequence;
        private string _hostDialogueLineFingerprint = "";
        private string _testLastSentDialogueLineEvents = "";
        private string _pendingGuestTvLineStream = "";
        private string _pendingGuestTvLineFingerprint = "";
        private string _pendingGuestTvLineText = "";
        private string _guestTvOverlayText = "";
        private Texture2D _guestTvCaptionBackground;
        private readonly Dictionary<string, Queue<DialogueLineEvent>> _pendingHostDialogueLines = new Dictionary<string, Queue<DialogueLineEvent>>(StringComparer.Ordinal);
        private readonly Dictionary<string, uint> _clientDialogueLineAcks = new Dictionary<string, uint>(StringComparer.Ordinal);
        private readonly Dictionary<string, uint> _lastAppliedHostDialogueLineSequences = new Dictionary<string, uint>(StringComparer.Ordinal);
        private readonly Dictionary<string, DialogueLineEvent> _hostDialogueLineEvents = new Dictionary<string, DialogueLineEvent>(StringComparer.Ordinal);
        private readonly Dictionary<string, uint> _claimedHostDialogueLineSequences = new Dictionary<string, uint>(StringComparer.Ordinal);
        private uint _clientDialogueLineAck;
        private uint _lastAppliedHostDialogueLineSequence;
        private string _pendingGuestDialogueLineFingerprint = "";
        private uint _hostGlobalDialogueLineRunId;
        private uint _lastGuestAutoAdvanceHostLineSequence;
        private bool _pendingHostDialogueStart;
        private bool _applyingHostDialogueReplay;
        private uint _lastAppliedChoiceCommitId;
        private bool _hasHostPosition;
        private Vector3 _hostPosition;
        private bool _hasGuestPosition;
        private Vector3 _guestPosition;
        private string _guestSceneName = "";
        private uint _guestInteractionRequestSequence;
        private uint _pendingGuestInteractionRequestId;
        private uint _lastGuestInteractionRequestId;
        private uint _pendingClientInteractionRequestId;
        private float _pendingClientInteractionDeadline;
        private string _pendingGuestInteractionAsset = "";
        private string _pendingGuestInteractionNode = "";
        private string _pendingClientInteractionAsset = "";
        private string _pendingClientInteractionNode = "";
        private string _guestInteractionStatus = "idle";
        private string _playerInteractionCompletionStatus = "idle";
        private int _playerInteractionCompletionStatusFrame = -1;
        private bool _playerInteractionCleanupPending;
        private int _playerInteractionCleanupAttempts;
        private int _playerInteractionCleanupLastAttemptFrame = -1;
        private int _playerInteractionCleanupDeadlineFrame = -1;
        private bool _capturingGuestPlayerInteraction;
        private object _capturingGuestInteractionIterator;
        // A guest interaction coroutine may call Dialogue.Run again after host cleanup.
        // Only a fresh local Interact edge may arm another request.
        private bool _guestInteractionCaptureArmed;
        private readonly Dictionary<object, bool> _guestInteractionCaptureConsumed = new Dictionary<object, bool>();
        private global::Player _puppetSource;
        private GameObject _remotePuppetRoot;
        private Animator _remoteAnimator;
        private string _puppetAnimatorStatus = "not created";
        private int _lastRemoteAnimationHash;
        private float _lastRemoteAnimationTime;
        private Animator _localUnityAnimator;
        private string _animationCaptureStatus = "not sampled";
        private readonly List<Transform> _puppetSourceTransforms = new List<Transform>();
        private readonly List<Transform> _puppetTransforms = new List<Transform>();
        private readonly List<SpriteRenderer> _puppetSourceSprites = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _puppetSprites = new List<SpriteRenderer>();
        private string _puppetStatus = "waiting for peer position";
        private int _storyVariableCount;
        private string _storyVariableSync = "waiting";
        private string _storyVariableHash = "00000000";
        private string _storyVariableProbe = "none";
        private string _lastStoryVariableHash = "";
        private string _lastStoryVariableDiagnosticHash = "";
        private Dictionary<string, float> _lastStoryVariableDiagnosticSnapshot;
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
            Patch("Continuity", "SetVar", "GuestNumericVariableWritePrefix", true);
            Patch("Continuity", "ChangeVar", "GuestNumericVariableWritePrefix", true);
            if (IsClient)
                Patch("Continuity", "Save", "GuestSavePrefix", true);
            Patch("Player", "GetAxisRaw", "AxisPostfix", false);
            Patch("Player", "GetMoveVector3", "MoveVectorPostfix", false);
            Patch("Player", "MoveX", "MoveXPrefix", true);
            Patch("Player", "GetJumpInputDown", "JumpDownPostfix", false);
            Patch("Player", "GetJumpInputHeld", "JumpHeldPostfix", false);
            Patch("Player", "SetInteractInput", "InteractPostfix", false);
            PatchGuestInteractionScope();

            PatchZeroDurationTitleIris();
            PatchSceneRootBootstrapDiagnostics();
            Patch("RavineLogStomp", "LateUpdate", "RavineLogStompPostfix", false);
            PatchRewiredButtonDown();
            Patch("Player", "Update", "PlayerStatePostfix", false);
            Patch("Player", "LateUpdate", "PlayerLateUpdatePostfix", false);
            Patch("Global", "IsConfirmOrInteractDown", "ConfirmInputPostfix", false);
            Patch("IntroPoemBrain/<Start>c__Iterator0", "MoveNext", "IntroPoemMoveNextPrefix", true);
            Patch("IntroPoemBrain/<Start>c__Iterator0", "MoveNext", "IntroPoemMoveNextPostfix", false);
            Patch("DialogueImplementation", "InputNext", "DialogueInputNextPrefix", true);
            Patch("DialogueImplementation", "IsPlayerStateDialogueControl", "DialogueControlStatePrefix", true);
            Patch("DialogueImplementation", "LateUpdate", "DialogueConfirmLateUpdatePostfix", false);
            PatchDialogueOptionIterator();
            PatchDialogueOptionCommit();
            PatchDialogueRun();
            PatchDialogueLine();
            PatchYarnVmDiagnostics();
            PatchDialogueSayDiagnostics();
            PatchDialogueCommandDiagnostics();

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

        private void PatchZeroDurationTitleIris()
        {
            Type transitionType = typeof(global::Continuity).Assembly.GetType("ScreenTransitioner", false);
            MethodInfo target = transitionType == null ? null : AccessTools.Method(transitionType, "IrisIn", new Type[] { typeof(float) });
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "ZeroDurationIrisPrefix");
            if (target == null || prefix == null)
            {
                Logger.LogWarning("Patch not installed: ScreenTransitioner.IrisIn(Single)");
                return;
            }
            _harmony.Patch(target, new HarmonyMethod(prefix), null, null, null, null);
            Logger.LogInfo("Patched ScreenTransitioner.IrisIn to guard zero-duration transitions");
        }

        private void PatchSceneRootBootstrapDiagnostics()
        {
            Type iteratorType = typeof(global::Continuity).Assembly.GetType("SceneRoot/<Start>c__Iterator0", false);
            MethodInfo target = iteratorType == null ? null : AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo postfix = AccessTools.Method(typeof(CoopPlugin), "SceneRootBootstrapMoveNextPostfix");
            if (target == null || postfix == null)
            {
                Logger.LogWarning("Patch not installed: SceneRoot.Start iterator diagnostics");
                return;
            }
            _harmony.Patch(target, null, new HarmonyMethod(postfix), null, null, null);
            Logger.LogInfo("Patched SceneRoot.Start iterator for bootstrap diagnostics");
        }

        private static void SceneRootBootstrapMoveNextPostfix(object __instance, bool __result)
        {
            if (Instance == null || __instance == null) return;
            string sceneName = Application.loadedLevelName ?? "";
            if (!string.Equals(Instance._ravineLogDiagnosticScene, sceneName, StringComparison.Ordinal))
            {
                Instance._ravineLogStompDiagnostics.Clear();
                Instance._ravineLogDiagnosticScene = sceneName;
            }
            Type iteratorType = __instance.GetType();
            FieldInfo pcField = AccessTools.Field(iteratorType, "$PC");
            FieldInfo currentField = AccessTools.Field(iteratorType, "$current");
            object pc = pcField == null ? (object)"?" : pcField.GetValue(__instance);
            object current = currentField == null ? null : currentField.GetValue(__instance);
            string currentName = current == null ? "null" : current.GetType().Name;
            string state = "pc=" + pc + ";yield=" + currentName + ";running=" + __result;
            Instance._sceneRootBootstrapStatus = state + ";frame=" + Time.frameCount;
            if (!string.Equals(Instance._lastLoggedSceneRootBootstrapStatus, state, StringComparison.Ordinal))
            {
                Instance._lastLoggedSceneRootBootstrapStatus = state;
                Instance.Logger.LogInfo("SceneRoot bootstrap " + state + ";frame=" + Time.frameCount);
            }
        }
        private static void RavineLogStompPostfix(object __instance)
        {
            if (Instance == null || __instance == null) return;
            UnityEngine.Component component = __instance as UnityEngine.Component;
            if (component == null) return;
            try
            {
                Type type = __instance.GetType();
                FieldInfo countField = AccessTools.Field(type, "stompCount");
                FieldInfo targetField = AccessTools.Field(type, "stomps");
                FieldInfo doneField = AccessTools.Field(type, "done");
                FieldInfo varField = AccessTools.Field(type, "varName");
                FieldInfo nodeField = AccessTools.Field(type, "dialogueNode");
                Vector3 position = component.transform.position;
                string details = component.gameObject.name + "@" +
                    position.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                    position.y.ToString("F2", CultureInfo.InvariantCulture) +
                    " stomp=" + (countField == null ? "?" : Convert.ToString(countField.GetValue(__instance), CultureInfo.InvariantCulture)) +
                    "/" + (targetField == null ? "?" : Convert.ToString(targetField.GetValue(__instance), CultureInfo.InvariantCulture)) +
                    " done=" + (doneField == null ? "?" : Convert.ToString(doneField.GetValue(__instance), CultureInfo.InvariantCulture)) +
                    " var=" + (varField == null ? "?" : Convert.ToString(varField.GetValue(__instance), CultureInfo.InvariantCulture)) +
                    " node=" + (nodeField == null ? "?" : Convert.ToString(nodeField.GetValue(__instance), CultureInfo.InvariantCulture));
                details += RavineCollisionBounds(type, __instance);
                int id = component.GetInstanceID();
                string previous;
                if (!Instance._ravineLogStompDiagnostics.TryGetValue(id, out previous) ||
                    !string.Equals(previous, details, StringComparison.Ordinal))
                {
                    Instance._ravineLogStompDiagnostics[id] = details;
                    Instance.Logger.LogInfo("Ravine stompable log " + details);
                }
            }
            catch (Exception e)
            {
                Instance.Logger.LogWarning("Ravine stompable log diagnostic failed: " + e.GetType().Name);
            }
        }

        private static string RavineCollisionBounds(Type stompType, object instance)
        {
            FieldInfo entityField = AccessTools.Field(stompType, "entity");
            object entity = entityField == null ? null : entityField.GetValue(instance);
            if (entity == null) return " collisionEntity=none";
            Type entityType = entity.GetType();
            FieldInfo boxField = AccessTools.Field(entityType, "collideBox");
            object box = boxField == null ? null : boxField.GetValue(entity);
            if (box == null) return " collisionEntity=" + entityType.Name + ";box=none";
            Type boxType = box.GetType();
            FieldInfo transformField = AccessTools.Field(boxType, "transform");
            UnityEngine.Transform boxTransform = transformField == null ? null : transformField.GetValue(box) as UnityEngine.Transform;
            Vector3 boxPosition = boxTransform == null ? Vector3.zero : boxTransform.position;
            FieldInfo widthField = AccessTools.Field(boxType, "width");
            FieldInfo heightField = AccessTools.Field(boxType, "height");
            FieldInfo xField = AccessTools.Field(boxType, "x");
            FieldInfo yField = AccessTools.Field(boxType, "y");
            FieldInfo oneWayField = AccessTools.Field(entityType, "oneWay");
            FieldInfo noFallField = AccessTools.Field(entityType, "noFallThrough");
            return " collisionEntity=" + entityType.Name +
                " boxPos=" + boxPosition.x.ToString("F2", CultureInfo.InvariantCulture) + "," + boxPosition.y.ToString("F2", CultureInfo.InvariantCulture) +
                " boxOffset=" + (xField == null ? "?" : Convert.ToString(xField.GetValue(box), CultureInfo.InvariantCulture)) + "," +
                    (yField == null ? "?" : Convert.ToString(yField.GetValue(box), CultureInfo.InvariantCulture)) +
                " boxSize=" + (widthField == null ? "?" : Convert.ToString(widthField.GetValue(box), CultureInfo.InvariantCulture)) + "x" +
                    (heightField == null ? "?" : Convert.ToString(heightField.GetValue(box), CultureInfo.InvariantCulture)) +
                " oneWay=" + (oneWayField == null ? "?" : Convert.ToString(oneWayField.GetValue(entity), CultureInfo.InvariantCulture)) +
                " noFallThrough=" + (noFallField == null ? "?" : Convert.ToString(noFallField.GetValue(entity), CultureInfo.InvariantCulture));
        }
        private void DumpRavineGeometry()
        {
            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsOfType(typeof(global::Entity));
            FieldInfo boxField = AccessTools.Field(typeof(global::Entity), "collideBox");
            FieldInfo typeField = AccessTools.Field(typeof(global::Entity), "type");
            FieldInfo oneWayField = AccessTools.Field(typeof(global::Entity), "oneWay");
            FieldInfo noFallField = AccessTools.Field(typeof(global::Entity), "noFallThrough");
            FieldInfo pointsField = AccessTools.Field(typeof(global::Entity), "points");
            FieldInfo surfaceField = AccessTools.Field(typeof(global::Entity), "surfaceLayerName");
            FieldInfo touchBoxField = AccessTools.Field(typeof(global::Entity), "touchBox");
            int logged = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                global::Entity entity = objects[i] as global::Entity;
                if (entity == null || boxField == null) continue;
                try
                {
                    object box = boxField.GetValue(entity);
                    if (box == null) continue;
                    Type boxType = box.GetType();
                    FieldInfo transformField = AccessTools.Field(boxType, "transform");
                    UnityEngine.Transform boxTransform = transformField == null ? null : transformField.GetValue(box) as UnityEngine.Transform;
                    if (boxTransform == null) continue;
                    Vector3 p = boxTransform.position;
                    FieldInfo widthField = AccessTools.Field(boxType, "width");
                    FieldInfo heightField = AccessTools.Field(boxType, "height");
                    FieldInfo xField = AccessTools.Field(boxType, "x");
                    FieldInfo yField = AccessTools.Field(boxType, "y");
                    if (widthField == null || heightField == null || xField == null || yField == null) continue;
                    float width = Convert.ToSingle(widthField.GetValue(box), CultureInfo.InvariantCulture);
                    float height = Convert.ToSingle(heightField.GetValue(box), CultureInfo.InvariantCulture);
                    float offsetX = Convert.ToSingle(xField.GetValue(box), CultureInfo.InvariantCulture);
                    float offsetY = Convert.ToSingle(yField.GetValue(box), CultureInfo.InvariantCulture);
                    Vector3 entityPosition = entity.transform.position;
                    float left = width > 0f ? p.x + offsetX - width * 0.5f : entityPosition.x;
                    float right = width > 0f ? p.x + offsetX + width * 0.5f : entityPosition.x;
                    float bottom = height > 0f ? p.y + offsetY - height * 0.5f : entityPosition.y;
                    float top = height > 0f ? p.y + offsetY + height * 0.5f : entityPosition.y;
                    if (right < -5f || left > 38f || top < -15f || bottom > 10f) continue;
                    UnityEngine.Vector3[] points = pointsField == null ? null : pointsField.GetValue(entity) as UnityEngine.Vector3[];
                    string pointSample = "";
                    if (points != null)
                    {
                        int pointLimit = entity.gameObject.name == "Collision" ? points.Length : Math.Min(points.Length, 4);
                        for (int pointIndex = 0; pointIndex < pointLimit; pointIndex++)
                        {
                            Vector3 worldPoint = entity.transform.TransformPoint(points[pointIndex]);
                            pointSample += (pointIndex == 0 ? "" : ";") + points[pointIndex].x.ToString("F2", CultureInfo.InvariantCulture) + "," + points[pointIndex].y.ToString("F2", CultureInfo.InvariantCulture) +
                                "=>" + worldPoint.x.ToString("F2", CultureInfo.InvariantCulture) + "," + worldPoint.y.ToString("F2", CultureInfo.InvariantCulture);
                        }
                        if (entity.gameObject.name == "Collision")
                        {
                            float pointLeft = float.MaxValue, pointRight = float.MinValue, pointBottom = float.MaxValue, pointTop = float.MinValue;
                            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
                            {
                                Vector3 worldPoint = entity.transform.TransformPoint(points[pointIndex]);
                                pointLeft = Math.Min(pointLeft, worldPoint.x); pointRight = Math.Max(pointRight, worldPoint.x);
                                pointBottom = Math.Min(pointBottom, worldPoint.y); pointTop = Math.Max(pointTop, worldPoint.y);
                            }
                            pointSample += " worldBounds=" + pointLeft.ToString("F2", CultureInfo.InvariantCulture) + ".." + pointRight.ToString("F2", CultureInfo.InvariantCulture) +
                                "," + pointBottom.ToString("F2", CultureInfo.InvariantCulture) + ".." + pointTop.ToString("F2", CultureInfo.InvariantCulture);
                        }
                    }
                    object touchBox = touchBoxField == null ? null : touchBoxField.GetValue(entity);
                    string touchSize = "none";
                    if (touchBox != null)
                    {
                        Type touchType = touchBox.GetType();
                        FieldInfo touchWidth = AccessTools.Field(touchType, "width");
                        FieldInfo touchHeight = AccessTools.Field(touchType, "height");
                        if (touchWidth != null && touchHeight != null)
                            touchSize = Convert.ToString(touchWidth.GetValue(touchBox), CultureInfo.InvariantCulture) + "x" + Convert.ToString(touchHeight.GetValue(touchBox), CultureInfo.InvariantCulture);
                    }
                    Logger.LogInfo("Ravine geometry x=" + left.ToString("F2", CultureInfo.InvariantCulture) + ".." + right.ToString("F2", CultureInfo.InvariantCulture) +
                        " y=" + bottom.ToString("F2", CultureInfo.InvariantCulture) + ".." + top.ToString("F2", CultureInfo.InvariantCulture) +
                        " object=" + entity.gameObject.name + " pos=" + entityPosition.x.ToString("F2", CultureInfo.InvariantCulture) + "," + entityPosition.y.ToString("F2", CultureInfo.InvariantCulture) +
                        " boxSize=" + width.ToString("F2", CultureInfo.InvariantCulture) + "x" + height.ToString("F2", CultureInfo.InvariantCulture) +
                        " type=" + (typeField == null ? "?" : Convert.ToString(typeField.GetValue(entity), CultureInfo.InvariantCulture)) +
                        " points=" + (points == null ? "?" : points.Length.ToString(CultureInfo.InvariantCulture)) + " sample=" + pointSample + " touchSize=" + touchSize +
                        " surface=" + (surfaceField == null ? "?" : Convert.ToString(surfaceField.GetValue(entity), CultureInfo.InvariantCulture)) +
                        " oneWay=" + (oneWayField == null ? "?" : Convert.ToString(oneWayField.GetValue(entity), CultureInfo.InvariantCulture)) +
                        " noFallThrough=" + (noFallField == null ? "?" : Convert.ToString(noFallField.GetValue(entity), CultureInfo.InvariantCulture)));
                    logged++;
                }
                catch (Exception e)
                {
                    Logger.LogWarning("Ravine geometry read failed: " + e.GetType().Name);
                }
            }
            Logger.LogInfo("Ravine geometry diagnostic complete; entities=" + objects.Length + " boxesLogged=" + logged);
        }
        private string RavineLogStompStatus()
        {
            if (_ravineLogStompDiagnostics.Count == 0) return "none observed";
            List<int> ids = new List<int>(_ravineLogStompDiagnostics.Keys);
            ids.Sort();
            List<string> statuses = new List<string>();
            for (int i = 0; i < ids.Count; i++) statuses.Add(_ravineLogStompDiagnostics[ids[i]]);
            return string.Join(" | ", statuses.ToArray());
        }
        private static void ZeroDurationIrisPrefix(ref float __0)
        {
            if (!float.IsNaN(__0) && !float.IsInfinity(__0) && __0 > 0f) return;
            __0 = 0.05f;
            if (Instance != null)
                Instance.Logger.LogWarning("Replaced invalid IrisIn duration with 0.05 seconds");
        }
        private void PatchRewiredButtonDown()
        {
            MethodInfo target = AccessTools.Method(typeof(Rewired.Player), "GetButtonDown", new Type[] { typeof(string) });
            MethodInfo patch = AccessTools.Method(typeof(CoopPlugin), "RewiredButtonDownPostfix");
            if (target == null || patch == null)
            {
                Logger.LogWarning("Patch not installed: Rewired.Player.GetButtonDown(string)");
                return;
            }
            _harmony.Patch(target, null, new HarmonyMethod(patch), null, null, null);
            Logger.LogInfo("Patched Rewired.Player.GetButtonDown(string)");
        }

        private void Update()
        {
            InputState local = ReadLocalInput();
            if (IsClient && _clientChoiceSelectionRequestIndex >= 0 &&
                _clientChoiceSelectionRequestSessionId != 0)
            {
                local.ChoiceRequestIndex = _clientChoiceSelectionRequestIndex;
                local.ChoiceRequestSessionId = _clientChoiceSelectionRequestSessionId;
            }
            _lastLocalInput.CopyFrom(local);
            if (IsHost)
            {
                if (local.InteractDown)
                    _hostInteractBroadcastUntil = Time.realtimeSinceStartup + 0.15f;
                PollHost();
                ProcessPendingGuestInteraction();
                CheckHostDialogueCompletion();
                HostInput.CopyFrom(local);
                if (_connected && Time.realtimeSinceStartup - _lastGuestPacket > 0.5f)
                {
                    ResetGuestSession("peer timed out; hosting locally");
                }
                if (Time.realtimeSinceStartup >= _nextSend && _peer != null)
                {
                    InputState shared = local;
                    shared.ConfirmDown = local.ConfirmDown ||
                        Time.realtimeSinceStartup < _hostConfirmBroadcastUntil;
                    if (Time.realtimeSinceStartup < _hostInteractBroadcastUntil) shared.InteractDown = true;
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
                    List<DialogueLineEvent> lineHeads = PendingDialogueLineHeads();
                    List<string> lineDescriptions = new List<string>();
                    for (int i = 0; i < lineHeads.Count; i++)
                        lineDescriptions.Add(lineHeads[i].Stream + "#" + lineHeads[i].Sequence + "/" + lineHeads[i].Fingerprint);
                    string lineEventSummary = string.Join(";", lineDescriptions.ToArray());
                    byte[] snapshot = Packet.Create(2, ++_sequence, shared,
                        Application.loadedLevelName, hasPlayer, playerPosition, null, _hostOptionsActive, _hostOptionIndex, _hostOptionNode,
                        _hostOptionText,
                        _hostOptionSessionId, _hostChoiceCommitId, _hostChoiceCommitSessionId,
                        _hostChoiceCommitIndex, _hostChoiceCommitNode, _hostChoiceCommitText,
                        _hostDialogueRunId, _hostDialogueOption, _hostDialogueAsset,
                        (_hostDialogueWireNodeReady ? _hostDialogueWireNode : ""), _hostDialogueRunStream,
                        lineHeads, _clientDialogueLineAcks, 0, "", "", _lastGuestInteractionRequestId, 0);
                    if (!string.Equals(lineEventSummary, _testLastSentDialogueLineEvents, StringComparison.Ordinal))
                    {
                        _testLastSentDialogueLineEvents = lineEventSummary;
                        if (lineHeads.Count > 0)
                            Logger.LogInfo("Sending host dialogue line events: " + lineEventSummary +
                                " packetBytes=" + snapshot.Length);
                    }
                    Send(_peer, snapshot);
                    _nextSend = Time.realtimeSinceStartup + 0.033f;
                }
            }
            else
            {
                if (_connected && Time.realtimeSinceStartup - _lastHostPacket > 0.5f)
                    ResetHostSession("host timed out; waiting to reconnect");
                PollClient();
                ExpirePendingClientInteractionRequest();
                ApplyPendingHostDialogue();
                if (Time.realtimeSinceStartup >= _nextSend)
                {
                    Vector3 playerPosition;
                    bool hasPlayer = TryGetPlayerPosition(out playerPosition);
                    Send(_host, Packet.Create(1, ++_sequence, local, Application.loadedLevelName, hasPlayer, playerPosition, null, false, 0, "", "", 0, 0, 0, 0, "", "", 0, 0, "", "", "", null, _clientDialogueLineAcks,
                        _pendingClientInteractionRequestId, _pendingClientInteractionAsset, _pendingClientInteractionNode, 0, _lastAppliedHostDialogueRunId));
                    _nextSend = Time.realtimeSinceStartup + 0.033f;
                }
            }
            WriteTestStatus();
        }

        private static string AnimationStateStatus(InputState state)
        {
            return state == null || !state.HasAnimation ? "none" : state.AnimationHash.ToString("X8", CultureInfo.InvariantCulture) + "/" + state.AnimationTime.ToString("F3", CultureInfo.InvariantCulture) + "/facing=" + state.Facing + "/walking=" + state.Walking;
        }

        private string PuppetAnimationStatus()
        {
            if (_remoteAnimator == null) return "none (" + _puppetAnimatorStatus + ")";
            try
            {
                AnimatorStateInfo state = _remoteAnimator.GetCurrentAnimatorStateInfo(0);
                return state.fullPathHash.ToString("X8", CultureInfo.InvariantCulture) + "/" + state.normalizedTime.ToString("F3", CultureInfo.InvariantCulture);
            }
            catch { return "error"; }
        }
        private string OptionIteratorStatusForReport()
        {
            int age = _testOptionIteratorFrame < 0 ? -1 : Time.frameCount - _testOptionIteratorFrame;
            bool iteratorSaysRunning = _testOptionIteratorStatus.IndexOf("running=True", StringComparison.Ordinal) >= 0;
            if (age > 120 || (iteratorSaysRunning && !_hostOptionsActive))
                return "stale(" + age + " frames): " + _testOptionIteratorStatus;
            return _testOptionIteratorStatus;
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
                    RecordStoryVariableDiagnosticSnapshot(currentStoryVariables);
                }
                string scene = Application.loadedLevelName;
                InputState sharedInput = IsHost ? Merge(HostInput, GuestInput) : HostInput;
                InputState remotePose = IsHost ? GuestInput : HostInput;
                string line = "role=" + RoleName + "\nconnected=" + _connected + "\nstatus=" + _status + "\nremoteMae=" + _puppetStatus +
                    "\nanimationCapture=" + _animationCaptureStatus + "\nlocalAnimation=" + AnimationStateStatus(_lastLocalInput) + "\nremoteAnimationPayload=" + AnimationStateStatus(remotePose) + "\npuppetAnimation=" + PuppetAnimationStatus() +
                    "\nscene=" + scene + "\nsceneSync=" + _sceneSyncStatus + "\nframe=" + Time.frameCount + "\nbot=" + _botStatus +
                    "\nconfirmObserved=" + _testConfirmObserved + "\nbotConfirmConsumed=" + _testBotConfirmConsumed +
                    "\nbotDialogueNextSignals=" + _testBotDialogueNextSignals +
                    "\nbotJumpInjected=" + _testBotJumpInjected + "\nintroPoemConfirmQueries=" + _testIntroPoemConfirmQueries +
                    "\nintroPoemPc=" + _testIntroPoemProgramCounter + "\nintroPoemConfirmStatus=" + _testIntroPoemConfirmStatus +
                    "\ninteractSetCalls=" + _testInteractSetCalls + "\nbotInteractInjected=" + _testBotInteractInjected +
                    "\nguestInteraction=" + _guestInteractionStatus +
                    "\nguestInteractSuppressed=" + _suppressGuestInteractUntilRelease +
                    "\nhostDialogueFromMirroredGuestInteract=" + _hostDialogueStartedByMirroredGuestInteract +
                    "\nrewiredInteractCalls=" + _testRewiredInteractCalls + "\nbotRewiredInteractCalls=" + _testBotRewiredInteractCalls +
                    "\nbotRewiredInteractDownQueries=" + _testBotRewiredInteractDownQueries +
                    "\ndialogueConfirmInjected=" + _testDialogueConfirmInjected +
                    "\nguestStoryWritesBlocked=" + _testGuestStoryWritesBlocked +
                    "\nguestConfirmPackets=" + _testGuestConfirmPackets +
                    "\nhostConfirmPackets=" + _testHostConfirmPackets +
                    "\nudpPacketsSent=" + _testUdpSent + "\nudpPacketsReceived=" + _testUdpReceived +
                    "\nchoiceCallbacks=" + _testChoiceCallbacks + "\nchoiceCallbacksApplied=" + _testChoiceCallbacksApplied +
                    "\nchoiceCallbackStatus=" + _testChoiceCallbackStatus +
                    "\ndialogueRunner=" + DialogueRunnerStatus() +
                    "\noptionDialogueRunner=" + DialogueRunnerStatus(_testOptionDialogue) +
                    "\ndialogueRunCalls=" + _testDialogueRunCalls + "\ndialogueRunStatus=" + _testDialogueRunStatus +
                    "\nscreenTransition=" + ScreenTransitionStatus() +
                    "\nsceneRootBootstrap=" + _sceneRootBootstrapStatus +
                    "\nravineLogStomps=" + RavineLogStompStatus() +
                    "\ndialogueLineCalls=" + _testDialogueLineCalls + "\ndialogueLineStatus=" + _testDialogueLineStatus +
                    "\nyarnSetNodeStatus=" + _testYarnSetNodeStatus + "\nyarnRunNextLogged=" + _testYarnRunNextLogged +
                    "\nhostDialogueLineSequence=" + _hostDialogueLineSequence + "\nhostDialogueLineFingerprint=" + _hostDialogueLineFingerprint +
                    "\nqueuedHostDialogueLines=" + PendingDialogueLineCount() +
                    "\nlastAppliedHostDialogueLineSequence=" + _lastAppliedHostDialogueLineSequence + "\nclientDialogueLineAck=" + _clientDialogueLineAck +
                    "\npendingGuestDialogueLineFingerprint=" + _pendingGuestDialogueLineFingerprint +
                    "\nlastGuestAutoAdvanceHostLineSequence=" + _lastGuestAutoAdvanceHostLineSequence +
                    "\npendingGuestConfirm=" + _pendingGuestConfirm + "\nguestConfirmHeld=" + _guestConfirmHeld +
                    "\nguestConfirmBlockedUntilRelease=" + _guestConfirmBlockedUntilRelease + "\nlastGuestInputDialogueRunId=" + _lastGuestInputDialogueRunId +
                    "\nhostDialogueRunStream=" + _hostDialogueRunStream + "\nhostGlobalDialogueLineSequence=" + _hostGlobalDialogueLineSequence +
                    "\nhostGlobalDialogueLineAcked=" + DialogueLineAcked("DialogueUI@Global", _hostGlobalDialogueLineSequence) +
                    "\nhostConfirmHeld=" + _hostConfirmHeld + "\npendingHostDialogueConfirm=" + _pendingHostDialogueConfirm + "\npendingHostGlobalConfirm=" + _pendingHostGlobalConfirm +
                    "\ndialogueConfirmConsumedUntilRelease=" + _dialogueConfirmConsumedUntilRelease +
                    "\ndialogueInputNextCalls=" + _testDialogueInputNextCalls + "\npendingInputNextCalls=" + _testPendingInputNextCalls +
                    "\npendingGlobalConfirmCalls=" + _testPendingGlobalConfirmCalls + "\ndialogueConfirmDiagnostics=" + _testDialogueConfirmDiagnostics +
                     "\nguestDialogueControl=" + _testGuestDialogueControlStatus +
                    "\nhostDialogueRunId=" + _hostDialogueRunId + "\nlastAppliedHostDialogueRunId=" + _lastAppliedHostDialogueRunId +
                    "\npendingHostDialogueStart=" + _pendingHostDialogueStart + "\nhostDialogueNode=" + _hostDialogueNode +
                    "\nlastConfirm=" + _testLastConfirm +
                    "\noptionIteratorSteps=" + _testOptionIteratorSteps + "\noptionIteratorStatus=" + OptionIteratorStatusForReport() +
                    "\noptionIteratorObservedFrame=" + _testOptionIteratorFrame +
                    "\noptionIteratorSampleAgeFrames=" + (_testOptionIteratorFrame < 0 ? -1 : Time.frameCount - _testOptionIteratorFrame) +
                    "\nlocalAxis=" +
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
                line += "optionsActive=" + _hostOptionsActive + "\noptionIndex=" + _hostOptionIndex +
                    "\noptionCount=" + (IsHost ? _hostOptionCount : _clientActiveOptionCount) + "\noptionNode=" + _hostOptionNode + "\noptionSession=" + _hostOptionSessionId +
                    "\nguestChoiceRequest=" + _clientChoiceSelectionRequestIndex + "/" + _clientChoiceSelectionRequestSessionId +
                    "\nchoiceCommitId=" + _hostChoiceCommitId + "\nchoiceCommitSession=" +
                    _hostChoiceCommitSessionId + "\nchoiceCommitIndex=" + _hostChoiceCommitIndex +
                    "\nchoiceCommitNode=" + _hostChoiceCommitNode + "\nchoiceCommitText=" + _hostChoiceCommitText +
                    "\nlastAppliedChoiceCommitId=" + _lastAppliedChoiceCommitId + "\nlastAppliedChoiceSessionId=" + _lastAppliedChoiceSessionId +
                    "\nclientActiveOptionNode=" + _clientActiveOptionNode + "\nhasDeferredChoiceCallback=" + _hasDeferredChoiceCallback +
                    "\noptionConfirmReady=" + _optionConfirmReady + "\noptionConfirmReadyStatus=" + _optionConfirmReadyStatus +
                    "\nclientActiveOptionSession=" + _clientActiveOptionSessionId +
                    "\npendingClientChoiceConfirm=" + _pendingClientChoiceConfirm +
                    "\npendingHostConfirm=" + _pendingHostConfirm +
                    "\nwaitingChoiceConfirm=" + _waitingChoiceConfirm +
                    "\nwaitingChoiceCommitId=" + _waitingChoiceCommitId + "\nwaitingChoiceSession=" +
                    _waitingChoiceSessionId + "\n";
                global::Player player = UnityEngine.Object.FindObjectOfType(typeof(global::Player)) as global::Player;
                if (player != null)
                {
                    Vector3 position = player.transform.position;
                    line += "player=" + position.x.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        position.y.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        position.z.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                        "playerControl=" + player.canMove + "," + player.movementEnabled + "," + player.moveState + "\n" +
                        "playerInstanceId=" + player.GetInstanceID() + "\n" +
                        "playerState=" + PlayerStateName(player) + "\n" +
                        "globalPlayerInstanceId=" + (global::Global.player == null ? "none" : global::Global.player.GetInstanceID().ToString()) + "\n" +
                        "globalPlayerState=" + PlayerStateName(global::Global.player) + "\n" +
                        "playerReferencesMatch=" + object.ReferenceEquals(player, global::Global.player) + "\n" +
                        "playerInteractionCompletion=" + _playerInteractionCompletionStatus + "\n" +
                        "playerInteractionCompletionFrame=" + _playerInteractionCompletionStatusFrame + "\n" +
                        "playerInteractionCompletionAgeFrames=" + (_playerInteractionCompletionStatusFrame < 0 ? -1 : Time.frameCount - _playerInteractionCompletionStatusFrame) + "\n" +
                        "playerInteractionCleanupPending=" + _playerInteractionCleanupPending + "\n" +
                        "playerInteractionCleanupAttempts=" + _playerInteractionCleanupAttempts + "\n" +
                        "playerMovement=" + _testMoveVector + "\n" +
                        "remoteSeparationBlocks=" + _testRemoteSeparationBlocks + "\n" +
                        "playerBasis=" + PlayerBasisStatus(player) + "\n" +
                        "axisPostfix=" + _testAxisResult + "\n" +
                        "axisHorizontal=" + _testHorizontalAxis + "\n" +
                        "axisVertical=" + _testVerticalAxis + "\n" +
                        "moveVectorCalls=" + _testMoveVectorCalls + "\n";
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
                if (IsGameLevelLoadInProgress())
                {
                    _sceneSyncStatus = "waiting for native scene load to " + _hostSceneName;
                    return;
                }
                if (!string.Equals(_requestedHostScene, _hostSceneName, StringComparison.Ordinal))
                {
                    _requestedHostScene = _hostSceneName;
                    _nextSceneRetryAt = 0f;
                }
                if (Time.realtimeSinceStartup >= _nextSceneRetryAt)
                {
                    _nextSceneRetryAt = Time.realtimeSinceStartup + 1.5f;
                    _sceneSyncStatus = "requesting host scene " + _hostSceneName;
                    if (global::Global.dialogue != null)
                    {
                        global::Global.dialogue.Stop();
                        global::Global.dialogue.Clear();
                    }
                    global::Global.LoadLevel(_hostSceneName);
                }
                return;
            }

            _sceneSyncStatus = "matched " + localScene;
            _requestedHostScene = "";
            _nextSceneRetryAt = 0f;
        }

        private static bool IsGameLevelLoadInProgress()
        {
            try
            {
                FieldInfo loadingField = AccessTools.Field(typeof(global::Global), "isLoading");
                return loadingField != null && (bool)loadingField.GetValue(null);
            }
            catch { return false; }
        }

        private void PatchDialogueOptionIterator()
        {
            Type implementationType = typeof(global::Continuity).Assembly.GetType("DialogueImplementation", false);
            Type iteratorType = implementationType == null ? null : implementationType.GetNestedType(
                "<RunOptions>c__Iterator5", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo target = iteratorType == null ? null : AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "DialogueOptionStepPrefix");
            MethodInfo postfix = AccessTools.Method(typeof(CoopPlugin), "DialogueOptionStepPostfix");
            if (target == null || prefix == null || postfix == null)
            {
                Logger.LogWarning("Dialogue option cursor sync unavailable: RunOptions iterator not found");
                return;
            }
            _harmony.Patch(target, new HarmonyMethod(prefix), new HarmonyMethod(postfix), null, null, null);
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

        private void PatchGuestInteractionScope()
        {
            Type interactionType = typeof(global::Continuity).Assembly.GetType("Interaction", false);
            Type iteratorType = interactionType == null ? null : interactionType.GetNestedType(
                "<Execute>c__Iterator0", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo target = iteratorType == null ? null : AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "GuestInteractionScopePrefix");
            MethodInfo postfix = AccessTools.Method(typeof(CoopPlugin), "GuestInteractionScopePostfix");
            MethodInfo finalizer = AccessTools.Method(typeof(CoopPlugin), "GuestInteractionScopeFinalizer");
            if (target == null || prefix == null || postfix == null || finalizer == null)
            {
                Logger.LogWarning("Guest interaction request sync unavailable: Interaction.Execute iterator not found");
                return;
            }
            _harmony.Patch(target, new HarmonyMethod(prefix), new HarmonyMethod(postfix), null, new HarmonyMethod(finalizer), null);
            Logger.LogInfo("Patched Interaction.Execute.MoveNext for guest interaction request capture");
        }
        private void PatchDialogueRun()
        {
            Type dialogueType = typeof(global::Continuity).Assembly.GetType("Dialogue", false);
            MethodInfo target = dialogueType == null ? null : AccessTools.Method(dialogueType, "Run",
                new Type[] { typeof(TextAsset), typeof(string), typeof(int) });
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "DialogueRunPrefix");
            if (target == null || prefix == null)
            {
                Logger.LogWarning("Dialogue start diagnostics unavailable: Dialogue.Run not found");
                return;
            }
            _harmony.Patch(target, new HarmonyMethod(prefix), null, null, null, null);
            Logger.LogInfo("Patched Dialogue.Run for host and guest dialogue-start diagnostics");
        }

        private void PatchDialogueLine()
        {
            Type dialogueUiType = typeof(global::Continuity).Assembly.GetType("Yarn.Unity.Example.DialogueUI", false);
            MethodInfo target = dialogueUiType == null ? null : AccessTools.Method(dialogueUiType, "RunLine");
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "DialogueLinePrefix");
            if (target == null || prefix == null)
            {
                Logger.LogWarning("Dialogue line diagnostics unavailable: DialogueUI.RunLine not found");
                return;
            }
            _harmony.Patch(target, new HarmonyMethod(prefix), null, null, null, null);
            MethodInfo postfix = AccessTools.Method(typeof(CoopPlugin), "DialogueLineGatePostfix");
            if (postfix != null) _harmony.Patch(target, null, new HarmonyMethod(postfix), null, null, null);
            Logger.LogInfo("Patched DialogueUI.RunLine for diagnostics and host-authoritative line gating");
        }

        private void PatchYarnVmDiagnostics()
        {
            Type vmType = typeof(global::Continuity).Assembly.GetType("Yarn.VirtualMachine", false);
            MethodInfo setNode = vmType == null ? null : AccessTools.Method(vmType, "SetNode", new Type[] { typeof(string) });
            MethodInfo runNext = vmType == null ? null : AccessTools.Method(vmType, "RunNext");
            MethodInfo setNodePostfix = AccessTools.Method(typeof(CoopPlugin), "YarnVmSetNodePostfix");
            MethodInfo runNextPostfix = AccessTools.Method(typeof(CoopPlugin), "YarnVmRunNextPostfix");
            if (setNode != null && setNodePostfix != null)
            {
                _harmony.Patch(setNode, null, new HarmonyMethod(setNodePostfix), null, null, null);
                Logger.LogInfo("Patched Yarn.VirtualMachine.SetNode for guest node diagnostics");
            }
            else Logger.LogWarning("Yarn VM node diagnostics unavailable: VirtualMachine.SetNode not found");
            if (runNext != null && runNextPostfix != null)
            {
                MethodInfo runNextPrefix = AccessTools.Method(typeof(CoopPlugin), "YarnVmRunNextPrefix");
                _harmony.Patch(runNext, new HarmonyMethod(runNextPrefix), new HarmonyMethod(runNextPostfix), null, null, null);
                Logger.LogInfo("Patched Yarn.VirtualMachine.RunNext for guest VM-step diagnostics and TV command bypass");
            }
            else Logger.LogWarning("Yarn VM step diagnostics unavailable: VirtualMachine.RunNext not found");

            Type runnerType = typeof(global::Continuity).Assembly.GetType("Yarn.Unity.DialogueRunner", false);
            MethodInfo startDialogue = runnerType == null ? null : AccessTools.Method(runnerType,
                "StartDialogue", new Type[] { typeof(string) });
            MethodInfo startPostfix = AccessTools.Method(typeof(CoopPlugin), "YarnRunnerStartDialoguePostfix");
            if (startDialogue != null && startPostfix != null)
            {
                _harmony.Patch(startDialogue, null, new HarmonyMethod(startPostfix), null, null, null);
                Logger.LogInfo("Patched Yarn.DialogueRunner.StartDialogue(string) for guest coroutine diagnostics");
            }
            else Logger.LogWarning("Yarn runner start diagnostics unavailable: StartDialogue(string) not found");

            Type runnerIteratorType = runnerType == null ? null : runnerType.GetNestedType(
                "<RunDialogue>c__Iterator0", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo moveNext = runnerIteratorType == null ? null : AccessTools.Method(runnerIteratorType, "MoveNext");
            MethodInfo moveNextPostfix = AccessTools.Method(typeof(CoopPlugin), "YarnRunnerMoveNextPostfix");
            if (moveNext != null && moveNextPostfix != null)
            {
                _harmony.Patch(moveNext, null, new HarmonyMethod(moveNextPostfix), null, null, null);
                Logger.LogInfo("Patched Yarn DialogueRunner.RunDialogue.MoveNext for guest coroutine diagnostics");
            }
            else Logger.LogWarning("Yarn runner coroutine diagnostics unavailable: RunDialogue iterator not found");
        }

        private void PatchDialogueSayDiagnostics()
        {
            Type implementationType = typeof(global::Continuity).Assembly.GetType("DialogueImplementation", false);
            Type iteratorType = implementationType == null ? null : implementationType.GetNestedType(
                "<Say>c__Iterator1", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo moveNext = iteratorType == null ? null : AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo postfix = AccessTools.Method(typeof(CoopPlugin), "DialogueSayMoveNextPostfix");
            if (moveNext != null && postfix != null)
            {
                _harmony.Patch(moveNext, null, new HarmonyMethod(postfix), null, null, null);
                Logger.LogInfo("Patched DialogueImplementation.Say.MoveNext for transition wait diagnostics");
            }
            else Logger.LogWarning("Dialogue Say diagnostics unavailable: Say iterator MoveNext not found");
        }

        private void PatchDialogueCommandDiagnostics()
        {
            Type implementationType = typeof(global::Continuity).Assembly.GetType("DialogueImplementation", false);
            Type iteratorType = implementationType == null ? null : implementationType.GetNestedType(
                "<RunCommand>c__Iterator6", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo moveNext = iteratorType == null ? null : AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo prefix = AccessTools.Method(typeof(CoopPlugin), "DialogueCommandMoveNextPrefix");
            MethodInfo postfix = AccessTools.Method(typeof(CoopPlugin), "DialogueCommandMoveNextPostfix");
            if (moveNext != null && prefix != null && postfix != null)
            {
                _harmony.Patch(moveNext, new HarmonyMethod(prefix), new HarmonyMethod(postfix), null, null, null);
                Logger.LogInfo("Patched DialogueImplementation.RunCommand.MoveNext for TV animation diagnostics");
            }
            else Logger.LogWarning("Dialogue command diagnostics unavailable: RunCommand iterator MoveNext not found");
        }
        private void ResumeClientTransitionIfPaused()
        {
            if (!IsClient || !_connected) return;
            try
            {
                Type globalType = typeof(global::Continuity).Assembly.GetType("Global", false);
                FieldInfo transitionField = globalType == null ? null : AccessTools.Field(globalType, "screenTransitioner");
                object transitioner = transitionField == null ? null : transitionField.GetValue(null);
                if (transitioner == null) return;
                MethodInfo pausedMethod = AccessTools.Method(transitioner.GetType(), "IsPaused");
                MethodInfo resumeMethod = AccessTools.Method(transitioner.GetType(), "Resume");
                if (pausedMethod != null && resumeMethod != null && (bool)pausedMethod.Invoke(transitioner, null))
                {
                    resumeMethod.Invoke(transitioner, null);
                    Logger.LogInfo("Resumed guest screen transition when applying host dialogue start");
                }
            }
            catch (Exception e) { Logger.LogWarning("Guest transition resume failed: " + e.GetType().Name); }
        }
        private string ScreenTransitionStatus()
        {
            try
            {
                Type globalType = typeof(global::Continuity).Assembly.GetType("Global", false);
                FieldInfo transitionField = globalType == null ? null : AccessTools.Field(globalType, "screenTransitioner");
                object transitioner = transitionField == null ? null : transitionField.GetValue(null);
                if (transitioner == null) return "none";
                Type transitionType = transitioner.GetType();
                MethodInfo pausedMethod = AccessTools.Method(transitionType, "IsPaused");
                MethodInfo transitioningMethod = AccessTools.Method(transitionType, "IsTransitioning");
                FieldInfo stateField = AccessTools.Field(transitionType, "state");
                FieldInfo percentField = AccessTools.Field(transitionType, "percent");
                FieldInfo effectField = AccessTools.Field(transitionType, "currentEffect");
                object paused = pausedMethod == null ? null : pausedMethod.Invoke(transitioner, null);
                object transitioning = transitioningMethod == null ? null : transitioningMethod.Invoke(transitioner, null);
                object state = stateField == null ? null : stateField.GetValue(transitioner);
                object percent = percentField == null ? null : percentField.GetValue(transitioner);
                object effect = effectField == null ? null : effectField.GetValue(transitioner);
                return "paused=" + (paused ?? "unknown") + ";transitioning=" + (transitioning ?? "unknown") +
                    ";state=" + (state ?? "unknown") + ";percent=" + (percent ?? "unknown") +
                    ";effect=" + (effect == null ? "none" : effect.GetType().Name + ":" + effect);
            }
            catch (Exception e) { return "error=" + e.GetType().Name; }
        }

        private static void DialogueSayMoveNextPostfix(object __instance, bool __result)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.Connected || plugin._testDialogueSayLogged >= 96) return;
            try
            {
                FieldInfo pcField = AccessTools.Field(__instance.GetType(), "$PC");
                FieldInfo lineField = AccessTools.Field(__instance.GetType(), "line");
                FieldInfo characterField = AccessTools.Field(__instance.GetType(), "characterName");
                int pc = pcField == null ? int.MinValue : Convert.ToInt32(pcField.GetValue(__instance), CultureInfo.InvariantCulture);
                int frame = Time.frameCount;
                if (pc == plugin._testDialogueSayLastPc && frame - plugin._testDialogueSayLastLogFrame < 300) return;
                plugin._testDialogueSayLastPc = pc;
                plugin._testDialogueSayLastLogFrame = frame;
                plugin._testDialogueSayLogged++;
                string line = lineField == null ? "" : lineField.GetValue(__instance) as string ?? "";
                string character = characterField == null ? "" : characterField.GetValue(__instance) as string ?? "";
                if (line.Length > 100) line = line.Substring(0, 100);
                plugin.Logger.LogInfo("Dialogue Say.MoveNext: node=" + plugin._hostDialogueNode +
                    " state=" + pc + " result=" + __result + " character=" + character +
                    " line=" + line + " transition={" + plugin.ScreenTransitionStatus() + "}");
            }
            catch (Exception e) { plugin.Logger.LogWarning("Dialogue Say diagnostic failed: " + e.GetType().Name); }
        }
        private static bool DialogueCommandMoveNextPrefix(object __instance, ref bool __result)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected ||
                !string.Equals(plugin._hostDialogueNode, "TV", StringComparison.Ordinal)) return true;
            try
            {
                FieldInfo tokensField = AccessTools.Field(__instance.GetType(), "<tokens>__0");
                string[] tokens = tokensField == null ? null : tokensField.GetValue(__instance) as string[];
                if (tokens == null || tokens.Length < 2 ||
                    !string.Equals(tokens[0], "animate", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(tokens[1], "TV", StringComparison.OrdinalIgnoreCase)) return true;
                plugin.Logger.LogInfo("Skipped guest-local TV animation command to unblock host-authoritative dialogue: " + string.Join(" ", tokens));
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                plugin.Logger.LogWarning("Guest TV animation bypass failed: " + e.GetType().Name);
                return true;
            }
        }
        private static void DialogueCommandMoveNextPostfix(object __instance, bool __result)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.Connected || plugin._testDialogueCommandLogged >= 96) return;
            try
            {
                FieldInfo tokensField = AccessTools.Field(__instance.GetType(), "<tokens>__0");
                string[] tokens = tokensField == null ? null : tokensField.GetValue(__instance) as string[];
                if (tokens == null || tokens.Length == 0) return;
                string command = string.Join(" ", tokens);
                if (command.IndexOf("animate", StringComparison.OrdinalIgnoreCase) < 0) return;
                FieldInfo pcField = AccessTools.Field(__instance.GetType(), "$PC");
                FieldInfo gameObjectField = AccessTools.Field(__instance.GetType(), "<gameObject>__21");
                FieldInfo animatorField = AccessTools.Field(__instance.GetType(), "<spriteAnimator>__22");
                int pc = pcField == null ? int.MinValue : Convert.ToInt32(pcField.GetValue(__instance), CultureInfo.InvariantCulture);
                int frame = Time.frameCount;
                if (pc == plugin._testDialogueCommandLastPc && frame - plugin._testDialogueCommandLastLogFrame < 300) return;
                plugin._testDialogueCommandLastPc = pc;
                plugin._testDialogueCommandLastLogFrame = frame;
                plugin._testDialogueCommandLogged++;
                GameObject target = gameObjectField == null ? null : gameObjectField.GetValue(__instance) as GameObject;
                object spriteAnimator = animatorField == null ? null : animatorField.GetValue(__instance);
                object animation = null;
                object playing = null;
                if (spriteAnimator != null)
                {
                    PropertyInfo currentProperty = AccessTools.Property(spriteAnimator.GetType(), "currentAnimation");
                    PropertyInfo playingProperty = AccessTools.Property(spriteAnimator.GetType(), "playing");
                    animation = currentProperty == null ? null : currentProperty.GetValue(spriteAnimator, null);
                    playing = playingProperty == null ? null : playingProperty.GetValue(spriteAnimator, null);
                }
                FieldInfo animationNameField = animation == null ? null : AccessTools.Field(animation.GetType(), "name");
                string animationName = animationNameField == null ? "none" : animationNameField.GetValue(animation) as string ?? "unknown";
                string targetStatus = target == null ? "none" : target.name + ";active=" + target.activeInHierarchy;
                plugin.Logger.LogInfo("Dialogue RunCommand.MoveNext: node=" + plugin._hostDialogueNode +
                    " state=" + pc + " result=" + __result + " command=" + command +
                    " target=" + targetStatus + " animator=" + (spriteAnimator == null ? "none" : "present") +
                    " animation=" + animationName + " playing=" + (playing ?? "unknown") +
                    " transition={" + plugin.ScreenTransitionStatus() + "}");
            }
            catch (Exception e) { plugin.Logger.LogWarning("Dialogue command diagnostic failed: " + e.GetType().Name); }
        }
        private static bool IsHostReplayRunner(object runner, CoopPlugin plugin)
        {
            if (runner == null || plugin == null || string.IsNullOrEmpty(plugin._hostDialogueRunStream)) return false;
            FieldInfo uiField = AccessTools.Field(runner.GetType(), "dialogueUI");
            object ui = uiField == null ? null : uiField.GetValue(runner);
            return string.Equals(DialogueStreamPath(DialogueUiStreamKey(ui)),
                DialogueStreamPath(plugin._hostDialogueRunStream), StringComparison.Ordinal);
        }

        private static string DialogueStreamPath(string stream)
        {
            if (string.IsNullOrEmpty(stream)) return "";
            int separator = stream.IndexOf('@');
            return separator < 0 ? stream : stream.Substring(separator + 1);
        }

        private static void YarnRunnerStartDialoguePostfix(object __instance, string __0)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected || !IsHostReplayRunner(__instance, plugin)) return;
            try
            {
                plugin._testYarnCoroutineLogged = 0;
                Component runner = __instance as Component;
                Behaviour runnerBehaviour = __instance as Behaviour;
                PropertyInfo runningProperty = AccessTools.Property(__instance.GetType(), "isDialogueRunning");
                PropertyInfo nodeProperty = AccessTools.Property(__instance.GetType(), "currentNodeName");
                string runnerNode = nodeProperty == null ? "" : nodeProperty.GetValue(__instance, null) as string ?? "";
                string running = runningProperty == null ? "unknown" : Convert.ToString(runningProperty.GetValue(__instance, null), CultureInfo.InvariantCulture);
                plugin.Logger.LogInfo("Guest Yarn runner StartDialogue: node=" + (__0 ?? "") +
                    " stream=" + plugin._hostDialogueRunStream + " active=" +
                    (runner == null ? "unknown" : runner.gameObject.activeInHierarchy.ToString()) +
                    " enabled=" + (runnerBehaviour == null ? "unknown" : runnerBehaviour.enabled.ToString()) +
                    " running=" + running + " current=" + runnerNode);
            }
            catch (Exception e) { plugin.Logger.LogWarning("Guest Yarn runner start diagnostic failed: " + e.GetType().Name); }
        }

        private static void YarnRunnerMoveNextPostfix(object __instance, bool __result)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected || plugin._testYarnCoroutineLogged >= 32) return;
            try
            {
                FieldInfo runnerField = AccessTools.Field(__instance.GetType(), "$this");
                object runner = runnerField == null ? null : runnerField.GetValue(__instance);
                if (!IsHostReplayRunner(runner, plugin)) return;
                FieldInfo pcField = AccessTools.Field(__instance.GetType(), "$PC");
                FieldInfo nodeField = AccessTools.Field(__instance.GetType(), "startNode");
                FieldInfo currentField = AccessTools.Field(__instance.GetType(), "$current");
                object current = currentField == null ? null : currentField.GetValue(__instance);
                string statePc = pcField == null ? "unknown" : Convert.ToString(pcField.GetValue(__instance), CultureInfo.InvariantCulture);
                string node = nodeField == null ? "" : nodeField.GetValue(__instance) as string ?? "";
                string yielded = current == null ? "null" : current.GetType().FullName;
                PropertyInfo runningProperty = runner == null ? null : AccessTools.Property(runner.GetType(), "isDialogueRunning");
                string running = runningProperty == null ? "unknown" : Convert.ToString(runningProperty.GetValue(runner, null), CultureInfo.InvariantCulture);
                plugin._testYarnCoroutineLogged++;
                plugin.Logger.LogInfo("Guest Yarn RunDialogue.MoveNext: node=" + node + " state=" + statePc +
                    " result=" + __result + " yielded=" + yielded + " runnerRunning=" + running +
                    " step=" + plugin._testYarnCoroutineLogged);
            }
            catch (Exception e) { plugin.Logger.LogWarning("Guest Yarn runner coroutine diagnostic failed: " + e.GetType().Name); }
        }

        private static void YarnVmSetNodePostfix(object __instance, string __0, bool __result)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected ||
                !string.Equals(__0 ?? "", plugin._hostDialogueNode ?? "", StringComparison.Ordinal)) return;
            try
            {
                Type vmType = __instance.GetType();
                FieldInfo stateField = AccessTools.Field(vmType, "state");
                object state = stateField == null ? null : stateField.GetValue(__instance);
                FieldInfo nodeField = state == null ? null : AccessTools.Field(state.GetType(), "currentNodeName");
                FieldInfo pcField = state == null ? null : AccessTools.Field(state.GetType(), "programCounter");
                string currentNode = nodeField == null ? "" : nodeField.GetValue(state) as string ?? "";
                string pc = pcField == null ? "unknown" : Convert.ToString(pcField.GetValue(state), CultureInfo.InvariantCulture);
                plugin._testYarnSetNodeStatus = "requested=" + __0 + " accepted=" + __result +
                    " current=" + currentNode + " pc=" + pc;
                plugin.Logger.LogInfo("Guest Yarn VM SetNode: " + plugin._testYarnSetNodeStatus);
            }
            catch (Exception e)
            {
                plugin._testYarnSetNodeStatus = "diagnostic error: " + e.GetType().Name;
                plugin.Logger.LogWarning("Guest Yarn VM SetNode diagnostic failed: " + e.GetType().Name);
            }
        }

        private static bool YarnVmRunNextPrefix(object __instance)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected ||
                !string.Equals(plugin._hostDialogueNode, "TV", StringComparison.Ordinal)) return true;
            try
            {
                Type vmType = __instance.GetType();
                FieldInfo stateField = AccessTools.Field(vmType, "state");
                object state = stateField == null ? null : stateField.GetValue(__instance);
                if (state == null) return true;
                FieldInfo currentNodeField = AccessTools.Field(state.GetType(), "currentNodeName");
                string currentNode = currentNodeField == null ? "" : currentNodeField.GetValue(state) as string ?? "";
                if (!string.Equals(currentNode, "TV", StringComparison.Ordinal)) return true;
                FieldInfo pcField = AccessTools.Field(state.GetType(), "programCounter");
                if (pcField == null) return true;
                int pc = Convert.ToInt32(pcField.GetValue(state), CultureInfo.InvariantCulture);
                FieldInfo nodeField = AccessTools.Field(vmType, "currentNode");
                object node = nodeField == null ? null : nodeField.GetValue(__instance);
                FieldInfo instructionsField = node == null ? null : AccessTools.Field(node.GetType(), "instructions");
                IList instructions = instructionsField == null ? null : instructionsField.GetValue(node) as IList;
                if (instructions == null || pc < 0 || pc >= instructions.Count) return true;
                object instruction = instructions[pc];
                FieldInfo operationField = AccessTools.Field(instruction.GetType(), "operation");
                FieldInfo operandAField = AccessTools.Field(instruction.GetType(), "operandA");
                object operation = operationField == null ? null : operationField.GetValue(instruction);
                object operandA = operandAField == null ? null : operandAField.GetValue(instruction);
                string command = operandA as string;
                if (!string.Equals(Convert.ToString(operation, CultureInfo.InvariantCulture), "RunCommand", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(command)) return true;
                bool isTvAnimation = command.StartsWith("animate TV ", StringComparison.OrdinalIgnoreCase);
                bool isTvTimingWait = command.StartsWith("wait ", StringComparison.OrdinalIgnoreCase);
                if (!isTvAnimation && !isTvTimingWait) return true;
                pcField.SetValue(state, pc + 1);
                plugin.Logger.LogInfo("Skipped guest TV Yarn " + (isTvAnimation ? "animation" : "timing wait") +
                    " instruction and advanced program counter " + pc + "->" + (pc + 1) + ": " + command);
                return false;
            }
            catch (Exception e)
            {
                plugin.Logger.LogWarning("Guest TV Yarn animation bypass failed: " + e.GetType().Name);
                return true;
            }
        }
        private static void YarnVmRunNextPostfix(object __instance)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected || plugin._testYarnRunNextLogged >= 64) return;
            try
            {
                Type vmType = __instance.GetType();
                FieldInfo stateField = AccessTools.Field(vmType, "state");
                object state = stateField == null ? null : stateField.GetValue(__instance);
                FieldInfo nodeField = state == null ? null : AccessTools.Field(state.GetType(), "currentNodeName");
                FieldInfo pcField = state == null ? null : AccessTools.Field(state.GetType(), "programCounter");
                string currentNode = nodeField == null ? "" : nodeField.GetValue(state) as string ?? "";
                if (!string.Equals(currentNode, plugin._hostDialogueNode ?? "", StringComparison.Ordinal)) return;
                int pcValue = pcField == null ? -1 : Convert.ToInt32(pcField.GetValue(state), CultureInfo.InvariantCulture);
                string pc = pcValue < 0 ? "unknown" : pcValue.ToString(CultureInfo.InvariantCulture);
                string instruction = DescribeYarnInstruction(__instance, pcValue - 1);
                plugin._testYarnRunNextLogged++;
                plugin.Logger.LogInfo("Guest Yarn VM RunNext: node=" + currentNode + " pc=" + pc +
                    " step=" + plugin._testYarnRunNextLogged + " executed=" + instruction);
            }
            catch (Exception e)
            {
                plugin.Logger.LogWarning("Guest Yarn VM RunNext diagnostic failed: " + e.GetType().Name);
            }
        }

        private static string DescribeYarnInstruction(object vm, int index)
        {
            try
            {
                if (index < 0) return "none";
                FieldInfo nodeField = AccessTools.Field(vm.GetType(), "currentNode");
                object node = nodeField == null ? null : nodeField.GetValue(vm);
                FieldInfo instructionsField = node == null ? null : AccessTools.Field(node.GetType(), "instructions");
                IList instructions = instructionsField == null ? null : instructionsField.GetValue(node) as IList;
                if (instructions == null || index >= instructions.Count) return "index=" + index + "/unavailable";
                object instruction = instructions[index];
                FieldInfo operationField = AccessTools.Field(instruction.GetType(), "operation");
                FieldInfo operandAField = AccessTools.Field(instruction.GetType(), "operandA");
                FieldInfo operandBField = AccessTools.Field(instruction.GetType(), "operandB");
                object operation = operationField == null ? null : operationField.GetValue(instruction);
                object operandA = operandAField == null ? null : operandAField.GetValue(instruction);
                object operandB = operandBField == null ? null : operandBField.GetValue(instruction);
                return index.ToString(CultureInfo.InvariantCulture) + ":" + (operation ?? "?") +
                    "(" + (operandA ?? "null") + "," + (operandB ?? "null") + ")";
            }
            catch (Exception e) { return "decode-error:" + e.GetType().Name; }
        }

        private static void DialogueLineGatePostfix(object __instance, object __0, ref System.Collections.IEnumerator __result)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected || __result == null || __0 == null) return;
            try
            {
                FieldInfo textField = AccessTools.Field(__0.GetType(), "text");
                string lineText = textField == null ? null : textField.GetValue(__0) as string;
                if (string.IsNullOrEmpty(lineText)) return;
                string stream = DialogueUiStreamKey(__instance);
                if (p.IsHost && p._hostDialogueRunId != 0 && !string.IsNullOrEmpty(p._hostDialogueRunStream) &&
                    string.Equals(DialogueStreamPath(p._hostDialogueRunStream), DialogueStreamPath(stream), StringComparison.Ordinal))
                {
                    string resolvedNode = ReadDialogueNode(global::Global.dialogue);
                    if (!string.IsNullOrEmpty(resolvedNode))
                    {
                        p._hostDialogueWireNode = resolvedNode;
                        p._hostDialogueWireNodeReady = true;
                    }
                }
                if (p.IsHost)
                {
                    unchecked { p._hostDialogueLineSequence++; }
                    if (p._hostDialogueLineSequence == 0) p._hostDialogueLineSequence = 1;
                    string fingerprint = DialogueLineFingerprint(lineText);
                    p._hostDialogueLineFingerprint = fingerprint;
                    uint sequence = p._hostDialogueLineSequence;
                    if (string.Equals(stream, "DialogueUI@Global", StringComparison.Ordinal))
                        p._hostGlobalDialogueLineSequence = sequence;
                    Queue<DialogueLineEvent> streamQueue;
                    if (!p._pendingHostDialogueLines.TryGetValue(stream, out streamQueue))
                    {
                        streamQueue = new Queue<DialogueLineEvent>();
                        p._pendingHostDialogueLines.Add(stream, streamQueue);
                    }
                    string networkLineText = (IsTvDialogueLineStream(stream) ||
                        string.Equals(stream, "DialogueUI@Global", StringComparison.Ordinal)) ? lineText : "";
                    if (!IsTvDialogueLineStream(stream) && Encoding.UTF8.GetByteCount(networkLineText) > MaxDialogueLineTextBytes)
                    {
                        networkLineText = "";
                        p.Logger.LogWarning("Host dialogue line exceeded the co-op text limit; guest line override is unavailable for sequence " + sequence);
                    }
                    streamQueue.Enqueue(new DialogueLineEvent(stream, sequence, fingerprint, networkLineText));
                    p._testDialogueLineStatus = "host authoritative stream=" + stream + " sequence=" + sequence + " fingerprint=" + fingerprint;
                    __result = p.HostDialogueLineCoroutine(__result, stream, sequence);
                }
                else
                {
                    string fingerprint = DialogueLineFingerprint(lineText);
                    if (IsTvDialogueLineStream(stream))
                    {
                        p._pendingGuestTvLineStream = stream;
                        p._pendingGuestTvLineFingerprint = fingerprint;
                        p._pendingGuestTvLineText = lineText;
                        p._testDialogueLineStatus = "guest-local TV line running stream=" + stream +
                            " fingerprint=" + fingerprint;
                        DialogueLineEvent hostLine;
                        if (p._hostDialogueLineEvents.TryGetValue(stream, out hostLine) &&
                            string.Equals(hostLine.Fingerprint, fingerprint, StringComparison.Ordinal))
                            p.AcknowledgeGuestTvLine(hostLine);
                        p.Logger.LogInfo("Released guest TV line to its local UI: " + stream + "/" + fingerprint);

                    }
                    else __result = p.ClientDialogueLineCoroutine(__result, __0, lineText, stream);
                }
            }
            catch (Exception e)
            {
                p.Logger.LogWarning("Dialogue line gate setup failed: " + e.GetType().Name);
            }
        }

        private System.Collections.IEnumerator HostDialogueLineCoroutine(System.Collections.IEnumerator original, string stream, uint sequence)
        {
            while (original.MoveNext()) yield return original.Current;
            if (!_connected) yield break;
            while (_connected && !DialogueLineAcked(stream, sequence)) yield return null;
        }

        private System.Collections.IEnumerator ClientDialogueLineCoroutine(System.Collections.IEnumerator original, object line, string lineText, string stream)
        {
            string expectedFingerprint = DialogueLineFingerprint(lineText);
            _pendingGuestDialogueLineFingerprint = expectedFingerprint;
            _testDialogueLineStatus = "waiting for host stream=" + stream + " fingerprint=" + expectedFingerprint;
            DialogueLineEvent hostEvent = null;
            bool hostTextOverride = false;
            int waitDiagnostics = 0;
            while (_connected)
            {
                if (TryClaimMatchingDialogueLine(stream, expectedFingerprint, out hostEvent)) break;

                DialogueLineEvent nextHostEvent;
                if (TryGetNextDialogueLine(stream, out nextHostEvent) &&
                    !string.IsNullOrEmpty(nextHostEvent.Text) &&
                    !string.Equals(nextHostEvent.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
                {
                    if (TrySetDialogueLineText(line, nextHostEvent.Text))
                    {
                        ClaimDialogueLine(nextHostEvent);
                        hostEvent = nextHostEvent;
                        hostTextOverride = true;
                        _pendingGuestDialogueLineFingerprint = nextHostEvent.Fingerprint;
                        _testDialogueLineStatus = "host text override stream=" + stream +
                            " sequence=" + nextHostEvent.Sequence + " local=" + expectedFingerprint +
                            " host=" + nextHostEvent.Fingerprint;
                        Logger.LogWarning("Guest local dialogue fingerprint differed; applying host text for " +
                            stream + "#" + nextHostEvent.Sequence + " local=" + expectedFingerprint +
                            " host=" + nextHostEvent.Fingerprint);
                        break;
                    }
                    _testDialogueLineStatus = "host line mismatch but local text could not be replaced stream=" +
                        stream + " sequence=" + nextHostEvent.Sequence + " local=" + expectedFingerprint +
                        " host=" + nextHostEvent.Fingerprint;
                    Logger.LogWarning("Could not replace guest Yarn line text for host event " +
                        stream + "#" + nextHostEvent.Sequence);
                }

                if ((waitDiagnostics++ % 120) == 0)
                    Logger.LogInfo("Guest dialogue line gate waiting: expectedStream=" + stream +
                        " fingerprint=" + expectedFingerprint + " hostEvents=" + DescribeHostDialogueLineEvents());
                yield return null;
            }
            if (!_connected)
            {
                Logger.LogInfo("Dialogue line gate released after host disconnect: " + lineText);
                while (original.MoveNext()) yield return original.Current;
                if (string.Equals(_pendingGuestDialogueLineFingerprint, expectedFingerprint, StringComparison.Ordinal))
                    _pendingGuestDialogueLineFingerprint = "";
                yield break;
            }
            uint sequence = hostEvent.Sequence;
            _lastAppliedHostDialogueLineSequence = sequence;
            _lastAppliedHostDialogueLineSequences[hostEvent.Stream] = sequence;
            _clientDialogueLineAcks[hostEvent.Stream] = sequence;
            _clientDialogueLineAck = sequence;
            Logger.LogInfo((hostTextOverride ? "Acknowledged host text override before guest UI completion: " :
                "Acknowledged matching host dialogue event before guest UI completion: ") +
                hostEvent.Stream + "#" + sequence + "/" + hostEvent.Fingerprint);
            while (original.MoveNext()) yield return original.Current;
            if (string.Equals(_pendingGuestDialogueLineFingerprint, hostEvent.Fingerprint, StringComparison.Ordinal))
                _pendingGuestDialogueLineFingerprint = "";
        }
        private static bool IsTvDialogueLineStream(string stream)
        {
            return !string.IsNullOrEmpty(stream) &&
                stream.EndsWith("/TintSprites/TV", StringComparison.OrdinalIgnoreCase);
        }
        private void AcknowledgeGuestTvLine(DialogueLineEvent lineEvent)
        {
            if (lineEvent == null) return;
            _lastAppliedHostDialogueLineSequence = lineEvent.Sequence;
            _lastAppliedHostDialogueLineSequences[lineEvent.Stream] = lineEvent.Sequence;
            _claimedHostDialogueLineSequences[lineEvent.Stream] = lineEvent.Sequence;
            _clientDialogueLineAcks[lineEvent.Stream] = lineEvent.Sequence;
            _clientDialogueLineAck = lineEvent.Sequence;
            string captionText = _pendingGuestTvLineText ?? "";
            if (captionText.StartsWith("TV:", StringComparison.OrdinalIgnoreCase))
                captionText = captionText.Substring(3).TrimStart();
            _guestTvOverlayText = captionText;
            _pendingGuestTvLineStream = "";
            _pendingGuestTvLineFingerprint = "";
            _pendingGuestTvLineText = "";
            _testDialogueLineStatus = "guest TV line released and acknowledged stream=" +
                lineEvent.Stream + " sequence=" + lineEvent.Sequence + " fingerprint=" + lineEvent.Fingerprint;
            Logger.LogInfo("Acknowledged matching guest-local TV line event: " +
                lineEvent.Stream + "#" + lineEvent.Sequence + "/" + lineEvent.Fingerprint);
        }
        private static string DialogueLineFingerprint(string lineText)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < lineText.Length; i++)
                    hash = (hash ^ lineText[i]) * 16777619;
                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static bool IsSequenceAtLeast(uint value, uint target)
        {
            return value == target || IsNewer(value, target);
        }

        private static object FindDialogueReference(object iterator, object implementation)
        {
            object found = FindDialogueInObject(iterator);
            if (found != null) return found;
            return FindDialogueInObject(implementation);
        }

        private static object FindDialogueInObject(object owner)
        {
            if (owner == null) return null;
            if (IsDialogueObject(owner)) return owner;
            try
            {
                FieldInfo[] fields = owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; i++)
                {
                    object value = fields[i].GetValue(owner);
                    if (value == null) continue;
                    if (IsDialogueObject(value)) return value;
                    FieldInfo[] nestedFields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    for (int j = 0; j < nestedFields.Length; j++)
                    {
                        object nested = nestedFields[j].GetValue(value);
                        if (nested != null && IsDialogueObject(nested)) return nested;
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsDialogueObject(object value)
        {
            return value != null && value.GetType().Name == "Dialogue";
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
                plugin._testOptionImplementation = implementation;
                FieldInfo activeField = AccessTools.Field(implementation.GetType(), "runningOptions");
                if (activeField == null) return;
                bool active = (bool)activeField.GetValue(implementation);
                FieldInfo indexField = AccessTools.Field(implementation.GetType(), "currentOption");
                FieldInfo inputConfirmField = AccessTools.Field(implementation.GetType(), "inputConfirm");
                FieldInfo typeField = AccessTools.Field(implementation.GetType(), "type");
                FieldInfo programCounterField = AccessTools.Field(__instance.GetType(), "$PC");
                FieldInfo timerField = AccessTools.Field(__instance.GetType(), "<timer>__1");
                FieldInfo timeToLerpField = AccessTools.Field(__instance.GetType(), "<TimeToLerp>__1");
                FieldInfo doneField = AccessTools.Field(__instance.GetType(), "<done>__1");
                FieldInfo optionsField = AccessTools.Field(__instance.GetType(), "options");
                System.Collections.IList options = optionsField == null ? null : optionsField.GetValue(__instance) as System.Collections.IList;
                string optionSummary = "";
                if (options != null)
                {
                    for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
                    {
                        if (optionIndex > 0) optionSummary += " || ";
                        string optionText = options[optionIndex] as string ?? "<null>";
                        optionSummary += optionIndex + ":" + (optionText.Length > 80 ? optionText.Substring(0, 80) : optionText);
                    }
                }
                FieldInfo callbackField = null;
                FieldInfo uiField = AccessTools.Field(__instance.GetType(), "dialogueUI");
                object ui = uiField == null ? null : uiField.GetValue(__instance);
                if (ui != null) callbackField = AccessTools.Field(ui.GetType(), "SetSelectedOption");
                object dialogue = FindDialogueReference(__instance, implementation);
                plugin._testOptionDialogue = dialogue;
                string node = ReadDialogueNode(dialogue);
                int currentIndex = indexField == null ? 0 : (int)indexField.GetValue(implementation);
                object inputConfirm = inputConfirmField == null ? null : inputConfirmField.GetValue(implementation);
                object implementationType = typeField == null ? null : typeField.GetValue(implementation);
                object programCounter = programCounterField == null ? null : programCounterField.GetValue(__instance);
                object timer = timerField == null ? null : timerField.GetValue(__instance);
                object timeToLerp = timeToLerpField == null ? null : timeToLerpField.GetValue(__instance);
                object done = doneField == null ? null : doneField.GetValue(__instance);
                float initTimerValue = 0f;
                bool hasInitTimer = false;
                FieldInfo[] iteratorFields = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int fieldIndex = 0; fieldIndex < iteratorFields.Length; fieldIndex++)
                {
                    if (!iteratorFields[fieldIndex].Name.StartsWith("<initTimer>__", StringComparison.Ordinal)) continue;
                    try
                    {
                        initTimerValue = Convert.ToSingle(iteratorFields[fieldIndex].GetValue(__instance), CultureInfo.InvariantCulture);
                        hasInitTimer = true;
                    }
                    catch { }
                    break;
                }
                string optionTypeName = Convert.ToString(implementationType, CultureInfo.InvariantCulture);
                int optionBubbleState = -1;
                bool bubbleReady = true;
                if (string.Equals(optionTypeName, "SpeechBubbles", StringComparison.Ordinal))
                {
                    FieldInfo bubbleField = AccessTools.Field(implementation.GetType(), "speechBubble");
                    object bubble = bubbleField == null ? null : bubbleField.GetValue(implementation);
                    FieldInfo bubbleStateField = bubble == null ? null : AccessTools.Field(bubble.GetType(), "state");
                    if (bubbleStateField != null)
                    {
                        try
                        {
                            optionBubbleState = Convert.ToInt32(bubbleStateField.GetValue(bubble), CultureInfo.InvariantCulture);
                            bubbleReady = optionBubbleState == 2;
                        }
                        catch { }
                    }
                }
                bool optionTimerReady = !hasInitTimer || initTimerValue <= 0.0001f;
                plugin._optionConfirmReady = active && optionTimerReady && bubbleReady;
                if (!active) plugin._optionConfirmReadyStatus = "no active option";
                else if (!optionTimerReady) plugin._optionConfirmReadyStatus = "waiting initTimer=" + initTimerValue.ToString("F3", CultureInfo.InvariantCulture);
                else if (!bubbleReady) plugin._optionConfirmReadyStatus = "waiting bubbleState=" + optionBubbleState;
                else plugin._optionConfirmReadyStatus = "ready type=" + optionTypeName + " initTimer=" +
                    (hasInitTimer ? initTimerValue.ToString("F3", CultureInfo.InvariantCulture) : "unknown") + " bubbleState=" + optionBubbleState;
                unchecked { plugin._testOptionIteratorSteps++; }
                plugin._testOptionIteratorFrame = Time.frameCount;
                plugin._testOptionIteratorStatus = "running=" + active + " index=" + currentIndex +
                    " options=" + (options == null ? "?" : options.Count.ToString()) +
                    " node=" + node + " callback=" + (callbackField == null ? "?" : (callbackField.GetValue(ui) != null).ToString());
                if (options != null) plugin._testOptionIteratorStatus += " labels=[" + optionSummary + "]";
                plugin._testOptionIteratorStatus += " type=" + (implementationType == null ? "?" : implementationType.ToString()) +
                    " pc=" + (programCounter == null ? "?" : programCounter.ToString()) +
                    " inputConfirm=" + (inputConfirm == null ? "?" : inputConfirm.ToString()) +
                    " timer=" + (timer == null ? "?" : timer.ToString()) +
                    " lerp=" + (timeToLerp == null ? "?" : timeToLerp.ToString()) +
                    " done=" + (done == null ? "?" : done.ToString()) +
                    " dt=" + Time.deltaTime.ToString("F3", CultureInfo.InvariantCulture) +
                    " scale=" + Time.timeScale.ToString("F2", CultureInfo.InvariantCulture);
                plugin._testOptionIteratorStatus += " ready=" + plugin._optionConfirmReadyStatus;
                plugin._testOptionIteratorStatus += " runner=" + DialogueRunnerStatus(dialogue);
                if (plugin.IsHost)
                {
                    plugin._hostOptionCount = options == null ? 0 : options.Count;
                    plugin._hostOptionLabels = CopyOptionLabels(options);
                    if (active && indexField != null && options != null &&
                        plugin._pendingGuestOptionSessionId == plugin._hostOptionSessionId &&
                        plugin._pendingGuestOptionIndex >= 0 &&
                        plugin._pendingGuestOptionIndex < options.Count &&
                        SameChoiceNode(node, plugin._hostOptionNode))
                    {
                        plugin._hostOptionIndex = plugin._pendingGuestOptionIndex;
                        indexField.SetValue(implementation, plugin._pendingGuestOptionIndex);
                        plugin._pendingGuestOptionIndex = -1;
                        plugin._pendingGuestOptionSessionId = 0;
                    }
                    plugin._hostOptionText = OptionLabelAt(plugin._hostOptionLabels, plugin._hostOptionIndex);
                    return;
                }
                if (!plugin.IsClient) return;
                string localNode = node;
                if (active)
                {
                    plugin._pendingHostConfirm = false;
                    plugin._clientActiveOptionSessionId = plugin._hostOptionSessionId;
                    plugin._clientActiveOptionNode = localNode;
                    plugin._clientActiveOptionCount = options == null ? 0 : options.Count;
                    plugin._clientActiveOptionLabels = CopyOptionLabels(options);
                }
                // The host's confirm level is also consumed by non-Yarn story
                // coroutines. Do not let that level confirm a guest Yarn option
                // before the corresponding authoritative choice commit arrives.
                // SetOption must run synchronously while Yarn's currentOptions
                // list is live; replaying it later invokes a stale VM callback.
                // A committed choice closes the host menu before this guest
                // iterator resumes, so host option-session metadata may already
                // have advanced or been cleared. Match against the commit itself
                // and the live guest node/options instead; the commit id guards
                // against replay and the node plus exact label keep it scoped to
                // the corresponding Yarn choice.
                bool matchingUnappliedCommit = plugin._hostChoiceCommitId != 0 &&
                    plugin._hostChoiceCommitId != plugin._lastAppliedChoiceCommitId &&
                    SameChoiceNode(localNode, plugin._hostChoiceCommitNode) &&
                    FindOptionLabel(options, plugin._hostChoiceCommitText) >= 0;
                if (inputConfirmField != null && !matchingUnappliedCommit)
                    inputConfirmField.SetValue(implementation, false);
                if (matchingUnappliedCommit && indexField != null)
                {
                    int committedIndex = FindOptionLabel(options, plugin._hostChoiceCommitText);
                    if (committedIndex >= 0)
                    {
                        plugin._clientActiveOptionSessionId = plugin._hostChoiceCommitSessionId;
                        plugin._clientActiveOptionNode = localNode;
                        plugin._clientActiveOptionCount = options == null ? 0 : options.Count;
                        plugin._clientActiveOptionLabels = CopyOptionLabels(options);
                        indexField.SetValue(implementation, committedIndex);
                        if (plugin._hasDeferredChoiceCallback)
                        {
                            // Run the deferred callback while Yarn's options list is live.
                            plugin.CompleteDeferredChoiceCallback(committedIndex, plugin._hostChoiceCommitId);
                        }
                        else plugin._pendingClientChoiceConfirm = true;
                    }
                }
                if (!plugin._hostOptionsActive) return;
                if (!SameChoiceSession(plugin._hostOptionSessionId, plugin._hostOptionSessionId,
                    plugin._hostOptionNode, localNode)) return;
                FieldInfo nextField = AccessTools.Field(implementation.GetType(), "inputArrowNext");
                FieldInfo previousField = AccessTools.Field(implementation.GetType(), "inputArrowPrev");
                if (indexField == null || !active) return;
                plugin._clientActiveOptionSessionId = plugin._hostOptionSessionId;
                plugin._clientActiveOptionNode = localNode;
                plugin._clientActiveOptionCount = options == null ? 0 : options.Count;
                bool nextPressed = nextField != null && (bool)nextField.GetValue(implementation);
                bool previousPressed = previousField != null && (bool)previousField.GetValue(implementation);
                if (plugin._clientChoiceAxisSessionId != plugin._hostOptionSessionId)
                {
                    plugin._clientChoiceAxisSessionId = plugin._hostOptionSessionId;
                    plugin._clientChoicePreviousHorizontal = 0f;
                }
                float horizontal = 0f;
                try
                {
                    if (global::Global.input != null)
                        horizontal = global::Global.input.GetAxisRaw("Horizontal");
                }
                catch { }
                if (plugin._testChoiceAxisFrames > 0)
                {
                    horizontal = plugin._testChoiceAxisDirection;
                    plugin._testChoiceAxisFrames--;
                }
                int axisDirection = horizontal > 0.5f && plugin._clientChoicePreviousHorizontal <= 0.5f ? 1 :
                    horizontal < -0.5f && plugin._clientChoicePreviousHorizontal >= -0.5f ? -1 : 0;
                plugin._clientChoicePreviousHorizontal = horizontal;
                int direction = nextPressed == previousPressed ? axisDirection : (nextPressed ? 1 : -1);
                if (direction != 0 && options != null && options.Count > 0)
                {
                    int currentLocalIndex = (int)indexField.GetValue(implementation);
                    if (currentLocalIndex >= 0 && currentLocalIndex < options.Count)
                    {
                        int requestedIndex = (currentLocalIndex + direction + options.Count) % options.Count;
                        plugin._clientChoiceSelectionRequestIndex = requestedIndex;
                        plugin._clientChoiceSelectionRequestSessionId = plugin._hostOptionSessionId;
                    }
                }
                int index = Math.Max(0, plugin._hostOptionIndex);
                int semanticIndex = FindOptionLabel(options, plugin._hostOptionText);
                if (semanticIndex >= 0) index = semanticIndex;
                else if (!string.IsNullOrEmpty(plugin._hostOptionText))
                {
                    plugin._testOptionIteratorStatus += " host-label-not-found";
                    return;
                }
                else if (options != null && options.Count > 0) index = Math.Min(index, options.Count - 1);
                bool waitingForGuestSelection = plugin._clientChoiceSelectionRequestIndex >= 0 &&
                    plugin._clientChoiceSelectionRequestSessionId == plugin._hostOptionSessionId;
                indexField.SetValue(implementation, waitingForGuestSelection
                    ? plugin._clientChoiceSelectionRequestIndex : index);
                if (nextField != null) nextField.SetValue(implementation, false);
                if (previousField != null) previousField.SetValue(implementation, false);
            }
            catch { }
        }

        private static void DialogueOptionStepPostfix(object __instance, bool __result)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsHost) return;
            try
            {
                FieldInfo ownerField = AccessTools.Field(__instance.GetType(), "$this");
                object implementation = ownerField == null ? null : ownerField.GetValue(__instance);
                if (implementation == null) return;
                FieldInfo activeField = AccessTools.Field(implementation.GetType(), "runningOptions");
                FieldInfo indexField = AccessTools.Field(implementation.GetType(), "currentOption");
                bool active = __result && activeField != null && (bool)activeField.GetValue(implementation);
                plugin.ObserveHostOptionActive(active);
                plugin._hostOptionsActive = active;
                if (active)
                {
                    if (indexField != null) plugin._hostOptionIndex = (int)indexField.GetValue(implementation);
                    plugin._hostOptionNode = ReadDialogueNode(FindDialogueReference(__instance, implementation));
                }
                else
                {
                    plugin._dialogueConfirmPending = false;
                    plugin._optionConfirmReady = false;
                    plugin._optionConfirmReadyStatus = "no active option";
                    bool committedThisSession = plugin._hostChoiceCommitId != 0 &&
                        plugin._hostChoiceCommitSessionId == plugin._hostOptionSessionId &&
                        SameChoiceNode(plugin._hostChoiceCommitNode, plugin._hostOptionNode);
                    if (!committedThisSession)
                    {
                        plugin._hostOptionIndex = 0;
                        plugin._hostOptionNode = "";
                    }
                }
            }
            catch { }
        }

        private static void GuestInteractionScopePrefix(object __instance)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected) return;
            plugin._capturingGuestInteractionIterator = __instance;
            plugin._capturingGuestPlayerInteraction = true;
        }

        private static void GuestInteractionScopePostfix(object __instance, bool __result)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || __result || __instance == null) return;
            if (plugin._guestInteractionCaptureConsumed.ContainsKey(__instance))
            {
                plugin._guestInteractionCaptureConsumed.Remove(__instance);
                plugin.Logger.LogInfo("Guest interaction iterator finished; fresh Interact input is still required to re-arm capture");
            }
        }

        private static Exception GuestInteractionScopeFinalizer(object __instance, Exception __exception)
        {
            CoopPlugin plugin = Instance;
            if (plugin != null)
            {
                plugin._capturingGuestPlayerInteraction = false;
                if (object.ReferenceEquals(plugin._capturingGuestInteractionIterator, __instance))
                    plugin._capturingGuestInteractionIterator = null;
                if (__exception != null && __instance != null)
                    plugin._guestInteractionCaptureConsumed.Remove(__instance);
            }
            return __exception;
        }

        private void QueueGuestInteraction(TextAsset asset, string node)
        {
            object iterator = _capturingGuestInteractionIterator;
            if (iterator != null && _guestInteractionCaptureConsumed.ContainsKey(iterator))
            {
                _guestInteractionStatus = "suppressed duplicate run within guest interaction iterator asset=" +
                    (asset == null ? "<null>" : asset.name) + " node=" + (node ?? "");
                Logger.LogInfo("Suppressed repeated guest Yarn run from the same interaction iterator: " +
                    _guestInteractionStatus);
                return;
            }
            if (!_guestInteractionCaptureArmed)
            {
                _guestInteractionStatus = "suppressed repeated run until fresh guest Interact input asset=" +
                    (asset == null ? "<null>" : asset.name) + " node=" + (node ?? "");
                Logger.LogInfo("Suppressed guest Yarn run until a fresh Interact input: " + _guestInteractionStatus);
                if (!HasRunningDialogue()) SchedulePlayerInteractionCleanup();
                return;
            }
            if (iterator != null) _guestInteractionCaptureConsumed.Add(iterator, true);
            _guestInteractionCaptureArmed = false;
            if (asset == null || string.IsNullOrEmpty(node))
            {
                _guestInteractionStatus = "guest interaction missing asset or node; suppressed";
                return;
            }
            unchecked { _guestInteractionRequestSequence++; }
            if (_guestInteractionRequestSequence == 0) _guestInteractionRequestSequence = 1;
            _pendingClientInteractionRequestId = _guestInteractionRequestSequence;
            _pendingClientInteractionDeadline = Time.realtimeSinceStartup + 5f;
            _pendingClientInteractionAsset = asset.name;
            _pendingClientInteractionNode = node;
            _guestInteractionStatus = "queued request=" + _pendingClientInteractionRequestId + " asset=" +
                asset.name + " node=" + node;
            Logger.LogInfo("Queued guest Yarn interaction for host: request=" + _pendingClientInteractionRequestId +
                " asset=" + asset.name + " node=" + node);
        }

        private static bool IsGuestSceneBootstrapDialogue(CoopPlugin plugin, TextAsset asset, string node)
        {
            return plugin != null && asset != null &&
                IsSceneBootstrapDialogue(asset.name, node, plugin._hostSceneName);
        }

        private static bool IsSceneBootstrapDialogue(string assetName, string node, string scene)
        {
            if (string.IsNullOrEmpty(assetName) || string.IsNullOrEmpty(node)) return false;
            string assetScene = Path.GetFileNameWithoutExtension(assetName);
            if (string.IsNullOrEmpty(scene) || !string.Equals(assetScene, scene, StringComparison.Ordinal)) return false;
            return string.Equals(node, "PreInitLevel", StringComparison.Ordinal) ||
                string.Equals(node, "InitLevel", StringComparison.Ordinal) ||
                string.Equals(node, "EnterLevel", StringComparison.Ordinal) ||
                node.StartsWith("Init" + scene, StringComparison.Ordinal) ||
                node.StartsWith("Enter" + scene, StringComparison.Ordinal);
        }
        private static bool DialogueRunPrefix(global::Dialogue __instance, TextAsset __0, string __1, int __2)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null) return true;
            unchecked { plugin._testDialogueRunCalls++; }
            string assetName = __0 == null ? "<null>" : __0.name;
            string currentNode = ReadDialogueNode(__instance);
            string runStream = DialogueUiStreamKey(__instance);
            plugin._testDialogueRunStatus = "asset=" + assetName + " requestedNode=" + (__1 ?? "") +
                " currentNode=" + currentNode + " option=" + __2 + " stream=" + runStream;
            if (plugin.IsClient && plugin.Connected && plugin._capturingGuestPlayerInteraction &&
                !plugin._applyingHostDialogueReplay)
            {
                plugin.QueueGuestInteraction(__0, __1);
                return false;
            }
            if (plugin.IsClient && plugin.Connected && plugin._hostDialogueRunId != 0)
            {
                bool matchesHost = string.Equals(assetName, plugin._hostDialogueAsset, StringComparison.Ordinal) &&
                    string.Equals(__1 ?? "", plugin._hostDialogueNode ?? "", StringComparison.Ordinal) &&
                    string.Equals(runStream, plugin._hostDialogueRunStream ?? "", StringComparison.Ordinal) &&
                    Math.Max(0, __2) == plugin._hostDialogueOption;
                if (!plugin._applyingHostDialogueReplay || !matchesHost)
                {
                    bool hostRunEnded = plugin._lastAppliedHostDialogueRunId == plugin._hostDialogueRunId &&
                        string.IsNullOrEmpty(plugin._hostDialogueAsset) &&
                        string.IsNullOrEmpty(plugin._hostDialogueNode);
                    bool matchingSceneBootstrap = IsGuestSceneBootstrapDialogue(plugin, __0, __1);
                    bool bootstrapAlreadyHosted = plugin._hostSceneBootstrapNodes.ContainsKey(assetName + "/" + (__1 ?? ""));
                    bool hostSceneBootstrapNodePending = string.IsNullOrEmpty(plugin._hostDialogueNode) &&
                        string.Equals(plugin._hostDialogueAsset, assetName, StringComparison.Ordinal);
                    if (matchingSceneBootstrap && (hostSceneBootstrapNodePending || (hostRunEnded && !bootstrapAlreadyHosted)))
                    {
                        string bootstrapReason = hostSceneBootstrapNodePending ? "while matching host scene node is pending" : "not yet sent by host";
                        plugin.Logger.LogInfo("Allowing guest scene-bootstrap Dialogue.Run " + bootstrapReason + ": " +
                            assetName + "/" + (__1 ?? "") + " scene=" + Application.loadedLevelName);
                    }
                    else
                    {
                        plugin._testDialogueRunStatus += " suppressed=host-authoritative";
                        plugin.Logger.LogInfo("Suppressed guest-local Dialogue.Run while connected: requested=" +
                            assetName + "/" + (__1 ?? "") + "/" + Math.Max(0, __2) + " host=" +
                            plugin._hostDialogueAsset + "/" + plugin._hostDialogueNode + "/" + plugin._hostDialogueOption);

                        return false;
                    }
                }
            }
            if (plugin.IsHost && plugin.Connected)
                plugin._hostDialogueStartedByMirroredGuestInteract =
                    Time.realtimeSinceStartup <= plugin._hostRemoteInteractInjectedUntil;
            else if (plugin.IsHost)
                plugin._hostDialogueStartedByMirroredGuestInteract = false;
            if (plugin.IsHost && __0 != null && !string.IsNullOrEmpty(__1))
            {
                unchecked { plugin._hostDialogueRunId++; }
                if (plugin._hostDialogueRunId == 0) plugin._hostDialogueRunId = 1;
                plugin._hostDialogueAsset = assetName;
                plugin._hostDialogueNode = __1;
                // Wait for the first displayed line to resolve Yarn's canonical node
                // (for example Creek -> Creek.1) before starting the guest runner.
                // If the host finishes without displaying a line, completion publishes
                // the requested node as a fallback and holds the run open long enough
                // for that start packet to reach the guest.
                plugin._hostDialogueWireNode = "";
                plugin._hostDialogueWireNodeReady = false;
                plugin._hostDialogueFallbackHoldUntil = 0f;
                plugin._nextSend = 0f;
                plugin._hostDialogueRunStartFrame = Time.frameCount;
                plugin._hostDialogueEndSentForRunId = 0;
                plugin._hostDialogueRunStream = runStream;
                plugin._hostDialogueOption = Math.Max(0, __2);
            }
            plugin.Logger.LogInfo("Dialogue.Run observed frame=" + Time.frameCount + " time=" +
                Time.realtimeSinceStartup.ToString("F3", CultureInfo.InvariantCulture) + ": " + plugin._testDialogueRunStatus);
            return true;
        }

        private static void DialogueLinePrefix(object __instance, object __0)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || __0 == null) return;
            try
            {
                FieldInfo textField = AccessTools.Field(__0.GetType(), "text");
                string text = textField == null ? "<line text unavailable>" : textField.GetValue(__0) as string;
                if (text == null) text = "<null>";
                text = text.Replace('\r', ' ').Replace('\n', ' ');
                if (text.Length > 180) text = text.Substring(0, 180);
                string status = "ui=" + DialogueUiIdentity(__instance) + " node=" + ReadDialogueNode(global::Global.dialogue) + " text=" + text;
                unchecked { plugin._testDialogueLineCalls++; }
                plugin._testDialogueLineStatus = status;
                if (!string.Equals(status, plugin._testLastDialogueLineLogged, StringComparison.Ordinal))
                {
                    plugin._testLastDialogueLineLogged = status;
                    plugin.Logger.LogInfo("Dialogue line observed: " + status);
                }
            }
            catch (Exception e)
            {
                plugin._testDialogueLineStatus = "line diagnostic error: " + e.GetType().Name;
            }
        }

        private static string DialogueUiIdentity(object instance)
        {
            try
            {
                Component component = instance as Component;
                if (component == null) return instance == null ? "null" : instance.GetType().FullName;
                return component.GetType().Name + "#" + component.GetInstanceID() + "@" + DialogueUiPath(component);
            }
            catch (Exception e)
            {
                return "identity-error:" + e.GetType().Name;
            }
        }

        private static string DialogueUiStreamKey(object instance)
        {
            Component component = instance as Component;
            if (component == null) return instance == null ? "unknown" : instance.GetType().FullName;
            return component.GetType().Name + "@" + DialogueUiPath(component);
        }

        private static string DialogueUiPath(Component component)
        {
            string path = component.name;
            Transform parent = component.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private bool DialogueLineAcked(string stream, uint sequence)
        {
            uint ack;
            return _clientDialogueLineAcks.TryGetValue(stream, out ack) && IsSequenceAtLeast(ack, sequence);
        }

        private bool TryClaimMatchingDialogueLine(string stream, string fingerprint, out DialogueLineEvent lineEvent)
        {
            lineEvent = null;
            DialogueLineEvent candidate;
            if (!_hostDialogueLineEvents.TryGetValue(stream, out candidate) ||
                !string.Equals(candidate.Fingerprint, fingerprint, StringComparison.Ordinal) ||
                !IsDialogueLineAvailable(candidate)) return false;
            lineEvent = candidate;
            ClaimDialogueLine(lineEvent);
            return true;
        }

        private bool TryGetNextDialogueLine(string stream, out DialogueLineEvent lineEvent)
        {
            lineEvent = null;
            DialogueLineEvent candidate;
            if (!_hostDialogueLineEvents.TryGetValue(stream, out candidate) || !IsDialogueLineAvailable(candidate))
                return false;
            lineEvent = candidate;
            return true;
        }

        private bool IsDialogueLineAvailable(DialogueLineEvent lineEvent)
        {
            if (lineEvent == null) return false;
            uint applied;
            if (_lastAppliedHostDialogueLineSequences.TryGetValue(lineEvent.Stream, out applied) &&
                !IsNewer(lineEvent.Sequence, applied)) return false;
            uint claimed;
            return !_claimedHostDialogueLineSequences.TryGetValue(lineEvent.Stream, out claimed) ||
                claimed != lineEvent.Sequence;
        }

        private void ClaimDialogueLine(DialogueLineEvent lineEvent)
        {
            if (lineEvent != null) _claimedHostDialogueLineSequences[lineEvent.Stream] = lineEvent.Sequence;
        }

        private static bool TrySetDialogueLineText(object line, string text)
        {
            if (line == null || string.IsNullOrEmpty(text)) return false;
            try
            {
                FieldInfo textField = AccessTools.Field(line.GetType(), "text");
                if (textField == null || textField.IsInitOnly) return false;
                textField.SetValue(line, text);
                return true;
            }
            catch { return false; }
        }

        private string DescribeHostDialogueLineEvents()
        {
            List<string> keys = new List<string>(_hostDialogueLineEvents.Keys);
            keys.Sort(StringComparer.Ordinal);
            List<string> descriptions = new List<string>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                DialogueLineEvent candidate = _hostDialogueLineEvents[keys[i]];
                uint applied;
                uint claimed;
                descriptions.Add(candidate.Stream + "#" + candidate.Sequence + "/" + candidate.Fingerprint +
                    " applied=" + (_lastAppliedHostDialogueLineSequences.TryGetValue(candidate.Stream, out applied) ? applied.ToString(CultureInfo.InvariantCulture) : "none") +
                    " claimed=" + (_claimedHostDialogueLineSequences.TryGetValue(candidate.Stream, out claimed) ? claimed.ToString(CultureInfo.InvariantCulture) : "none"));
            }
            return descriptions.Count == 0 ? "<none>" : string.Join(";", descriptions.ToArray());
        }
        private List<DialogueLineEvent> PendingDialogueLineHeads()
        {
            List<string> keys = new List<string>(_pendingHostDialogueLines.Keys);
            keys.Sort(StringComparer.Ordinal);
            List<DialogueLineEvent> result = new List<DialogueLineEvent>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                Queue<DialogueLineEvent> queue = _pendingHostDialogueLines[keys[i]];
                if (queue.Count > 0) result.Add(queue.Peek());
            }
            return result;
        }

        private int PendingDialogueLineCount()
        {
            int count = 0;
            foreach (Queue<DialogueLineEvent> queue in _pendingHostDialogueLines.Values) count += queue.Count;
            return count;
        }

        private static TextAsset CurrentSceneDialogueAsset()
        {
            try
            {
                FieldInfo currentField = AccessTools.Field(typeof(global::SceneRoot), "current");
                object current = currentField == null ? null : currentField.GetValue(null);
                FieldInfo dialogueField = current == null ? null : AccessTools.Field(current.GetType(), "dialogue");
                return dialogueField == null ? null : dialogueField.GetValue(current) as TextAsset;
            }
            catch { return null; }
        }

        private void ApplyPendingHostDialogue()
        {
            if (!IsClient || !_pendingHostDialogueStart || string.IsNullOrEmpty(_hostDialogueNode)) return;
            if (string.IsNullOrEmpty(_hostSceneName) ||
                !SameChoiceNode(Application.loadedLevelName, _hostSceneName)) return;
            _pendingHostDialogueStart = false;
            if (_lastAppliedHostDialogueRunId == _hostDialogueRunId) return;
            Dialogue target = FindDialogueByStream(_hostDialogueRunStream);
            if (target == null)
            {
                _lastAppliedHostDialogueRunId = _hostDialogueRunId;
                Logger.LogWarning("Host dialogue target not found for stream " + _hostDialogueRunStream +
                    "; dialogue start skipped rather than routed to the wrong UI");
                return;
            }
            string localNode = ReadDialogueNode(target);
            if (IsDialogueRunning(target) && SameChoiceNode(localNode, _hostDialogueNode))
            {
                _lastAppliedHostDialogueRunId = _hostDialogueRunId;
                Logger.LogInfo("Host dialogue already active on guest: " + _hostDialogueNode);
                return;
            }
            TextAsset asset = null;
            UnityEngine.Object[] assets = Resources.FindObjectsOfTypeAll(typeof(TextAsset));
            for (int i = 0; i < assets.Length; i++)
            {
                TextAsset candidate = assets[i] as TextAsset;
                if (candidate != null && string.Equals(candidate.name, _hostDialogueAsset, StringComparison.Ordinal))
                {
                    asset = candidate;
                    break;
                }
            }
            if (asset == null)
            {
                TextAsset currentAsset = CurrentSceneDialogueAsset();
                if (currentAsset != null && string.Equals(currentAsset.name, _hostDialogueAsset, StringComparison.Ordinal))
                    asset = currentAsset;
            }
            if (asset == null)
            {
                _lastAppliedHostDialogueRunId = _hostDialogueRunId;
                Logger.LogWarning("Host dialogue start asset not found: " + _hostDialogueAsset);
                return;
            }
            try
            {
                target.Stop();
                target.Clear();
                ResumeClientTransitionIfPaused();
                _applyingHostDialogueReplay = true;
                target.Run(asset, _hostDialogueNode, _hostDialogueOption);
                _lastAppliedHostDialogueRunId = _hostDialogueRunId;
                Logger.LogInfo("Applied host dialogue start on guest: " + _hostDialogueAsset + "/" +
                    _hostDialogueNode + " stream=" + DialogueUiStreamKey(target) +
                    " active=" + target.gameObject.activeInHierarchy);
                LogDialogueNodeAvailability(target, asset, _hostDialogueNode);
                LogDialogueUiBindings(target);
            }
            catch (Exception e)
            {
                _lastAppliedHostDialogueRunId = _hostDialogueRunId;
                Logger.LogWarning("Host dialogue start failed: " + e.GetType().Name);
            }
            finally { _applyingHostDialogueReplay = false; }
        }

        private static Dialogue FindDialogueByStream(string stream)
        {
            if (string.IsNullOrEmpty(stream)) return null;
            try
            {
                // The game's shared Dialogue component can live in Unity's
                // DontDestroyOnLoad scene. Resolve the canonical Global
                // dialogue by its exact stream key before restricting the
                // search to the currently loaded level.
                Dialogue globalDialogue = global::Global.dialogue;
                if (globalDialogue != null &&
                    string.Equals(DialogueUiStreamKey(globalDialogue), stream, StringComparison.Ordinal))
                    return globalDialogue;

                UnityEngine.Object[] dialogues = Resources.FindObjectsOfTypeAll(typeof(Dialogue));
                Dialogue inactiveMatch = null;
                for (int i = 0; i < dialogues.Length; i++)
                {
                    Dialogue dialogue = dialogues[i] as Dialogue;
                    Component component = dialogue as Component;
                    if (component == null || !component.gameObject.scene.IsValid() ||
                        !string.Equals(component.gameObject.scene.name, Application.loadedLevelName, StringComparison.Ordinal)) continue;
                    if (!string.Equals(DialogueUiStreamKey(dialogue), stream, StringComparison.Ordinal)) continue;
                    if (component.gameObject.activeInHierarchy) return dialogue;
                    if (inactiveMatch == null) inactiveMatch = dialogue;
                }
                return inactiveMatch;
            }
            catch { }
            return null;
        }

        private void LogDialogueUiBindings(Dialogue target)
        {
            try
            {
                Type uiType = typeof(global::Continuity).Assembly.GetType("Yarn.Unity.Example.DialogueUI", false);
                if (uiType == null) return;
                FieldInfo implField = AccessTools.Field(uiType, "dialogueImplementation");
                FieldInfo targetRunnerField = AccessTools.Field(target.GetType(), "dialogueRunner");
                object targetRunner = targetRunnerField == null ? null : targetRunnerField.GetValue(target);
                object targetImpl = target.GetComponent(typeof(global::DialogueImplementation));
                UnityEngine.Object[] uis = Resources.FindObjectsOfTypeAll(uiType);
                for (int i = 0; i < uis.Length; i++)
                {
                    Component ui = uis[i] as Component;
                    if (ui == null || !ui.gameObject.scene.IsValid()) continue;
                    object impl = implField == null ? null : implField.GetValue(ui);
                    object boundDialogue = null;
                    PropertyInfo dialogueProperty = impl == null ? null : AccessTools.Property(impl.GetType(), "dialogue");
                    if (dialogueProperty != null) boundDialogue = dialogueProperty.GetValue(impl, null);
                    object runner = null;
                    FieldInfo runnerField = boundDialogue == null ? null : AccessTools.Field(boundDialogue.GetType(), "dialogueRunner");
                    if (runnerField != null) runner = runnerField.GetValue(boundDialogue);
                    FieldInfo runnerUiField = runner == null ? null : AccessTools.Field(runner.GetType(), "dialogueUI");
                    object runnerUi = runnerUiField == null ? null : runnerUiField.GetValue(runner);
                    string node = "?";
                    bool running = false;
                    if (runner != null)
                    {
                        PropertyInfo nodeProperty = AccessTools.Property(runner.GetType(), "currentNodeName");
                        PropertyInfo runningProperty = AccessTools.Property(runner.GetType(), "isDialogueRunning");
                        if (nodeProperty != null) node = Convert.ToString(nodeProperty.GetValue(runner, null));
                        if (runningProperty != null) running = Convert.ToBoolean(runningProperty.GetValue(runner, null));
                    }
                    Logger.LogInfo("Dialogue UI binding: ui=" + DialogueUiIdentity(ui) +
                        " active=" + ui.gameObject.activeInHierarchy +
                        " implMatchesTarget=" + object.ReferenceEquals(impl, targetImpl) +
                        " dialogueMatchesTarget=" + object.ReferenceEquals(boundDialogue, target) +
                        " runnerMatchesTarget=" + object.ReferenceEquals(runner, targetRunner) +
                        " runnerUiMatchesTarget=" + object.ReferenceEquals(runnerUi, ui) +
                        " runnerUi=" + DialogueUiIdentity(runnerUi) +
                        " runner=" + (runner == null ? "null" : runner.GetType().Name + "#" + runner.GetHashCode()) +
                        " running=" + running + " node=" + node);
                }
                Logger.LogInfo("Guest target dialogue state: impl=" +
                    (targetImpl == null ? "null" : targetImpl.GetType().Name + "#" + targetImpl.GetHashCode()) +
                    " runner=" + (targetRunner == null ? "null" : targetRunner.GetType().Name + "#" + targetRunner.GetHashCode()));
            }
            catch (Exception e) { Logger.LogWarning("Dialogue UI binding diagnostics failed: " + e.GetType().Name); }
        }

        private void LogDialogueNodeAvailability(Dialogue target, TextAsset asset, string node)
        {
            FieldInfo runnerField = AccessTools.Field(target.GetType(), "dialogueRunner");
            object runner = runnerField == null ? null : runnerField.GetValue(target);
            MethodInfo existsMethod = runner == null ? null : AccessTools.Method(runner.GetType(), "NodeExists", new Type[] { typeof(string) });
            MethodInfo textMethod = AccessTools.Method(target.GetType(), "GetNodeText", new Type[] { typeof(string) });
            bool exists = false;
            string existsError = "none";
            try { exists = existsMethod != null && (bool)existsMethod.Invoke(runner, new object[] { node }); }
            catch (Exception e) { existsError = DialogueDiagnosticException(e); }
            string nodeText = "<not read>";
            string textError = "none";
            if (exists)
            {
                try { nodeText = textMethod == null ? "<unavailable>" : Convert.ToString(textMethod.Invoke(target, new object[] { node })); }
                catch (Exception e) { textError = DialogueDiagnosticException(e); }
            }
            if (nodeText == null) nodeText = "<null>";
            nodeText = nodeText.Replace('\r', ' ').Replace('\n', ' ');
            if (nodeText.Length > 240) nodeText = nodeText.Substring(0, 240);
            Logger.LogInfo("Guest dialogue node availability: asset=" + (asset == null ? "null" : asset.name + "#" + asset.GetInstanceID()) +
                " node=" + node + " exists=" + exists + " existsError=" + existsError + " text=" + nodeText + " textError=" + textError +
                " currentNode=" + ReadDialogueNode(target) + " running=" + IsDialogueRunning(target));
        }

        private static string DialogueDiagnosticException(Exception e)
        {
            TargetInvocationException invocation = e as TargetInvocationException;
            Exception detail = invocation != null && invocation.InnerException != null ? invocation.InnerException : e;
            return detail.GetType().Name + ":" + (detail.Message ?? "").Replace('\r', ' ').Replace('\n', ' ');
        }

        private static bool DialogueOptionCommitPrefix(object __instance, ref int __0)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.Connected) return true;
            if (plugin._applyingDeferredChoiceCallback) return true;
            plugin._testChoiceCallbacks++;
            plugin._testChoiceCallbackStatus = "local index " + __0;
            string node = "";
            try
            {
                object dialogue = FindDialogueReference(null, __instance);
                node = ReadDialogueNode(dialogue);
            }
            catch { return true; }

            if (plugin.IsHost)
            {
                plugin.ObserveHostOptionActive(plugin._hostOptionsActive);
                plugin._hostChoiceCommitIndex = Math.Max(0, __0);
                plugin._hostChoiceCommitText = OptionLabelAt(plugin._hostOptionLabels, __0);
                plugin._hostChoiceCommitNode = node;
                plugin._hostChoiceCommitSessionId = plugin._hostOptionSessionId;
                unchecked { plugin._hostChoiceCommitId++; }
                if (plugin._hostChoiceCommitId == 0) plugin._hostChoiceCommitId = 1;
                plugin._testChoiceCallbacksApplied++;
                plugin._testChoiceCallbackStatus = "host committed index " + __0 + " label=" + plugin._hostChoiceCommitText + " session " + plugin._hostChoiceCommitSessionId;
                plugin._dialogueConfirmPending = false;
                return true;
            }

            if (plugin._hostChoiceCommitId != 0 &&
                plugin._hostChoiceCommitId != plugin._lastAppliedChoiceCommitId &&
                SameChoiceSession(plugin._clientActiveOptionSessionId, plugin._hostChoiceCommitSessionId,
                    plugin._clientActiveOptionNode, plugin._hostChoiceCommitNode) &&
                SameChoiceNode(node, plugin._hostChoiceCommitNode))
            {
                int localIndex = FindOptionLabel(plugin._clientActiveOptionLabels, plugin._hostChoiceCommitText);
                if (localIndex < 0)
                {
                    plugin._testChoiceCallbackStatus = "host choice label not found in guest option list";
                    plugin.Logger.LogWarning(plugin._testChoiceCallbackStatus + ": " + plugin._hostChoiceCommitText);
                    return false;
                }
                __0 = localIndex;
                plugin._lastAppliedChoiceCommitId = plugin._hostChoiceCommitId;
                plugin._pendingClientChoiceConfirm = false;
                plugin._lastAppliedChoiceSessionId = plugin._hostChoiceCommitSessionId;
                plugin._clientActiveOptionSessionId = 0;
                plugin._clientActiveOptionNode = "";
                plugin._clientActiveOptionCount = 0;
                plugin._testChoiceCallbacksApplied++;
                plugin._testChoiceCallbackStatus = "guest mapped host label to local index " + __0 + " session " + plugin._hostChoiceCommitSessionId;
                plugin.Logger.LogInfo("Applied host dialogue choice commit " + plugin._lastAppliedChoiceCommitId +
                    " at index " + __0 + " for option session " + plugin._hostChoiceCommitSessionId);
                return true;
            }

            if (plugin._hasDeferredChoiceCallback)
            {
                plugin._testChoiceCallbackStatus = "guest choice callback already waiting for host commit";
                return false;
            }
            plugin._hasDeferredChoiceCallback = true;
            plugin._deferredChoiceTarget = __instance;
            plugin._deferredLocalChoiceIndex = __0;
            plugin._deferredChoiceNode = node;
            plugin._deferredChoiceDeadline = Time.realtimeSinceStartup + 1.5f;
            plugin._testChoiceCallbackStatus = "guest choice deferred for host commit; host session " + plugin._hostOptionSessionId;
            return false;
        }

        private static bool SameChoiceNode(string left, string right)
        {
            return string.Equals(left ?? "", right ?? "", StringComparison.Ordinal);
        }

        private static List<string> CopyOptionLabels(System.Collections.IList options)
        {
            List<string> labels = new List<string>();
            if (options == null) return labels;
            for (int i = 0; i < options.Count; i++) labels.Add(options[i] as string ?? "");
            return labels;
        }

        private static string OptionLabelAt(List<string> labels, int index)
        {
            return labels != null && index >= 0 && index < labels.Count ? labels[index] : "";
        }

        private static int FindOptionLabel(System.Collections.IList options, string label)
        {
            if (options == null || string.IsNullOrEmpty(label)) return -1;
            for (int i = 0; i < options.Count; i++)
                if (string.Equals(options[i] as string, label, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static int FindOptionLabel(List<string> options, string label)
        {
            if (options == null || string.IsNullOrEmpty(label)) return -1;
            for (int i = 0; i < options.Count; i++)
                if (string.Equals(options[i], label, StringComparison.Ordinal)) return i;
            return -1;
        }

        private void TryApplyDeferredChoiceCallback(Packet packet)
        {
            if (!_hasDeferredChoiceCallback ||
                !SameChoiceSession(_clientActiveOptionSessionId, packet.ChoiceCommitSessionId,
                    _clientActiveOptionNode, packet.ChoiceCommitNode) ||
                !SameChoiceNode(_deferredChoiceNode, packet.ChoiceCommitNode)) return;
            int localIndex = FindOptionLabel(_clientActiveOptionLabels, packet.ChoiceCommitText);
            if (localIndex < 0)
            {
                _testChoiceCallbackStatus = "host choice label not found in deferred guest option list";
                Logger.LogWarning(_testChoiceCallbackStatus + ": " + packet.ChoiceCommitText);
                return;
            }
            CompleteDeferredChoiceCallback(localIndex, packet.ChoiceCommitId);
        }

        private void CompleteDeferredChoiceCallback(int choiceIndex, uint commitId)
        {
            if (!_hasDeferredChoiceCallback) return;
            object target = _deferredChoiceTarget;
            _hasDeferredChoiceCallback = false;
            _deferredChoiceTarget = null;
            _applyingDeferredChoiceCallback = true;
            try
            {
                MethodInfo setOption = target == null ? null : AccessTools.Method(
                    target.GetType(), "SetOption", new Type[] { typeof(int) });
                if (setOption == null) throw new MissingMethodException("DialogueUI.SetOption(int) is unavailable");
                setOption.Invoke(target, new object[] { choiceIndex });
                if (commitId != 0)
                {
                    _lastAppliedChoiceCommitId = commitId;
                    _lastAppliedChoiceSessionId = _hostChoiceCommitSessionId;
                    _testChoiceCallbacksApplied++;
                    _clientActiveOptionSessionId = 0;
                    _clientActiveOptionNode = "";
                    _clientActiveOptionCount = 0;
                    _testChoiceCallbackStatus = "guest applied host index " + choiceIndex + " session " + _hostChoiceCommitSessionId;
                    Logger.LogInfo("Applied deferred host dialogue choice commit " + commitId +
                        " at index " + choiceIndex + " for option session " + _hostChoiceCommitSessionId);
                }
                else _testChoiceCallbackStatus = "guest used local choice after host commit timeout; index " + choiceIndex;
            }
            catch (Exception e)
            {
                Exception cause = e.InnerException ?? e;
                _testChoiceCallbackStatus = "deferred choice invocation failed: " + cause.GetType().Name + ": " + cause.Message;
                Logger.LogWarning("Deferred dialogue choice callback failed: " + cause);
            }
            finally { _applyingDeferredChoiceCallback = false; }
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

        private static string DialogueRunnerStatus()
        {
            return DialogueRunnerStatus(global::Global.dialogue);
        }

        private static string DialogueRunnerStatus(object dialogue)
        {
            try
            {
                FieldInfo runnerField = dialogue == null ? null : AccessTools.Field(dialogue.GetType(), "dialogueRunner");
                object runner = runnerField == null ? null : runnerField.GetValue(dialogue);
                if (runner == null) return "unavailable";
                PropertyInfo nodeProperty = AccessTools.Property(runner.GetType(), "currentNodeName");
                PropertyInfo runningProperty = AccessTools.Property(runner.GetType(), "isDialogueRunning");
                object node = nodeProperty == null ? null : nodeProperty.GetValue(runner, null);
                object running = runningProperty == null ? null : runningProperty.GetValue(runner, null);
                return "node=" + (node == null ? "" : node.ToString()) +
                    " running=" + (running == null ? "?" : running.ToString());
            }
            catch (Exception e) { return "error=" + e.GetType().Name; }
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
            if (plugin == null || __instance == null) return;
            plugin.SyncRemoteMae(__instance);
        }

        private static void PlayerLateUpdatePostfix(global::Player __instance)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || __instance == null) return;
            plugin.ProcessPendingPlayerInteractionCleanup(__instance);
            plugin.SyncRemoteMae(__instance);
        }

        private void SyncRemoteMae(global::Player localPlayer)
        {
            bool hasRemote = IsHost ? _hasGuestPosition : _hasHostPosition;
            Vector3 remotePosition = IsHost ? _guestPosition : _hostPosition;
            string remoteScene = IsHost ? _guestSceneName : _hostSceneName;
            if (!Connected || !hasRemote || localPlayer == null ||
                !string.Equals(Application.loadedLevelName, remoteScene, StringComparison.Ordinal))
            {
                DestroyRemoteMae();
                _puppetStatus = Connected ? "waiting for matching scene/position" : "disconnected";
                return;
            }
            if (_puppetSource != localPlayer || _remotePuppetRoot == null)
            {
                DestroyRemoteMae();
                try
                {
                    _puppetSource = localPlayer;
                    _remotePuppetRoot = CreatePuppetNode(localPlayer.transform, null);
                    _remotePuppetRoot.name = "Remote Mae (Co-op)";
                    _remotePuppetRoot.hideFlags = HideFlags.HideAndDontSave;
                    AttachPuppetAnimator();
                }
                catch (Exception e)
                {
                    DestroyRemoteMae();
                    _puppetStatus = "create failed: " + e.GetType().Name;
                    return;
                }
            }
            if (_remoteAnimator == null && _localUnityAnimator != null) AttachPuppetAnimator();
            CopyPuppetTransformsAndSprites();
            ApplyRemoteAnimation(IsHost ? GuestInput : HostInput);
            ApplyRemotePuppetTint();
            _remotePuppetRoot.transform.position = remotePosition;
            _puppetStatus = "visible at " + remotePosition.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                remotePosition.y.ToString("F2", CultureInfo.InvariantCulture) + " sprites=" + _puppetSprites.Count;
        }

        private GameObject CreatePuppetNode(Transform source, Transform parent)
        {
            GameObject node = new GameObject(source.name + " (Co-op visual)");
            node.hideFlags = HideFlags.HideAndDontSave;
            Transform target = node.transform;
            target.SetParent(parent, false);
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            _puppetSourceTransforms.Add(source);
            _puppetTransforms.Add(target);
            SpriteRenderer[] renderers = source.GetComponents<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer targetRenderer = node.AddComponent<SpriteRenderer>();
                CopySpriteRenderer(renderers[i], targetRenderer);
                _puppetSourceSprites.Add(renderers[i]);
                _puppetSprites.Add(targetRenderer);
            }
            for (int i = 0; i < source.childCount; i++) CreatePuppetNode(source.GetChild(i), target);
            return node;
        }

        private void AttachPuppetAnimator()
        {
            if (_localUnityAnimator == null)
            {
                _puppetAnimatorStatus = "local animator unavailable";
                return;
            }
            Transform animatorTransform = _localUnityAnimator.transform;
            int sourceIndex = _puppetSourceTransforms.IndexOf(animatorTransform);
            if (sourceIndex < 0 || sourceIndex >= _puppetTransforms.Count)
            {
                _puppetAnimatorStatus = "animator object outside cloned player hierarchy: " + animatorTransform.name;
                return;
            }
            CreatePuppetAnimator(_localUnityAnimator, _puppetTransforms[sourceIndex].gameObject);
        }

        private void CreatePuppetAnimator(Animator original, GameObject target)
        {
            try
            {
                Animator clone = target.AddComponent<Animator>();
                clone.runtimeAnimatorController = original.runtimeAnimatorController;
                clone.avatar = original.avatar;
                clone.applyRootMotion = false;
                clone.updateMode = original.updateMode;
                clone.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                clone.fireEvents = false;
                clone.enabled = original.enabled;
                _remoteAnimator = clone;
                _puppetAnimatorStatus = "attached to " + target.name;
            }
            catch (Exception e)
            {
                _puppetAnimatorStatus = "clone failed: " + e.GetType().Name;
                Logger.LogWarning("Could not create remote Unity Animator: " + e.GetType().Name);
            }
        }

        private void ApplyRemoteAnimation(InputState remote)
        {
            if (_remoteAnimator == null || remote == null || !remote.HasAnimation || remote.AnimationHash == 0) return;
            try
            {
                float normalizedTime = remote.AnimationTime;
                if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime)) normalizedTime = 0f;
                _remoteAnimator.SetInteger("facing", remote.Facing);
                _remoteAnimator.SetBool("walking", remote.Walking);
                _remoteAnimator.Play(remote.AnimationHash, 0, normalizedTime);
                _remoteAnimator.Update(0f);
                _lastRemoteAnimationHash = remote.AnimationHash;
                _lastRemoteAnimationTime = normalizedTime;
            }
            catch (Exception e) { Logger.LogWarning("Could not apply remote Unity Animator state: " + e.GetType().Name); }
        }
        private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer target)
        {
            target.sprite = source.sprite;
            target.color = source.color;
            target.flipX = source.flipX;
            target.flipY = source.flipY;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            target.enabled = source.enabled;
            target.sharedMaterial = source.sharedMaterial;
        }

        private void CopyPuppetTransformsAndSprites()
        {
            for (int i = 0; i < _puppetSourceTransforms.Count; i++)
            {
                Transform source = _puppetSourceTransforms[i];
                Transform target = _puppetTransforms[i];
                if (source != null && target != null)
                {
                    target.localPosition = source.localPosition;
                    target.localRotation = source.localRotation;
                    target.localScale = source.localScale;
                }
            }
            for (int i = 0; i < _puppetSourceSprites.Count; i++)
            {
                SpriteRenderer source = _puppetSourceSprites[i];
                SpriteRenderer target = _puppetSprites[i];
                if (source != null && target != null) CopySpriteRenderer(source, target);
            }
        }

        private void ApplyRemotePuppetTint()
        {
            for (int i = 0; i < _puppetSourceSprites.Count; i++)
            {
                SpriteRenderer source = _puppetSourceSprites[i];
                SpriteRenderer target = _puppetSprites[i];
                if (source == null || target == null) continue;

                Color color = source.color;
                target.color = new Color(
                    color.r * RemotePuppetRedTint,
                    color.g * RemotePuppetGreenTint,
                    color.b * RemotePuppetBlueTint,
                    color.a * RemotePuppetOpacity);
            }
        }

        private void DestroyRemoteMae()
        {
            if (_remotePuppetRoot != null) UnityEngine.Object.Destroy(_remotePuppetRoot);
            _remotePuppetRoot = null;
            _remoteAnimator = null;
            _puppetAnimatorStatus = "not created";
            _lastRemoteAnimationHash = 0;
            _lastRemoteAnimationTime = 0f;
            _puppetSource = null;
            _puppetSourceTransforms.Clear();
            _puppetTransforms.Clear();
            _puppetSourceSprites.Clear();
            _puppetSprites.Clear();
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12, 12, 620, 28), "Night in the Woods Co-op | " + RoleName + " | " + _status);
            if (IsHost && _peer == null) GUI.Label(new Rect(12, 32, 620, 24), "Share this PC's LAN/VPN IPv4 address; UDP " + PortValue());
            if (!IsClient) return;
            if (!string.Equals(_hostDialogueNode, "TV", StringComparison.Ordinal))
            {
                _guestTvOverlayText = "";
                return;
            }
            if (!string.IsNullOrEmpty(_guestTvOverlayText))
            {
                if (_guestTvCaptionBackground == null)
                {
                    _guestTvCaptionBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    _guestTvCaptionBackground.hideFlags = HideFlags.HideAndDontSave;
                    _guestTvCaptionBackground.SetPixel(0, 0, Color.black);
                    _guestTvCaptionBackground.Apply();
                }
                GUIStyle caption = new GUIStyle(GUI.skin.box);
                caption.alignment = TextAnchor.MiddleCenter;
                caption.wordWrap = true;
                caption.fontSize = 36;
                caption.padding = new RectOffset(8, 8, 6, 6);
                caption.normal.background = _guestTvCaptionBackground;
                caption.onNormal.background = _guestTvCaptionBackground;
                caption.normal.textColor = Color.white;
                Rect captionRect = new Rect(Screen.width * 0.627f, Screen.height * 0.182f,
                    Screen.width * 0.165f, Screen.height * 0.09f);
                GUI.Box(captionRect, _guestTvOverlayText.ToUpperInvariant(), caption);
            }
        }

        private InputState ReadLocalInput()
        {
            InputState state = new InputState();
            CaptureLocalAnimation(state);
            _readingLocalInput = true;
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
            finally { _readingLocalInput = false; }
            ApplyBotInput(state);
            if (!state.ConfirmDown && !(IsHost && GuestInput.ConfirmDown))
                _dialogueConfirmConsumedUntilRelease = false;
            if (state.InteractDown && !HasRunningDialogue() && !_pendingBotConfirm)
            {
                state.ConfirmDown = false;
                _suppressLocalConfirmUntil = Time.realtimeSinceStartup + 0.25f;
            }
            bool localConfirmRising = state.ConfirmDown && !_lastLocalDialogueConfirm;
            _lastLocalDialogueConfirm = state.ConfirmDown;
            if (IsHost && localConfirmRising)
            {
                _hostConfirmBroadcastUntil = Time.realtimeSinceStartup + 0.15f;
                if (_hostOptionsActive) _dialogueConfirmPending = true;
            }
            return state;
        }

        private void CaptureLocalAnimation(InputState state)
        {
            _localUnityAnimator = null;
            state.HasAnimation = false;
            try
            {
                global::Player player = UnityEngine.Object.FindObjectOfType(typeof(global::Player)) as global::Player;
                if (player == null) { _animationCaptureStatus = "no-player"; state.HasAnimation = false; return; }
                FieldInfo animatorField = AccessTools.Field(typeof(global::Player), "animator");
                Animator animator = animatorField == null ? null : animatorField.GetValue(player) as Animator;
                _localUnityAnimator = animator;
                if (animator == null) { _animationCaptureStatus = "Player.animator=null"; state.HasAnimation = false; return; }
                AnimatorStateInfo animation = animator.GetCurrentAnimatorStateInfo(0);
                state.AnimationHash = animation.fullPathHash;
                state.AnimationTime = animation.normalizedTime;
                PropertyInfo facingProperty = AccessTools.Property(typeof(global::Player), "facing");
                state.Facing = facingProperty == null ? 0 : Convert.ToInt32(facingProperty.GetValue(player, null), CultureInfo.InvariantCulture);
                state.Walking = animator.GetBool("walking");
                state.HasAnimation = state.AnimationHash != 0;
                _animationCaptureStatus = "Unity Animator=" + state.AnimationHash.ToString("X8", CultureInfo.InvariantCulture) + "/" + state.AnimationTime.ToString("F3", CultureInfo.InvariantCulture) +
                    ";transition=" + animator.IsInTransition(0) + ";facing=" + state.Facing + ";walking=" + state.Walking;
            }
            catch (Exception e) { state.HasAnimation = false; _animationCaptureStatus = "error=" + e.GetType().Name; }
        }
        private int SignalBotDialogueNext()
        {
            if (!HasRunningDialogue() || global::Global.dialogue == null) return 0;
            UnityEngine.Object[] implementations = UnityEngine.Object.FindObjectsOfType(typeof(global::DialogueImplementation));
            int signaled = 0;
            for (int i = 0; i < implementations.Length; i++)
            {
                global::DialogueImplementation implementation = implementations[i] as global::DialogueImplementation;
                if (implementation == null || !object.ReferenceEquals(implementation.dialogue, global::Global.dialogue)) continue;
                implementation.OnNext();
                signaled++;
                _testBotDialogueNextSignals++;
            }
            if (signaled == 0)
                Logger.LogWarning("QA bot confirm could not find the active DialogueImplementation");
            else
                Logger.LogInfo("QA bot confirm signaled active DialogueImplementation.OnNext count=" + signaled);
            return signaled;
        }
        private void ApplyBotInput(InputState state)
        {
            try
            {
                if (File.Exists(_botCommandFile))
                {
                    string command = File.ReadAllText(_botCommandFile).Trim();
                    File.Delete(_botCommandFile);
                    if (string.Equals(command, "interact", StringComparison.OrdinalIgnoreCase))
                    {
                        _botInteractFrame = Time.frameCount + 1;
                        _botStatus = "interact queued";
                    }
                    else if (string.Equals(command, "qa_request_mural", StringComparison.OrdinalIgnoreCase))
                    {
                        TextAsset asset = CurrentSceneDialogueAsset();
                        if (IsClient && Connected && Application.loadedLevelName == "BusStation" &&
                            HasRunningDialogue() && asset != null && _pendingClientInteractionRequestId == 0)
                        {
                            unchecked { _guestInteractionRequestSequence++; }
                            if (_guestInteractionRequestSequence == 0) _guestInteractionRequestSequence = 1;
                            _pendingClientInteractionRequestId = _guestInteractionRequestSequence;
                            _pendingClientInteractionDeadline = Time.realtimeSinceStartup + 5f;
                            _pendingClientInteractionAsset = asset.name;
                            _pendingClientInteractionNode = "Mural";
                            _guestInteractionStatus = "QA queued request=" + _pendingClientInteractionRequestId +
                                " asset=" + asset.name + " node=Mural";
                            _botStatus = "QA Mural request queued while dialogue active";
                            Logger.LogInfo("QA bot queued guest Mural request: request=" +
                                _pendingClientInteractionRequestId + " asset=" + asset.name + " node=Mural");
                        }
                        else _botStatus = "rejected qa_request_mural";
                    }
                    else if (string.Equals(command, "qa_repeat_mural_request", StringComparison.OrdinalIgnoreCase))
                    {
                        TextAsset asset = CurrentSceneDialogueAsset();
                        if (IsClient && Connected && Application.loadedLevelName == "BusStation" &&
                            asset != null && _guestInteractionRequestSequence != 0 &&
                            _pendingClientInteractionRequestId == 0)
                        {
                            _pendingClientInteractionRequestId = _guestInteractionRequestSequence;
                            _pendingClientInteractionDeadline = Time.realtimeSinceStartup + 5f;
                            _pendingClientInteractionAsset = asset.name;
                            _pendingClientInteractionNode = "Mural";
                            _guestInteractionStatus = "QA resent request=" + _pendingClientInteractionRequestId +
                                " asset=" + asset.name + " node=Mural";
                            _botStatus = "QA repeated last Mural request";
                            Logger.LogInfo("QA bot resent guest Mural request: request=" +
                                _pendingClientInteractionRequestId + " asset=" + asset.name + " node=Mural");
                        }
                        else _botStatus = "rejected qa_repeat_mural_request";
                    }
                    else if (string.Equals(command, "qa_ravine_geometry", StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsHost && Application.loadedLevelName == "Ravine")
                        {
                            _botStatus = "Ravine geometry diagnostic queued";
                            DumpRavineGeometry();
                        }
                        else _botStatus = "rejected qa_ravine_geometry";
                    }
                    else if (string.Equals(command, "jump", StringComparison.OrdinalIgnoreCase))
                    {
                        _botJumpFrame = Time.frameCount + 1;
                        state.JumpDown = true;
                        _botStatus = "jump queued";
                    }
                    else if (string.Equals(command, "confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        // A dialogue confirm is not an interaction. Echoing an Interact
                        // edge from the host can make the guest replay the same interaction.
                        _botInteractFrame = HasRunningDialogue() ? -1 : Time.frameCount + 1;
                        _botConfirmFrames = IsHost ? 4 : 0;
                        _botConfirmUntil = IsHost ? 0f : Time.realtimeSinceStartup + 0.15f;
                        _pendingBotConfirm = true;
                        _pendingBotIntroPoemConfirm = string.Equals(Application.loadedLevelName, "IntroPoem", StringComparison.Ordinal);
                        if (_pendingBotIntroPoemConfirm) _testIntroPoemConfirmStatus = "confirm queued for IntroPoemBrain";
                        if (IsHost && _hostOptionsActive) _dialogueConfirmPending = true;
                                                bool optionConfirm = IsHost ? _hostOptionsActive : ClientOptionIteratorActive();
                        int signaledDialogueNext = optionConfirm ? 0 : SignalBotDialogueNext();
                        if (optionConfirm)
                        {
                            Logger.LogInfo("QA bot choice confirm skipped DialogueImplementation.OnNext so it cannot move the option cursor");
                            _botStatus = "choice confirm queued; skipped DialogueImplementation.OnNext";
                        }
                        else _botStatus = "confirm queued; DialogueImplementation.OnNext=" + signaledDialogueNext;
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
                            sceneName == "Playground" || sceneName == "MaeRoom" || sceneName == "Ravine")
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
                    else if (command.StartsWith("dialogue:", StringComparison.OrdinalIgnoreCase))
                    {
                        string node = command.Substring("dialogue:".Length).Trim();
                        TextAsset asset = CurrentSceneDialogueAsset();
                        if (IsHost && !string.IsNullOrEmpty(node) && asset != null && global::Global.dialogue != null)
                        {
                            global::Global.dialogue.Run(asset, node, 0);
                            _botStatus = "started host dialogue " + node;
                        }
                        else _botStatus = "rejected dialogue";
                    }
                    else if (string.Equals(command, "qa_mirrored_mural", StringComparison.OrdinalIgnoreCase))
                    {
                        TextAsset asset = CurrentSceneDialogueAsset();
                        if (IsHost && Connected && Application.loadedLevelName == "BusStation" &&
                            asset != null && global::Global.dialogue != null && !HasRunningDialogue())
                        {
                            _hostRemoteInteractInjectedUntil = Time.realtimeSinceStartup + 0.5f;
                            global::Global.dialogue.Run(asset, "Mural", 0);
                            _botStatus = "started mirrored-input Mural";
                        }
                        else _botStatus = "rejected qa_mirrored_mural";
                    }
                    else if (string.Equals(command, "choice_axis_next", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command, "choice_axis_prev", StringComparison.OrdinalIgnoreCase))
                    {
                        int direction = string.Equals(command, "choice_axis_next", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
                        if (IsClient && ClientOptionIteratorActive() && _clientActiveOptionCount > 0)
                        {
                            _testChoiceAxisDirection = direction;
                            _testChoiceAxisFrames = 2;
                            _botStatus = "queued guest horizontal axis " + direction + " session " + _clientActiveOptionSessionId;
                        }
                        else _botStatus = "rejected " + command;
                    }
                    else if (string.Equals(command, "choice_next", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command, "choice_prev", StringComparison.OrdinalIgnoreCase))
                    {
                        object implementation = _testOptionImplementation;
                        Type implementationType = implementation == null ? null : implementation.GetType();
                        FieldInfo activeField = implementationType == null ? null : AccessTools.Field(implementationType, "runningOptions");
                        bool next = string.Equals(command, "choice_next", StringComparison.OrdinalIgnoreCase);
                        FieldInfo arrowField = implementationType == null ? null : AccessTools.Field(implementationType, next ? "inputArrowNext" : "inputArrowPrev");
                        if (IsClient && ClientOptionIteratorActive() && _clientActiveOptionCount > 0 &&
                            implementation != null && activeField != null && (bool)activeField.GetValue(implementation) && arrowField != null)
                        {
                            arrowField.SetValue(implementation, true);
                            _botStatus = "queued guest " + (next ? "next" : "previous") + " option session " + _clientActiveOptionSessionId;
                        }
                        else _botStatus = "rejected " + command;
                    }
                    else if (command.StartsWith("choice:", StringComparison.OrdinalIgnoreCase))
                    {
                        int index;
                        object implementation = _testOptionImplementation;
                        Type implementationType = implementation == null ? null : implementation.GetType();
                        FieldInfo activeField = implementationType == null ? null : AccessTools.Field(implementationType, "runningOptions");
                        FieldInfo indexField = implementationType == null ? null : AccessTools.Field(implementationType, "currentOption");
                        if (int.TryParse(command.Substring("choice:".Length), out index) && IsClient &&
                            index >= 0 && index < _clientActiveOptionCount && ClientOptionIteratorActive())
                        {
                            _clientChoiceSelectionRequestIndex = index;
                            _clientChoiceSelectionRequestSessionId = _clientActiveOptionSessionId;
                            _botStatus = "requested guest option " + index + " session " + _clientActiveOptionSessionId;
                        }
                        else if (IsHost && int.TryParse(command.Substring("choice:".Length), out index) &&
                            index >= 0 && index < _hostOptionCount && implementation != null &&
                            activeField != null && indexField != null && (bool)activeField.GetValue(implementation))
                        {
                            indexField.SetValue(implementation, index);
                            _hostOptionIndex = index;
                            _botStatus = "selected host option " + index;
                        }
                        else _botStatus = "rejected choice";
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
                    if (_botInteractFrame == Time.frameCount) state.InteractDown = true;
                    _botConfirmFrames--;
                }
                if (!IsHost && Time.realtimeSinceStartup < _botConfirmUntil)
                {
                    state.ConfirmDown = true;
                    if (_botInteractFrame == Time.frameCount) state.InteractDown = true;
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

        private void CheckHostDialogueCompletion()
        {
            if (!IsHost || !_connected || _hostDialogueRunId == 0 ||
                _hostDialogueEndSentForRunId == _hostDialogueRunId ||
                Time.frameCount <= _hostDialogueRunStartFrame + 2 || IsDialogueRunning(global::Global.dialogue)) return;
            if (!_hostDialogueWireNodeReady)
            {
                _hostDialogueWireNode = _hostDialogueNode;
                _hostDialogueWireNodeReady = !string.IsNullOrEmpty(_hostDialogueWireNode);
                _hostDialogueFallbackHoldUntil = Time.realtimeSinceStartup + 0.1f;
                _nextSend = 0f;
                Logger.LogInfo("Host dialogue completed before a line resolved its Yarn node; publishing requested-node fallback " +
                    _hostDialogueWireNode + " for run " + _hostDialogueRunId);
                return;
            }
            if (_hostDialogueFallbackHoldUntil > Time.realtimeSinceStartup) return;
            SchedulePlayerInteractionCleanup();
            uint completedRunId = _hostDialogueRunId;
            if (_pendingGuestConfirm)
            {
                _pendingGuestConfirm = false;
                _testDialogueConfirmDiagnostics = "discarded unconsumed guest confirm at end of host run " + completedRunId;
                Logger.LogInfo("Discarded unconsumed guest confirm at end of host dialogue run " + completedRunId);
            }
            unchecked { _hostDialogueRunId++; }
            if (_hostDialogueRunId == 0) _hostDialogueRunId = 1;
            _hostDialogueEndSentForRunId = _hostDialogueRunId;
            _hostDialogueAsset = "";
            _hostDialogueNode = "";
            _hostDialogueWireNode = "";
            _hostDialogueWireNodeReady = false;
            _hostDialogueFallbackHoldUntil = 0f;
            _hostDialogueRunStream = "";
            _hostDialogueOption = 0;
            Logger.LogInfo("Host dialogue ended; sent end marker after run " + completedRunId + " as run " + _hostDialogueRunId);
        }
        private static string PlayerStateName(global::Player player)
        {
            if (player == null) return "none";
            try
            {
                PropertyInfo stateProperty = AccessTools.Property(player.GetType(), "state");
                Delegate state = stateProperty == null ? null : stateProperty.GetValue(player, null) as Delegate;
                return state == null || state.Method == null ? "none" : state.Method.Name;
            }
            catch (Exception e) { return "unavailable:" + e.GetType().Name; }
        }

        private void SetPlayerInteractionCompletionStatus(string status)
        {
            _playerInteractionCompletionStatus = status;
            _playerInteractionCompletionStatusFrame = Time.frameCount;
        }

        private void SchedulePlayerInteractionCleanup()
        {
            if (!IsClient && !IsHost) return;
            if (global::Global.isInitializingLevel)
            {
                SetPlayerInteractionCompletionStatus("deferred during level initialization");
                return;
            }
            global::Player player = global::Global.player;
            string stateName = PlayerStateName(player);
            if (player == null || (!string.Equals(stateName, "Interact", StringComparison.Ordinal) &&
                !string.Equals(stateName, "PreInteract", StringComparison.Ordinal)))
            {
                SetPlayerInteractionCompletionStatus("skipped state=" + stateName);
                return;
            }
            if (_playerInteractionCleanupPending) return;
            _playerInteractionCleanupPending = true;
            _playerInteractionCleanupAttempts = 0;
            _playerInteractionCleanupLastAttemptFrame = -1;
            _playerInteractionCleanupDeadlineFrame = Time.frameCount + 180;
            SetPlayerInteractionCompletionStatus("scheduled cleanup state=" + stateName);
            Logger.LogInfo("Scheduled Player.Interact cleanup; state=" + stateName);
        }

        private void ProcessPendingPlayerInteractionCleanup(global::Player player)
        {
            if ((!IsClient && !IsHost) || !_playerInteractionCleanupPending || player == null ||
                !object.ReferenceEquals(player, global::Global.player) || HasRunningDialogue() ||
                global::Global.isInitializingLevel) return;
            if (_playerInteractionCleanupLastAttemptFrame == Time.frameCount) return;
            _playerInteractionCleanupLastAttemptFrame = Time.frameCount;

            string stateName = PlayerStateName(player);
            if (Time.frameCount >= _playerInteractionCleanupDeadlineFrame)
            {
                _playerInteractionCleanupPending = false;
                _playerInteractionCleanupDeadlineFrame = -1;
                SetPlayerInteractionCompletionStatus("timed out state=" + stateName +
                    " attempts=" + _playerInteractionCleanupAttempts);
                Logger.LogWarning("Player.Interact cleanup timed out; state=" + stateName +
                    " attempts=" + _playerInteractionCleanupAttempts);
                return;
            }
            if (string.Equals(stateName, "InteractCompleting", StringComparison.Ordinal))
            {
                SetPlayerInteractionCompletionStatus("waiting for InteractCompleting state to settle");
                return;
            }
            if (!string.Equals(stateName, "Interact", StringComparison.Ordinal) &&
                !string.Equals(stateName, "PreInteract", StringComparison.Ordinal))
            {
                _playerInteractionCleanupPending = false;
                _playerInteractionCleanupDeadlineFrame = -1;
                SetPlayerInteractionCompletionStatus("resolved state=" + stateName +
                    " attempts=" + _playerInteractionCleanupAttempts);
                Logger.LogInfo("Player.Interact completion resolved; state=" + stateName +
                    " attempts=" + _playerInteractionCleanupAttempts);
                return;
            }

            try
            {
                MethodInfo complete = AccessTools.Method(player.GetType(), "InteractComplete");
                if (complete == null)
                {
                    _playerInteractionCleanupPending = false;
                    _playerInteractionCleanupDeadlineFrame = -1;
                    SetPlayerInteractionCompletionStatus("missing InteractComplete");
                    Logger.LogWarning("Player.Interact completion unavailable: missing InteractComplete");
                    return;
                }
                complete.Invoke(player, null);
                _playerInteractionCleanupAttempts++;
                string afterStateName = PlayerStateName(player);
                if (string.Equals(afterStateName, "InteractCompleting", StringComparison.Ordinal))
                {
                    SetPlayerInteractionCompletionStatus("waiting after InteractComplete; state=" +
                        afterStateName + " attempt=" + _playerInteractionCleanupAttempts);
                    return;
                }
                if (!string.Equals(afterStateName, "Interact", StringComparison.Ordinal) &&
                    !string.Equals(afterStateName, "PreInteract", StringComparison.Ordinal))
                {
                    _playerInteractionCleanupPending = false;
                    _playerInteractionCleanupDeadlineFrame = -1;
                    SetPlayerInteractionCompletionStatus("resolved state=" + afterStateName +
                        " attempts=" + _playerInteractionCleanupAttempts);
                    Logger.LogInfo("Player.Interact completion resolved by InteractComplete; state=" +
                        afterStateName + " attempts=" + _playerInteractionCleanupAttempts);
                }
                else if (_playerInteractionCleanupAttempts >= 60)
                {
                    _playerInteractionCleanupPending = false;
                    _playerInteractionCleanupDeadlineFrame = -1;
                    SetPlayerInteractionCompletionStatus("still state=" + afterStateName +
                        " after retries=" + _playerInteractionCleanupAttempts);
                    Logger.LogWarning("Player.Interact state persisted after " +
                        _playerInteractionCleanupAttempts + " completion attempts");
                }
                else
                {
                    SetPlayerInteractionCompletionStatus("retrying state=" + afterStateName +
                        " attempt=" + _playerInteractionCleanupAttempts);
                    if (_playerInteractionCleanupAttempts == 1 || _playerInteractionCleanupAttempts % 15 == 0)
                        Logger.LogInfo("Player.Interact still active after completion attempt " +
                            _playerInteractionCleanupAttempts + "; state=" + afterStateName);
                }
            }
            catch (Exception e)
            {
                _playerInteractionCleanupAttempts++;
                if (_playerInteractionCleanupAttempts >= 60)
                {
                    _playerInteractionCleanupPending = false;
                    _playerInteractionCleanupDeadlineFrame = -1;
                }
                SetPlayerInteractionCompletionStatus("error=" + e.GetType().Name +
                    " attempt=" + _playerInteractionCleanupAttempts);
                if (_playerInteractionCleanupAttempts == 1 || _playerInteractionCleanupAttempts % 15 == 0)
                    Logger.LogWarning("Player.Interact completion failed: " + e.GetType().Name +
                        " attempt=" + _playerInteractionCleanupAttempts);
            }
        }
        private void ProcessPendingGuestInteraction()
        {
            if (!IsHost || _pendingGuestInteractionRequestId == 0 || !Connected) return;
            if (!string.Equals(_guestSceneName, Application.loadedLevelName, StringComparison.Ordinal)) return;
            if (global::Global.dialogue == null || global::Global.dialogue.running ||
                global::Global.isLoading || (global::Global.screenTransitioner != null &&
                global::Global.screenTransitioner.IsTransitioning())) return;
            TextAsset asset = CurrentSceneDialogueAsset();
            if (asset == null || !string.Equals(asset.name, _pendingGuestInteractionAsset, StringComparison.Ordinal))
            {
                UnityEngine.Object[] assets = Resources.FindObjectsOfTypeAll(typeof(TextAsset));
                for (int i = 0; i < assets.Length; i++)
                {
                    TextAsset candidate = assets[i] as TextAsset;
                    if (candidate != null && string.Equals(candidate.name, _pendingGuestInteractionAsset, StringComparison.Ordinal))
                    {
                        asset = candidate;
                        break;
                    }
                }
            }
            uint requestId = _pendingGuestInteractionRequestId;
            string requestedAsset = _pendingGuestInteractionAsset;
            string requestedNode = _pendingGuestInteractionNode;
            _pendingGuestInteractionRequestId = 0;
            _pendingGuestInteractionAsset = "";
            _pendingGuestInteractionNode = "";
            _lastGuestInteractionRequestId = requestId;
            _suppressGuestInteractUntilRelease = true;
            _pendingGuestInteract = false;
            _hostRemoteInteractInjectedUntil = 0f;
            if (asset == null)
            {
                _guestInteractionStatus = "rejected request=" + requestId + " asset not found=" + requestedAsset;
                Logger.LogWarning("Rejected guest Yarn interaction " + requestId + ": asset not found " + requestedAsset);
                return;
            }
            try
            {
                global::Global.dialogue.Run(asset, requestedNode, 0);
                _guestInteractionStatus = "host started request=" + requestId + " asset=" + asset.name + " node=" + requestedNode;
                Logger.LogInfo("Host accepted guest Yarn interaction: request=" + requestId + " asset=" + asset.name + " node=" + requestedNode);
            }
            catch (Exception e)
            {
                _guestInteractionStatus = "host interaction failed request=" + requestId + " error=" + e.GetType().Name;
                Logger.LogWarning("Guest Yarn interaction failed: " + e.GetType().Name + " request=" + requestId);
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
                foreach (KeyValuePair<string, uint> ack in p.DialogueLineAcks)
                {
                    uint previous;
                    if (!_clientDialogueLineAcks.TryGetValue(ack.Key, out previous) || IsNewer(ack.Value, previous))
                        _clientDialogueLineAcks[ack.Key] = ack.Value;
                    Queue<DialogueLineEvent> queue;
                    if (_pendingHostDialogueLines.TryGetValue(ack.Key, out queue))
                    {
                        while (queue.Count > 0 && IsSequenceAtLeast(ack.Value, queue.Peek().Sequence)) queue.Dequeue();
                        if (queue.Count == 0) _pendingHostDialogueLines.Remove(ack.Key);
                    }
                    _clientDialogueLineAck = ack.Value;
                }
                _lastGuestInputDialogueRunId = p.InputDialogueRunId;
                if (!p.State.ConfirmDown) _guestConfirmBlockedUntilRelease = false;
                else if (p.InputDialogueRunId != _hostDialogueRunId)
                {
                    if (!_guestConfirmBlockedUntilRelease)
                        Logger.LogInfo("Blocked guest confirm held for dialogue run " + p.InputDialogueRunId +
                            " while host is on run " + _hostDialogueRunId);
                    _guestConfirmBlockedUntilRelease = true;
                }
                GuestInput.CopyFrom(p.State);
                if (_guestConfirmBlockedUntilRelease) GuestInput.ConfirmDown = false;
                if (_hostOptionsActive && p.State.ChoiceRequestIndex >= 0 &&
                    p.State.ChoiceRequestIndex < _hostOptionCount &&
                    p.State.ChoiceRequestSessionId == _hostOptionSessionId)
                {
                    _pendingGuestOptionIndex = p.State.ChoiceRequestIndex;
                    _pendingGuestOptionSessionId = p.State.ChoiceRequestSessionId;
                }
                _hasGuestPosition = p.HasPosition;
                _guestPosition = p.Position;
                _guestSceneName = p.SceneName;
                if (p.InteractionRequestId != 0)
                {
                    _suppressGuestInteractUntilRelease = true;
                    _pendingGuestInteract = false;
                    _pendingGuestInteractDeadline = 0f;
                    if ((_lastGuestInteractionRequestId == 0 || IsNewer(p.InteractionRequestId, _lastGuestInteractionRequestId)) &&
                        (_pendingGuestInteractionRequestId == 0 || IsNewer(p.InteractionRequestId, _pendingGuestInteractionRequestId)))
                    {
                        string requestedAsset = p.InteractionAsset ?? "";
                        string requestedNode = p.InteractionNode ?? "";
                        bool alreadyStartedByMirroredInput = _hostDialogueStartedByMirroredGuestInteract &&
                            HasRunningDialogue() &&
                            string.Equals(_hostDialogueAsset, requestedAsset, StringComparison.Ordinal) &&
                            string.Equals(_hostDialogueNode, requestedNode, StringComparison.Ordinal);
                        if (alreadyStartedByMirroredInput)
                        {
                            _lastGuestInteractionRequestId = p.InteractionRequestId;
                            _pendingGuestInteractionRequestId = 0;
                            _pendingGuestInteractionAsset = "";
                            _pendingGuestInteractionNode = "";
                            _guestInteractionStatus = "acknowledged request=" + p.InteractionRequestId +
                                "; dialogue already started by mirrored guest input";
                            Logger.LogInfo("Acknowledged guest Yarn request already started by mirrored Interact input: request=" +
                                p.InteractionRequestId + " asset=" + requestedAsset + " node=" + requestedNode);
                        }
                        else
                        {
                            _pendingGuestInteractionRequestId = p.InteractionRequestId;
                            _pendingGuestInteractionAsset = requestedAsset;
                            _pendingGuestInteractionNode = requestedNode;
                            _guestInteractionStatus = "received request=" + p.InteractionRequestId + " asset=" +
                                _pendingGuestInteractionAsset + " node=" + _pendingGuestInteractionNode;
                        }
                    }
                }
                if (p.State.InteractDown && !_guestInteractHeld)
                {
                    _pendingGuestInteract = !_suppressGuestInteractUntilRelease && p.InteractionRequestId == 0;
                    if (_pendingGuestInteract) _pendingGuestInteractDeadline = Time.realtimeSinceStartup + 0.1f;
                    // The guest already ran this interaction locally. Do not echo its
                    // Interact edge back when its semantic Yarn request is already available.
                    if (!HasRunningDialogue())
                        _suppressLocalConfirmUntil = Time.realtimeSinceStartup + 0.25f;
                }
                _guestInteractHeld = p.State.InteractDown;
                if (!p.State.InteractDown && !p.State.InteractHeld && p.InteractionRequestId == 0)
                    _suppressGuestInteractUntilRelease = false;
                if (p.State.ConfirmDown) _testGuestConfirmPackets++;
                if (p.State.ConfirmDown && !_guestConfirmHeld)
                {
                    if ((HasRunningDialogue() || _hostOptionsActive) &&
                        p.InputDialogueRunId == _hostDialogueRunId && !_guestConfirmBlockedUntilRelease)
                    {
                        _pendingGuestConfirm = true;
                        _testDialogueConfirmDiagnostics = "guest confirm received for active host run=" + _hostDialogueRunId +
                            " consumedUntilRelease=" + _dialogueConfirmConsumedUntilRelease +
                            " lineAck=" + DialogueLineAcked("DialogueUI@Global", _hostGlobalDialogueLineSequence) +
                            " runStream=" + _hostDialogueRunStream;
                        if (_hostOptionsActive) _dialogueConfirmPending = true;
                    }
                    else
                    {
                        bool activeHostDialogue = HasRunningDialogue() || _hostOptionsActive;
                        _testDialogueConfirmDiagnostics = activeHostDialogue
                            ? "guest confirm dropped: stale dialogue run=" + p.InputDialogueRunId + " host run=" + _hostDialogueRunId
                            : "guest confirm dropped: no active host dialogue or choice menu";
                        Logger.LogInfo(activeHostDialogue
                            ? "Dropped guest confirm from stale dialogue run " + p.InputDialogueRunId + " while host is on run " + _hostDialogueRunId
                            : "Dropped guest confirm outside an active host dialogue or choice menu");
                    }
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

        private void ResetGuestSession(string reason)
        {
            _connected = false;
            _peer = null;
            _lastGuestPacket = 0f;
            _haveSequence = false;
            _lastSequence = 0;
            _pendingHostDialogueLines.Clear();
            _clientDialogueLineAcks.Clear();
            _lastAppliedHostDialogueLineSequences.Clear();
            _claimedHostDialogueLineSequences.Clear();
            _hostDialogueLineEvents.Clear();
            _hostGlobalDialogueLineRunId = 0;
            _clientDialogueLineAck = 0;
            GuestInput.Clear();
            _hasGuestPosition = false;
            _guestSceneName = "";
            _pendingGuestInteractionRequestId = 0;
            _lastGuestInteractionRequestId = 0;
            _suppressGuestInteractUntilRelease = false;
            _hostRemoteInteractInjectedUntil = 0f;
            _hostDialogueStartedByMirroredGuestInteract = false;
            _pendingGuestInteractionAsset = "";
            _pendingGuestInteractionNode = "";
            _guestInteractionCaptureConsumed.Clear();
            _capturingGuestInteractionIterator = null;
            _capturingGuestPlayerInteraction = false;
            _guestInteractionCaptureArmed = false;
            _guestInteractHeld = false;
            _pendingGuestInteract = false;
            _pendingGuestInteractDeadline = 0f;
            _guestConfirmHeld = false;
            _guestConfirmBlockedUntilRelease = false;
            _lastGuestInputDialogueRunId = 0;
            _pendingGuestConfirm = false;
            _dialogueConfirmPending = false;
            _dialogueConfirmConsumedUntilRelease = false;
            _waitingChoiceConfirm = false;
            _waitingChoiceCommitId = 0;
            _waitingChoiceSessionId = 0;
            _waitingChoiceNode = "";
            _waitingChoiceDeadline = 0f;
            _status = reason;
            Logger.LogInfo("Guest session reset: " + reason);
        }

        private void ResetHostSession(string reason)
        {
            _connected = false;
            _lastHostPacket = 0f;
            _haveSequence = false;
            _lastSequence = 0;
            _hostDialogueRunId = 0;
            _lastAppliedHostDialogueRunId = 0;
            _hostDialogueEndSentForRunId = 0;
            _hostDialogueAsset = "";
            _hostDialogueNode = "";
            _hostDialogueWireNode = "";
            _hostDialogueWireNodeReady = false;
            _hostDialogueFallbackHoldUntil = 0f;
            _hostDialogueRunStream = "";
            _hostDialogueOption = 0;
            _pendingHostDialogueStart = false;
            _pendingHostDialogueLines.Clear();
            _clientDialogueLineAcks.Clear();
            _lastAppliedHostDialogueLineSequences.Clear();
            _claimedHostDialogueLineSequences.Clear();
            _hostDialogueLineEvents.Clear();
            _hostDialogueLineSequence = 0;
            _hostGlobalDialogueLineSequence = 0;
            _hostDialogueLineFingerprint = "";
            _hostGlobalDialogueLineRunId = 0;
            _lastAppliedHostDialogueLineSequence = 0;
            _clientDialogueLineAck = 0;
            _pendingGuestDialogueLineFingerprint = "";
            _lastGuestAutoAdvanceHostLineSequence = 0;
            _pendingHostDialogueConfirm = false;
            _pendingHostGlobalConfirm = false;
            _pendingHostConfirm = false;
            _hostOptionsActive = false;
            _hostOptionIndex = 0;
            _hostOptionNode = "";
            _hostOptionText = "";
            _hostOptionSessionId = 0;
            _hostChoiceCommitId = 0;
            _hostChoiceCommitSessionId = 0;
            _hostChoiceCommitIndex = 0;
            _hostChoiceCommitNode = "";
            _hostChoiceCommitText = "";
            _clientActiveOptionSessionId = 0;
            _lastAppliedChoiceSessionId = 0;
            _clientActiveOptionNode = "";
            _clientActiveOptionCount = 0;
            _pendingClientChoiceConfirm = false;
            _ignoreHostChoiceConfirmUntilRelease = false;
            _hasDeferredChoiceCallback = false;
            _deferredChoiceTarget = null;
            _waitingChoiceConfirm = false;
            _waitingChoiceCommitId = 0;
            _waitingChoiceSessionId = 0;
            _waitingChoiceNode = "";
            _waitingChoiceDeadline = 0f;
            _hostSceneName = "";
            _hostSceneBootstrapNodes.Clear();
            _hasHostPosition = false;
            _hostPosition = Vector3.zero;
            _hostInteractHeld = false;
            _hostConfirmHeld = false;
            _pendingHostInteract = false;
            _pendingGuestTvLineStream = "";
            _pendingGuestTvLineFingerprint = "";
            _pendingGuestTvLineText = "";
            _guestTvOverlayText = "";
            _pendingStorySnapshotId = 0;
            _pendingStoryFragmentCount = 0;
            _receivedStoryFragments = null;
            _pendingStoryVariables = null;
            _storyVariableSync = "waiting for host snapshot";
            if (_pendingClientInteractionRequestId != 0)
            {
                Logger.LogWarning("Discarding guest interaction request=" + _pendingClientInteractionRequestId +
                    " after host timeout; it will not be replayed into a new session");
                _pendingClientInteractionRequestId = 0;
                _pendingClientInteractionDeadline = 0f;
                _pendingClientInteractionAsset = "";
                _pendingClientInteractionNode = "";
            }
            if (global::Global.dialogue != null)
            {
                global::Global.dialogue.Stop();
                global::Global.dialogue.Clear();
            }
            _status = reason;
            Logger.LogInfo("Host session reset: " + reason);
        }
        private void ExpirePendingClientInteractionRequest()
        {
            if (!IsClient || _pendingClientInteractionRequestId == 0 ||
                Time.realtimeSinceStartup < _pendingClientInteractionDeadline) return;
            uint expiredRequestId = _pendingClientInteractionRequestId;
            string expiredAsset = _pendingClientInteractionAsset;
            string expiredNode = _pendingClientInteractionNode;
            _pendingClientInteractionRequestId = 0;
            _pendingClientInteractionDeadline = 0f;
            _pendingClientInteractionAsset = "";
            _pendingClientInteractionNode = "";
            _guestInteractionStatus = "expired request=" + expiredRequestId + " asset=" + expiredAsset +
                " node=" + expiredNode + " after 5 seconds without host acknowledgement";
            Logger.LogWarning("Expired stale guest Yarn interaction request=" + expiredRequestId +
                " asset=" + expiredAsset + " node=" + expiredNode + " after 5 seconds without host acknowledgement");
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
                _lastHostPacket = Time.realtimeSinceStartup;
                uint previousChoiceCommitId = _hostChoiceCommitId;
                uint previousOptionSessionId = _hostOptionSessionId;
                bool newHostDialogueRun = p.DialogueRunId != 0 &&
                    (_hostDialogueRunId == 0 || IsNewer(p.DialogueRunId, _hostDialogueRunId));
                bool hostDialogueNodeArrived = p.DialogueRunId != 0 && p.DialogueRunId == _hostDialogueRunId &&
                    string.IsNullOrEmpty(_hostDialogueNode) && !string.IsNullOrEmpty(p.DialogueNode);
                bool newChoiceCommit = p.ChoiceCommitId != 0 &&
                    (previousChoiceCommitId == 0 || IsNewer(p.ChoiceCommitId, previousChoiceCommitId));
                // A choice commit can be serialized before DialogueUI.SetOption's
                // callback has populated its semantic label. The host then keeps
                // sending the same commit ID with its now-known label. Accept that
                // metadata update so the guest can map the label to its local menu.
                bool choiceCommitTextArrived = p.ChoiceCommitId != 0 &&
                    p.ChoiceCommitId == previousChoiceCommitId &&
                    string.IsNullOrEmpty(_hostChoiceCommitText) &&
                    !string.IsNullOrEmpty(p.ChoiceCommitText);
                bool choiceCommitAvailable = newChoiceCommit || choiceCommitTextArrived;
                bool hostOptionSessionChanged = p.OptionSessionId != previousOptionSessionId;
                bool hostSceneChanged = !string.IsNullOrEmpty(_hostSceneName) &&
                    !string.IsNullOrEmpty(p.SceneName) && !SameChoiceNode(_hostSceneName, p.SceneName);
                bool hostConfirmRising = p.State.ConfirmDown && !_hostConfirmHeld;
                if (hostSceneChanged) _hostSceneBootstrapNodes.Clear();
                if (p.DialogueRunId != 0 && IsSceneBootstrapDialogue(p.DialogueAsset, p.DialogueNode, p.SceneName))
                    _hostSceneBootstrapNodes[p.DialogueAsset + "/" + p.DialogueNode] = 0;
                HostInput.CopyFrom(p.State);
                if (_pendingClientInteractionRequestId != 0 &&
                    (p.InteractionAckId == _pendingClientInteractionRequestId || IsNewer(p.InteractionAckId, _pendingClientInteractionRequestId)))
                {
                    _guestInteractionStatus = "host acknowledged request=" + p.InteractionAckId;
                    _pendingClientInteractionRequestId = 0;
                    _pendingClientInteractionDeadline = 0f;
                    _pendingClientInteractionAsset = "";
                    _pendingClientInteractionNode = "";
                }
                if (!p.State.ConfirmDown) _dialogueConfirmConsumedUntilRelease = false;
                if (newHostDialogueRun)
                {
                    _pendingHostDialogueConfirm = false;
                    _pendingHostGlobalConfirm = false;
                    _pendingHostConfirm = false;
                    _hostGlobalDialogueLineRunId = 0;
                }
                if (p.State.InteractDown && !_hostInteractHeld) _pendingHostInteract = true;
                _hostInteractHeld = p.State.InteractDown;
                _hostSceneName = p.SceneName;
                _hasHostPosition = p.HasPosition;
                _hostPosition = p.Position;
                _hostOptionsActive = p.OptionsActive;
                _hostOptionIndex = p.OptionIndex;
                _hostOptionNode = p.OptionNode;
                _hostOptionText = p.OptionText;
                _hostOptionSessionId = p.OptionSessionId;
                if (_clientChoiceSelectionRequestIndex >= 0 &&
                    (!p.OptionsActive || _hostOptionSessionId != _clientChoiceSelectionRequestSessionId ||
                     _hostOptionIndex == _clientChoiceSelectionRequestIndex))
                {
                    _clientChoiceSelectionRequestIndex = -1;
                    _clientChoiceSelectionRequestSessionId = 0;
                }
                for (int i = 0; i < p.DialogueLines.Count; i++)
                {
                    DialogueLineEvent incomingLine = p.DialogueLines[i];
                    DialogueLineEvent previousLine;
                    bool isNewEvent = !_hostDialogueLineEvents.TryGetValue(incomingLine.Stream, out previousLine) ||
                        IsNewer(incomingLine.Sequence, previousLine.Sequence);
                    if (isNewEvent)
                    {
                        _hostDialogueLineEvents[incomingLine.Stream] = incomingLine;
                        if (!newHostDialogueRun &&
                            string.Equals(incomingLine.Stream, "DialogueUI@Global", StringComparison.Ordinal) && previousLine != null &&
                            _hostGlobalDialogueLineRunId == p.DialogueRunId &&
                            _lastAppliedHostDialogueRunId == p.DialogueRunId)
                        {
                            uint previouslyAppliedSequence;
                            if (_lastAppliedHostDialogueLineSequences.TryGetValue(incomingLine.Stream, out previouslyAppliedSequence) &&
                                previouslyAppliedSequence == previousLine.Sequence &&
                                IsNewer(incomingLine.Sequence, previouslyAppliedSequence) &&
                                string.Equals(_pendingGuestDialogueLineFingerprint, previousLine.Fingerprint, StringComparison.Ordinal) &&
                                !string.Equals(_pendingGuestDialogueLineFingerprint, incomingLine.Fingerprint, StringComparison.Ordinal) &&
                                _lastGuestAutoAdvanceHostLineSequence != incomingLine.Sequence)
                            {
                                _lastGuestAutoAdvanceHostLineSequence = incomingLine.Sequence;
                                _pendingHostDialogueConfirm = true;
                                _pendingHostGlobalConfirm = ShouldMirrorHostConfirmToGlobalCoroutine();
                                _testDialogueConfirmDiagnostics = "auto-advance guest to host line sequence=" + incomingLine.Sequence;
                                Logger.LogInfo("Auto-advancing guest dialogue after host advanced to global line " + incomingLine.Sequence);
                            }
                        }
                        Logger.LogInfo("Received host dialogue line event: " + incomingLine.Stream + "#" +
                            incomingLine.Sequence + "/" + incomingLine.Fingerprint + " packet=" + p.Sequence);
                    }
                    if (string.Equals(incomingLine.Stream, "DialogueUI@Global", StringComparison.Ordinal) &&
                        _lastAppliedHostDialogueRunId == p.DialogueRunId)
                        _hostGlobalDialogueLineRunId = p.DialogueRunId;
                    _hostDialogueLineSequence = incomingLine.Sequence;
                    _hostDialogueLineFingerprint = incomingLine.Fingerprint;
                    if (IsTvDialogueLineStream(incomingLine.Stream))
                    {
                        _pendingGuestTvLineText = incomingLine.Text;
                        AcknowledgeGuestTvLine(incomingLine);
                    }
                }
                if (p.DialogueRunId != 0)
                {
                    _hostDialogueRunId = p.DialogueRunId;
                    _hostDialogueAsset = p.DialogueAsset ?? "";
                    _hostDialogueNode = p.DialogueNode ?? "";
                _hostDialogueRunStream = p.DialogueRunStream ?? "";
                    _hostDialogueOption = p.DialogueOption;
                    if ((newHostDialogueRun || hostDialogueNodeArrived) && !string.IsNullOrEmpty(_hostDialogueAsset) &&
                        !string.IsNullOrEmpty(_hostDialogueNode)) _pendingHostDialogueStart = true;
                    else if (newHostDialogueRun && string.IsNullOrEmpty(_hostDialogueAsset) &&
                        string.IsNullOrEmpty(_hostDialogueNode))
                    {
                        _pendingHostDialogueStart = false;
                        _pendingHostDialogueConfirm = false;
                        _pendingHostGlobalConfirm = false;
                        _pendingHostConfirm = false;
                        _pendingGuestDialogueLineFingerprint = "";
                        if (global::Global.dialogue != null)
                        {
                            global::Global.dialogue.Stop();
                            global::Global.dialogue.Clear();
                        }
                        _lastAppliedHostDialogueRunId = p.DialogueRunId;
                        SchedulePlayerInteractionCleanup();
                        Logger.LogInfo("Applied host dialogue end marker for run " + p.DialogueRunId);
                    }
                }
                if (hostOptionSessionChanged)
                {
                    // A confirm packet can be the line advance that opens the next
                    // Yarn choice. Do not reinterpret that same edge as a choice
                    // confirm merely because the newly opened choice is in this snapshot.
                    _pendingClientChoiceConfirm = false;
                    // If the host has opened a new choice but the guest's local
                    // iterator has not reached that session yet, let the generic
                    // story coroutine consume this confirm so it can catch up.
                    _pendingHostConfirm = p.OptionsActive &&
                        _clientActiveOptionSessionId != p.OptionSessionId;
                    _waitingChoiceConfirm = false;
                    _waitingChoiceCommitId = 0;
                    _waitingChoiceSessionId = 0;
                    _waitingChoiceNode = "";
                    _status = "connected to host";
                }
                if (choiceCommitAvailable)
                {
                    // A prior choice-wait timeout can leave a stale status even when the
                    // delayed authoritative commit arrives and is applied successfully.
                    _status = "connected to host";
                    _hostChoiceCommitId = p.ChoiceCommitId;
                    _hostChoiceCommitSessionId = p.ChoiceCommitSessionId;
                    _hostChoiceCommitIndex = p.ChoiceCommitIndex;
                    _hostChoiceCommitNode = p.ChoiceCommitNode;
                    _hostChoiceCommitText = p.ChoiceCommitText;
                }
                if (hostSceneChanged)
                {
                    _clientActiveOptionSessionId = 0;
                    _clientActiveOptionNode = "";
                    _clientActiveOptionCount = 0;
                    _lastAppliedChoiceSessionId = _hostOptionSessionId;
                    _pendingClientChoiceConfirm = false;
                    _pendingHostDialogueConfirm = false;
                    _pendingHostGlobalConfirm = false;
                    _pendingHostConfirm = false;
                    _hasDeferredChoiceCallback = false;
                    _deferredChoiceTarget = null;
                    _waitingChoiceConfirm = false;
                }
                bool hadDeferredChoiceCallback = _hasDeferredChoiceCallback;
                if (choiceCommitAvailable) TryApplyDeferredChoiceCallback(p);
                if (choiceCommitAvailable && !hadDeferredChoiceCallback && !hostSceneChanged)
                {
                    _pendingClientChoiceConfirm = true;
                    _pendingHostConfirm = false;
                }
                if (p.Variables != null) ApplyStoryVariables(p.Variables);
                if (p.State.ConfirmDown) _testHostConfirmPackets++;
                if (p.State.ConfirmDown && !_hostConfirmHeld && !newHostDialogueRun && !newChoiceCommit && !hostOptionSessionChanged &&
                    !_ignoreHostChoiceConfirmUntilRelease)
                {
                    if (p.OptionsActive)
                    {
                        _waitingChoiceConfirm = true;
                        _waitingChoiceCommitId = previousChoiceCommitId;
                        _waitingChoiceSessionId = p.OptionSessionId;
                        _waitingChoiceNode = p.OptionNode ?? "";
                        _waitingChoiceDeadline = Time.realtimeSinceStartup + 1.5f;
                    }
                    else if (_clientActiveOptionSessionId == 0 &&
                        _hostOptionSessionId == _lastAppliedChoiceSessionId) _pendingHostConfirm = true;
                }
                if (_waitingChoiceConfirm && newChoiceCommit &&
                    IsNewer(p.ChoiceCommitId, _waitingChoiceCommitId) &&
                    SameChoiceSession(_waitingChoiceSessionId, p.ChoiceCommitSessionId,
                        _waitingChoiceNode, p.ChoiceCommitNode))
                {
                    _waitingChoiceConfirm = false;
                    if (!hadDeferredChoiceCallback && !hostSceneChanged)
                        _pendingClientChoiceConfirm = true;
                }
                if (newChoiceCommit && p.State.ConfirmDown)
                    _ignoreHostChoiceConfirmUntilRelease = true;
                else if (!p.State.ConfirmDown)
                    _ignoreHostChoiceConfirmUntilRelease = false;
                if (!newHostDialogueRun && hostConfirmRising && HasRunningDialogue() && !ClientOptionIteratorActive())
                {
                    _pendingHostDialogueConfirm = true;
                    _pendingHostGlobalConfirm = ShouldMirrorHostConfirmToGlobalCoroutine();
                    _loggedPendingInputNext = false;
                    _loggedPendingGlobalConfirm = false;
                    _testDialogueConfirmDiagnostics = "host dialogue-confirm edge queued";
                    Logger.LogInfo("Queued one host dialogue-confirm edge for guest InputNext");
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
            if (_hasDeferredChoiceCallback && Time.realtimeSinceStartup >= _deferredChoiceDeadline)
            {
                int localIndex = FindOptionLabel(_clientActiveOptionLabels, _hostOptionText);
                if (localIndex >= 0) CompleteDeferredChoiceCallback(localIndex, 0);
                else
                {
                    _hasDeferredChoiceCallback = false;
                    _deferredChoiceTarget = null;
                    _testChoiceCallbackStatus = "host option label not found; deferred choice held";
                    Logger.LogWarning(_testChoiceCallbackStatus + ": " + _hostOptionText);
                }
            }
        }

        private bool TryReceive(out byte[] bytes, out IPEndPoint from)
        {
            bytes = null;
            from = new IPEndPoint(IPAddress.Any, 0);
            if (_socket == null) return false;
            try
            {
                if (!_socket.Poll(0, SelectMode.SelectRead)) return false;
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

        private void RecordStoryVariableDiagnosticSnapshot(Dictionary<string, float> variables)
        {
            string currentHash = StoryVariableHash(variables);
            if (string.Equals(currentHash, _lastStoryVariableDiagnosticHash, StringComparison.Ordinal)) return;
            string changes = DescribeStoryVariableChanges(_lastStoryVariableDiagnosticSnapshot, variables);
            Logger.LogInfo("Story variable snapshot changed: role=" + RoleName + " hash=" +
                (_lastStoryVariableDiagnosticHash.Length == 0 ? "baseline" : _lastStoryVariableDiagnosticHash) +
                "->" + currentHash + " changes=" + changes);
            _lastStoryVariableDiagnosticHash = currentHash;
            _lastStoryVariableDiagnosticSnapshot = new Dictionary<string, float>(variables);
        }

        private static string DescribeStoryVariableChanges(Dictionary<string, float> previous, Dictionary<string, float> current)
        {
            if (previous == null) return "baseline";
            List<string> keys = new List<string>(previous.Keys);
            foreach (string key in current.Keys) if (!previous.ContainsKey(key)) keys.Add(key);
            keys.Sort(StringComparer.Ordinal);
            List<string> changes = new List<string>();
            int total = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                float oldValue;
                float newValue;
                bool hadOld = previous.TryGetValue(key, out oldValue);
                bool hasNew = current.TryGetValue(key, out newValue);
                if (hadOld && hasNew && BitConverter.ToInt32(BitConverter.GetBytes(oldValue), 0) ==
                    BitConverter.ToInt32(BitConverter.GetBytes(newValue), 0)) continue;
                total++;
                if (changes.Count < 32)
                    changes.Add(key + ":" + (hadOld ? oldValue.ToString("R", CultureInfo.InvariantCulture) : "<missing>") +
                        "->" + (hasNew ? newValue.ToString("R", CultureInfo.InvariantCulture) : "<missing>"));
            }
            if (total == 0) return "no value differences";
            if (total > changes.Count) changes.Add("...(+" + (total - changes.Count).ToString(CultureInfo.InvariantCulture) + ")");
            return string.Join(";", changes.ToArray());
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
                ApplyRavineLogSlideVisualState(hostVariables);
            }
            catch (Exception e) { _storyVariableSync = "apply failed: " + e.GetType().Name; }
        }

        private void ApplyRavineLogSlideVisualState(Dictionary<string, float> hostVariables)
        {
            if (hostVariables == null || !string.Equals(Application.loadedLevelName, "Ravine", StringComparison.Ordinal)) return;
            float slideValue;
            if (!hostVariables.TryGetValue("ravine_log_slide", out slideValue) || slideValue <= 0f) return;

            try
            {
                Type stompType = typeof(global::Continuity).Assembly.GetType("RavineLogStomp", false);
                if (stompType == null) return;
                FieldInfo variableField = AccessTools.Field(stompType, "varName");
                MethodInfo startMethod = AccessTools.Method(stompType, "Start", null, null);
                if (variableField == null || startMethod == null) return;

                UnityEngine.Object[] triggers = UnityEngine.Object.FindObjectsOfType(stompType);
                for (int i = 0; i < triggers.Length; i++)
                {
                    object trigger = triggers[i];
                    if (trigger == null || !string.Equals(Convert.ToString(variableField.GetValue(trigger), CultureInfo.InvariantCulture),
                        "ravine_log_slide", StringComparison.Ordinal)) continue;

                    // The game's Start path applies only the settled world state when
                    // ravine_log_slide is set. It does not run Trigger(), which also
                    // launches the local Mae into the host-only fly-off cutscene.
                    Behaviour behaviour = trigger as Behaviour;
                    if (behaviour != null && !behaviour.enabled) continue;
                    startMethod.Invoke(trigger, null);
                    Logger.LogInfo("Applied host Ravine log settled visual state on guest; componentEnabled=" +
                        (behaviour == null ? "unknown" : behaviour.enabled.ToString()));
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("Guest Ravine log visual-state replay failed: " + e.GetType().Name);
            }
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

        private bool ClientOptionIteratorActive()
        {
            if (!IsClient || _testOptionImplementation == null || _clientActiveOptionSessionId == 0 ||
                _clientActiveOptionSessionId != _hostOptionSessionId) return false;
            try
            {
                FieldInfo activeField = AccessTools.Field(_testOptionImplementation.GetType(), "runningOptions");
                return activeField != null && (bool)activeField.GetValue(_testOptionImplementation);
            }
            catch { return false; }
        }

        internal static float SharedAxis(string axis, float local)
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.Connected) return local;
            if (axis != "Horizontal" && axis != "Vertical") return local;
            if (plugin._readingLocalInput || plugin._botMoveFrames <= 0) return local;
            return axis == "Horizontal" ? plugin._botHorizontal : plugin._botVertical;
        }

        internal static bool SharedJumpDown(bool local)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return local;
            bool botJump = p._botJumpFrame == Time.frameCount;
            if (botJump && p._testBotJumpInjectedFrame != Time.frameCount)
            {
                p._testBotJumpInjectedFrame = Time.frameCount;
                p._testBotJumpInjected++;
            }
            return local || botJump;
        }

        internal static bool SharedJumpHeld(bool local)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return local;
            return local;
        }

        internal static bool SharedInteract(bool local)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return local;
            bool botInteract = p._botInteractFrame == Time.frameCount;
            bool remoteInteract = p.IsHost && p._pendingGuestInteract &&
                Time.realtimeSinceStartup >= p._pendingGuestInteractDeadline &&
                !p._suppressGuestInteractUntilRelease && p._pendingGuestInteractionRequestId == 0;
            if (remoteInteract)
                p._hostRemoteInteractInjectedUntil = Time.realtimeSinceStartup + 0.5f;
            return p.IsHost ? (local || remoteInteract || botInteract) : HostInput.InteractDown;
        }

        private static void RewiredButtonDownPostfix(Rewired.Player __instance, object[] __args, ref bool __result)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected || __args == null || __args.Length == 0 ||
                !string.Equals(__args[0] as string, "Interact", StringComparison.OrdinalIgnoreCase)) return;
            p._testRewiredInteractCalls++;
            bool botInteract = p._botInteractFrame == Time.frameCount;
            if (botInteract) p._testBotRewiredInteractCalls++;
            if (p._readingLocalInput)
            {
                if (botInteract)
                {
                    __result = true;
                    p._testBotRewiredInteractDownQueries++;
                }
                // Host-replayed confirms are not local Interact edges. Re-arm only when the
                // connected guest is idle and a local or QA-bot Interact edge is present.
                bool freshGuestInteract = p.IsClient && (__result || botInteract) &&
                    !HasRunningDialogue() && !p._playerInteractionCleanupPending;
                if (freshGuestInteract && !p._guestInteractionCaptureArmed)
                {
                    p._guestInteractionCaptureArmed = true;
                    p.Logger.LogInfo("Re-armed guest interaction capture after fresh Interact input");
                }
                return;
            }
            bool remoteInteract = false;
            if (p.IsHost && p._pendingGuestInteract && Time.realtimeSinceStartup >= p._pendingGuestInteractDeadline)
            {
                p._pendingGuestInteract = false;
                p._pendingGuestInteractDeadline = 0f;
                if (!p._suppressGuestInteractUntilRelease && p._pendingGuestInteractionRequestId == 0)
                {
                    remoteInteract = true;
                    p._hostRemoteInteractInjectedUntil = Time.realtimeSinceStartup + 0.5f;
                }
            }
            else if (p.IsClient && p._pendingHostInteract)
            {
                p._pendingHostInteract = false;
                remoteInteract = true;
            }
            __result = __result || botInteract || remoteInteract;
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

        private static bool GuestNumericVariableWritePrefix()
        {
            CoopPlugin plugin = Instance;
            if (plugin == null || !plugin.IsClient || !plugin.Connected) return true;
            plugin._testGuestStoryWritesBlocked++;
            return false;
        }

        private static bool HasRunningDialogue()
        {
            if (IsDialogueRunning(global::Global.dialogue)) return true;
            try
            {
                UnityEngine.Object[] dialogues = UnityEngine.Object.FindObjectsOfType(typeof(global::Dialogue));
                for (int i = 0; i < dialogues.Length; i++)
                    if (IsDialogueRunning(dialogues[i])) return true;
            }
            catch { }
            return false;
        }

        private static bool HasRunningDialogue(object implementation)
        {
            try
            {
                PropertyInfo dialogueProperty = implementation == null ? null :
                    AccessTools.Property(implementation.GetType(), "dialogue");
                object dialogue = dialogueProperty == null ? null : dialogueProperty.GetValue(implementation, null);
                if (IsDialogueRunning(dialogue)) return true;
            }
            catch { }
            return HasRunningDialogue();
        }

        private static bool IsDialogueRunning(object dialogue)
        {
            if (dialogue == null) return false;
            try
            {
                PropertyInfo dialogueRunning = AccessTools.Property(dialogue.GetType(), "running");
                if (dialogueRunning != null && (bool)dialogueRunning.GetValue(dialogue, null)) return true;
                FieldInfo runnerField = AccessTools.Field(dialogue.GetType(), "dialogueRunner");
                object runner = runnerField == null ? null : runnerField.GetValue(dialogue);
                PropertyInfo runnerRunning = runner == null ? null : AccessTools.Property(runner.GetType(), "isDialogueRunning");
                return runnerRunning != null && (bool)runnerRunning.GetValue(runner, null);
            }
            catch { return false; }
        }

        private static bool DialogueControlStatePrefix(object __instance, ref bool __result)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected || !p.IsClient || __instance == null ||
                p._hostDialogueRunId == 0 || p._lastAppliedHostDialogueRunId != p._hostDialogueRunId)
                return true;

            try
            {
                PropertyInfo dialogueProperty = AccessTools.Property(__instance.GetType(), "dialogue");
                object dialogue = dialogueProperty == null ? null : dialogueProperty.GetValue(__instance, null);
                if (!object.ReferenceEquals(dialogue, global::Global.dialogue) || !IsDialogueRunning(dialogue))
                    return true;
                TextAsset sceneAsset = CurrentSceneDialogueAsset();
                if (sceneAsset == null || !string.Equals(sceneAsset.name, p._hostDialogueAsset, StringComparison.Ordinal))
                    return true;

                __result = true;
                p._testGuestDialogueControlStatus = "forced foreground input for host run " + p._hostDialogueRunId;
                if (p._lastGuestDialogueControlOverrideRunId != p._hostDialogueRunId)
                {
                    p._lastGuestDialogueControlOverrideRunId = p._hostDialogueRunId;
                    p.Logger.LogInfo("Forced guest foreground dialogue input for host run " + p._hostDialogueRunId +
                        " asset=" + p._hostDialogueAsset + " node=" + p._hostDialogueNode);
                }
                return false;
            }
            catch (Exception e)
            {
                p._testGuestDialogueControlStatus = "override error: " + e.GetType().Name;
                return true;
            }
        }

        private static bool ShouldMirrorHostConfirmToGlobalCoroutine()
        {
            // The opening poem has a separate game coroutine that also waits for confirm.
            // Ordinary Yarn dialogue should receive one confirm through InputNext only.
            return string.Equals(Application.loadedLevelName, "IntroPoem", StringComparison.Ordinal);
        }
        private static void IntroPoemMoveNextPrefix(object __instance)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected || __instance == null ||
                !string.Equals(Application.loadedLevelName, "IntroPoem", StringComparison.Ordinal)) return;
            p._insideIntroPoemStartMoveNext = true;
            try
            {
                FieldInfo pc = AccessTools.Field(__instance.GetType(), "$PC");
                if (pc != null) p._testIntroPoemProgramCounter = (int)pc.GetValue(__instance);
            }
            catch { }
        }

        private static void IntroPoemMoveNextPostfix(object __instance)
        {
            CoopPlugin p = Instance;
            if (p == null || __instance == null) return;
            try
            {
                FieldInfo pc = AccessTools.Field(__instance.GetType(), "$PC");
                if (pc != null) p._testIntroPoemProgramCounter = (int)pc.GetValue(__instance);
            }
            catch { }
            p._insideIntroPoemStartMoveNext = false;
        }
        private static bool DialogueInputNextPrefix(object __instance, ref bool __result)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected || __instance == null) return true;
            if (p.IsHost) p.PollHost(); else p.PollClient();
            unchecked { p._testDialogueInputNextCalls++; }
            bool runningOptions = false;
            try
            {
                FieldInfo optionsField = AccessTools.Field(__instance.GetType(), "runningOptions");
                runningOptions = optionsField != null && (bool)optionsField.GetValue(__instance);
            }
            catch { return true; }

            if (p.IsClient && p._pendingHostDialogueConfirm)
            {
                unchecked { p._testPendingInputNextCalls++; }
                p._testDialogueConfirmDiagnostics = "InputNext reached; active=" + HasRunningDialogue(__instance) +
                    " options=" + runningOptions;
                if (!p._loggedPendingInputNext)
                {
                    p._loggedPendingInputNext = true;
                    p.Logger.LogInfo("Pending host dialogue edge reached DialogueImplementation.InputNext: active=" +
                        HasRunningDialogue(__instance) + " options=" + runningOptions);
                }
            }
            if (runningOptions) return true;

            if (p.IsHost && HasRunningDialogue(__instance) &&
                string.Equals(p._hostDialogueRunStream, "Dialogue@Global", StringComparison.Ordinal) &&
                p._hostGlobalDialogueLineSequence != 0 &&
                !p.DialogueLineAcked("DialogueUI@Global", p._hostGlobalDialogueLineSequence))
            {
                __result = false;
                p._testDialogueConfirmDiagnostics = "host waiting for guest line ack stream=DialogueUI@Global sequence=" + p._hostGlobalDialogueLineSequence;
                if (p._pendingGuestConfirm) p.Logger.LogInfo(p._testDialogueConfirmDiagnostics + " guestConfirmPending=true");
                return false;
            }
            if (HasRunningDialogue(__instance) && p._pendingBotConfirm)
            {
                p._pendingBotConfirm = false;
                p._botInteractFrame = -1;
                p._testBotConfirmConsumed++;
                p._dialogueConfirmConsumedUntilRelease = true;
                __result = true;
                p.RecordDialogueConfirm("bot");
                return false;
            }
            if (HasRunningDialogue(__instance) && p.IsClient && p._pendingHostDialogueConfirm)
            {
                p._pendingHostDialogueConfirm = false;
                p._pendingHostConfirm = false;
                p._testDialogueConfirmDiagnostics = "consumed by DialogueImplementation.InputNext";
                p._botInteractFrame = -1;
                p._dialogueConfirmConsumedUntilRelease = true;
                __result = true;
                p.RecordDialogueConfirm("host-dialogue-edge");
                return false;
            }
            if (HasRunningDialogue(__instance) && p._dialogueConfirmConsumedUntilRelease)
            {
                __result = false;
                return false;
            }
            if (HasRunningDialogue(__instance) && p.IsHost && p._pendingGuestConfirm)
            {
                p._pendingGuestConfirm = false;
                p._pendingGuestInteract = false;
                p._dialogueConfirmConsumedUntilRelease = true;
                __result = true;
                p._hostConfirmBroadcastUntil = Time.realtimeSinceStartup + 0.15f;
                p.RecordDialogueConfirm("guest");
                return false;
            }
            if (HasRunningDialogue(__instance) && p.IsClient && p._pendingHostConfirm)
            {
                p._pendingHostConfirm = false;
                p._pendingHostInteract = false;
                p._dialogueConfirmConsumedUntilRelease = true;
                __result = true;
                p.RecordDialogueConfirm("host");
                return false;
            }
            return true;
        }

        private void RecordDialogueConfirm(string source)
        {
            _testConfirmObserved++;
            if (_testLastConfirmLogFrame == Time.frameCount) return;
            _testLastConfirmLogFrame = Time.frameCount;
            _testLastConfirm = "frame=" + Time.frameCount + " time=" +
                Time.realtimeSinceStartup.ToString("F3", CultureInfo.InvariantCulture) + " source=" + source + "-dialogue";
            Logger.LogInfo("Dialogue confirm accepted: " + _testLastConfirm + " " + DialogueRunnerStatus());
        }

        private static void AxisPostfix(string axisName, ref float __result)
        {
            CoopPlugin p = Instance;
            __result = SharedAxis(axisName, __result);
            if (p != null && p.Connected)
            {
                p._testAxisResult = axisName + ":" + __result.ToString("F2", CultureInfo.InvariantCulture);
                if (axisName == "Horizontal") p._testHorizontalAxis = __result.ToString("F2", CultureInfo.InvariantCulture);
                else if (axisName == "Vertical") p._testVerticalAxis = __result.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        private static string PlayerBasisStatus(global::Player player)
        {
            FieldInfo xField = AccessTools.Field(typeof(global::Player), "moveXAxis");
            FieldInfo yField = AccessTools.Field(typeof(global::Player), "moveYAxis");
            if (xField == null || yField == null) return "unavailable";
            try
            {
                Vector3 x = (Vector3)xField.GetValue(player);
                Vector3 y = (Vector3)yField.GetValue(player);
                return x.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                    x.y.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                    y.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                    y.y.ToString("F2", CultureInfo.InvariantCulture);
            }
            catch { return "unavailable"; }
        }

        private static void MoveVectorPostfix(global::Player __instance, ref Vector3 __result)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected || __instance == null) return;
            p._testMoveVectorCalls++;
            p._testMoveVector = __result.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                __result.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
                __result.z.ToString("F2", CultureInfo.InvariantCulture) +
                " canMove=" + __instance.canMove +
                " left=" + __instance.canMoveLeft +
                " right=" + __instance.canMoveRight +
                " state=" + __instance.state;
        }

        private static void MoveXPrefix(global::Player __instance, ref float __0)
        {
            CoopPlugin p = Instance;
            if (p != null && p.Connected && p.ConstrainPlayerMoveX(__instance, ref __0))
                p._testRemoteSeparationBlocks++;
        }

        private bool ConstrainPlayerMoveX(global::Player player, ref float movementX)
        {
            bool hasRemote = IsHost ? _hasGuestPosition : _hasHostPosition;
            Vector3 remotePosition = IsHost ? _guestPosition : _hostPosition;
            string remoteScene = IsHost ? _guestSceneName : _hostSceneName;
            if (!hasRemote || player == null || !player.canMove ||
                !string.Equals(Application.loadedLevelName, remoteScene, StringComparison.Ordinal)) return false;

            Vector3 offset = player.transform.position - remotePosition;
            float bufferedRadius = RemotePlayerSeparationRadius + RemotePlayerSeparationNetworkMargin;
            float horizontalLimitSquared = bufferedRadius * bufferedRadius - offset.y * offset.y;
            if (horizontalLimitSquared <= 0f) return false;

            float minimumHorizontalGap = (float)Math.Sqrt(horizontalLimitSquared);
            float horizontalGap = offset.x;
            if (Math.Abs(horizontalGap) < 0.0001f || movementX == 0f) return false;

            if (Math.Abs(horizontalGap) < minimumHorizontalGap)
            {
                if (horizontalGap * movementX < 0f)
                {
                    movementX = 0f;
                    return true;
                }
                return false;
            }

            if (horizontalGap > minimumHorizontalGap && movementX < 0f && horizontalGap + movementX < minimumHorizontalGap)
            {
                movementX = minimumHorizontalGap - horizontalGap;
                return true;
            }
            if (horizontalGap < -minimumHorizontalGap && movementX > 0f && horizontalGap + movementX > -minimumHorizontalGap)
            {
                movementX = -minimumHorizontalGap - horizontalGap;
                return true;
            }
            return false;
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
            if (f == null) return;
            bool local = (bool)f.GetValue(__instance);
            bool shared = SharedInteract(local);
            CoopPlugin p = Instance;
            if (p != null)
            {
                p._testInteractSetCalls++;
                if (!local && shared && p._botInteractFrame == Time.frameCount)
                    p._testBotInteractInjected++;
            }
            f.SetValue(__instance, shared);
        }

        private static void ConfirmInputPostfix(ref bool __result)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected) return;
            if (p._insideIntroPoemStartMoveNext)
            {
                bool poemConfirm = p.IsHost
                    ? (__result || p._pendingBotIntroPoemConfirm || p._lastLocalInput.ConfirmDown || GuestInput.ConfirmDown)
                    : HostInput.ConfirmDown;
                p._testIntroPoemConfirmQueries++;
                if (poemConfirm)
                {
                    __result = true;
                    p._pendingBotIntroPoemConfirm = false;
                    p._pendingBotConfirm = false;
                    if (!p.IsHost)
                    {
                        p._pendingHostGlobalConfirm = false;
                        p._pendingHostConfirm = false;
                    }
                    p._testIntroPoemConfirmStatus = "accepted in IntroPoemBrain.MoveNext by " +
                        (p.IsHost ? "host/bot input" : "host input");
                }
                else
                {
                    __result = false;
                    p._testIntroPoemConfirmStatus = "waiting inside IntroPoemBrain.MoveNext";
                }
                return;
            }            if (p._dialogueConfirmConsumedUntilRelease && HasRunningDialogue() &&
                !(p.IsClient && p._pendingHostGlobalConfirm))
            {
                __result = false;
                return;
            }
            bool local = __result;
            if (p.IsHost) p.PollHost(); else p.PollClient();
            string source = "none";
            if (p.IsClient && p._pendingHostDialogueConfirm)
            {
                unchecked { p._testPendingGlobalConfirmCalls++; }
                bool dialogueActive = HasRunningDialogue();
                bool optionIteratorActive = p.ClientOptionIteratorActive();
                p._testDialogueConfirmDiagnostics = "Global confirm reached; active=" + dialogueActive +
                    " optionIterator=" + optionIteratorActive;
                if (!p._loggedPendingGlobalConfirm)
                {
                    p._loggedPendingGlobalConfirm = true;
                    p.Logger.LogInfo("Pending host dialogue edge reached Global confirm: active=" + dialogueActive +
                        " optionIterator=" + optionIteratorActive);
                }
            }
            // Some game coroutines (notably IntroPoemBrain.Start) and Yarn both
            // wait for confirms. Separate pending edges let one host confirm
            // advance each local coroutine without consuming Yarn's edge.
            if (p.IsClient && HasRunningDialogue() && !p.ClientOptionIteratorActive())
            {
                if (p._pendingHostGlobalConfirm)
                {
                    p._pendingHostGlobalConfirm = false;
                    p._pendingHostConfirm = false;
                    p._dialogueConfirmConsumedUntilRelease = true;
                    __result = true;
                    source = "host-dialogue-edge";
                    p._testDialogueConfirmDiagnostics = "consumed by Global confirm";
                }
                else
                {
                    __result = false;
                    return;
                }
            }
            else if (p.ClientOptionIteratorActive())
            {
                // The generic confirm level also advances story coroutines. A
                // guest choice is confirmed separately by the injected Yarn
                // input only after its matching host choice-commit packet arrives.
                __result = false;
                source = "guest-choice-await-host-commit";
            }
            else if (p.IsHost && p._pendingGuestConfirm)
            {
                p._pendingGuestConfirm = false;
                __result = true;
                p._hostConfirmBroadcastUntil = Time.realtimeSinceStartup + 0.15f;
                source = "guest";
            }
            else if (p._pendingBotConfirm)
            {
                p._pendingBotConfirm = false;
                p._testBotConfirmConsumed++;
                __result = true;
                source = "bot";
            }
            else if (p.IsClient && p._pendingHostConfirm)
            {
                p._pendingHostConfirm = false;
                __result = true;
                source = "host";
            }
            else if (p.IsHost && Time.realtimeSinceStartup < p._suppressLocalConfirmUntil)
            {
                __result = false;
            }
            else
            {
                // Global.IsConfirmOrInteractDown also drives non-Yarn story
                // coroutines such as IntroPoemBrain.Start. Preserve the host's
                // shared confirm level there; edge-based pendingHostConfirm can
                // be suppressed on the same packet as a committed Yarn choice.
                __result = p.IsHost ? (local || GuestInput.ConfirmDown) : HostInput.ConfirmDown;
                if (__result) source = p.IsHost ? "local" : "host-level";
            }
            if (__result)
            {
                if (HasRunningDialogue()) p._dialogueConfirmConsumedUntilRelease = true;
                p._testConfirmObserved++;
                if (p._testLastConfirmLogFrame != Time.frameCount)
                {
                    p._testLastConfirmLogFrame = Time.frameCount;
                    p._testLastConfirm = "frame=" + Time.frameCount + " time=" +
                        Time.realtimeSinceStartup.ToString("F3", CultureInfo.InvariantCulture) + " source=" + source;
                    p.Logger.LogInfo("Confirm accepted: " + p._testLastConfirm + " " + DialogueRunnerStatus());
                }
            }
        }

        private static void DialogueConfirmLateUpdatePostfix(object __instance)
        {
            CoopPlugin p = Instance;
            if (p == null || !p.Connected || __instance == null) return;
            try
            {
                bool activeOptions = p.IsHost ? p._hostOptionsActive : p._clientActiveOptionSessionId != 0;
                FieldInfo activeField = AccessTools.Field(__instance.GetType(), "runningOptions");
                if (activeField != null) activeOptions = (bool)activeField.GetValue(__instance);
                bool injectHostConfirm = p.IsHost && activeOptions && p._dialogueConfirmPending && p._optionConfirmReady;
                int clientCommitIndex = FindOptionLabel(p._clientActiveOptionLabels, p._hostChoiceCommitText);
                bool injectClientChoiceConfirm = p.IsClient && activeOptions && p._pendingClientChoiceConfirm && p._optionConfirmReady &&
                    p._clientActiveOptionSessionId == p._hostChoiceCommitSessionId &&
                    SameChoiceNode(p._clientActiveOptionNode, p._hostChoiceCommitNode) &&
                    clientCommitIndex >= 0 && clientCommitIndex < p._clientActiveOptionCount;
                if (injectHostConfirm || injectClientChoiceConfirm)
                {
                    FieldInfo inputConfirm = AccessTools.Field(__instance.GetType(), "inputConfirm");
                    if (inputConfirm != null)
                    {
                        if (injectClientChoiceConfirm)
                        {
                            FieldInfo indexField = AccessTools.Field(__instance.GetType(), "currentOption");
                            if (indexField != null) indexField.SetValue(__instance, clientCommitIndex);
                        }
                        inputConfirm.SetValue(__instance, true);
                        p._testDialogueConfirmInjected++;
                    }
                }
            }
            catch { }
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
        internal int ChoiceRequestIndex = -1;
        internal uint ChoiceRequestSessionId;
        internal bool HasAnimation;
        internal int AnimationHash;
        internal float AnimationTime;
        internal int Facing;
        internal bool Walking;

        internal void CopyFrom(InputState other)
        {
            Horizontal = other.Horizontal;
            Vertical = other.Vertical;
            JumpDown = other.JumpDown;
            JumpHeld = other.JumpHeld;
            InteractDown = other.InteractDown;
            InteractHeld = other.InteractHeld;
            ConfirmDown = other.ConfirmDown;
            ChoiceRequestIndex = other.ChoiceRequestIndex;
            ChoiceRequestSessionId = other.ChoiceRequestSessionId;
            HasAnimation = other.HasAnimation;
            AnimationHash = other.AnimationHash;
            AnimationTime = other.AnimationTime;
            Facing = other.Facing;
            Walking = other.Walking;
        }

        internal void Clear()
        {
            Horizontal = Vertical = 0f;
            JumpDown = JumpHeld = InteractDown = InteractHeld = ConfirmDown = false;
            ChoiceRequestIndex = -1;
            ChoiceRequestSessionId = 0;
            HasAnimation = false;
            AnimationHash = 0;
            AnimationTime = 0f;
            Facing = 0;
            Walking = false;
        }

        internal void ClearEdges()
        {
            JumpDown = InteractDown = ConfirmDown = false;
        }
    }

    internal sealed class DialogueLineEvent
    {
        internal readonly string Stream;
        internal readonly uint Sequence;
        internal readonly string Fingerprint;
        internal readonly string Text;

        internal DialogueLineEvent(string stream, uint sequence, string fingerprint, string text)
        {
            Stream = stream ?? "unknown";
            Sequence = sequence;
            Fingerprint = fingerprint ?? "";
            Text = text ?? "";
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
        internal string OptionText;
        internal uint OptionSessionId;
        internal uint ChoiceCommitId;
        internal uint ChoiceCommitSessionId;
        internal int ChoiceCommitIndex;
        internal string ChoiceCommitNode;
        internal string ChoiceCommitText;
        internal uint DialogueRunId;
        internal int DialogueOption;
        internal string DialogueAsset;
        internal string DialogueNode;
        internal string DialogueRunStream;
        internal List<DialogueLineEvent> DialogueLines;
        internal Dictionary<string, uint> DialogueLineAcks;
        internal uint InteractionRequestId;
        internal string InteractionAsset;
        internal string InteractionNode;
        internal uint InteractionAckId;
        internal uint InputDialogueRunId;
        private const byte ProtocolVersion = 18;
        private const int MaxPacketBytes = 1200;
        private const int FragmentHeaderBytes = 13;

        internal static byte[] Create(byte type, uint sequence, InputState state,
            string sceneName, bool hasPosition, Vector3 position, Dictionary<string, float> variables,
            bool optionsActive, int optionIndex, string optionNode, string optionText,
            uint optionSessionId, uint choiceCommitId, uint choiceCommitSessionId,
            int choiceCommitIndex, string choiceCommitNode, string choiceCommitText,
            uint dialogueRunId, int dialogueOption, string dialogueAsset, string dialogueNode,
            string dialogueRunStream, List<DialogueLineEvent> dialogueLines, Dictionary<string, uint> dialogueLineAcks,
            uint interactionRequestId, string interactionAsset, string interactionNode, uint interactionAckId,
            uint inputDialogueRunId)
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
                writer.Write(state.ChoiceRequestIndex);
                writer.Write(state.ChoiceRequestSessionId);
                writer.Write(state.HasAnimation && state.AnimationHash != 0);
                writer.Write(state.AnimationHash);
                writer.Write(state.AnimationTime);
                writer.Write(state.Facing);
                writer.Write(state.Walking);
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
                if (dialogueLineAcks == null) dialogueLineAcks = new Dictionary<string, uint>();
                if (dialogueLineAcks.Count > byte.MaxValue) throw new InvalidOperationException("Too many dialogue line acknowledgements for packet.");
                writer.Write((byte)dialogueLineAcks.Count);
                List<string> ackStreams = new List<string>(dialogueLineAcks.Keys);
                ackStreams.Sort(StringComparer.Ordinal);
                for (int i = 0; i < ackStreams.Count; i++)
                {
                    byte[] streamBytes = Encoding.UTF8.GetBytes(ackStreams[i]);
                    if (streamBytes.Length > 255) throw new InvalidOperationException("Dialogue UI stream name too long.");
                    writer.Write((byte)streamBytes.Length);
                    writer.Write(streamBytes);
                    writer.Write(dialogueLineAcks[ackStreams[i]]);
                }
                byte[] interactionAssetBytes = Encoding.UTF8.GetBytes(interactionAsset ?? "");
                byte[] interactionNodeBytes = Encoding.UTF8.GetBytes(interactionNode ?? "");
                if (interactionAssetBytes.Length > 255 || interactionNodeBytes.Length > 255)
                    throw new InvalidOperationException("Interaction asset or node name too long.");
                writer.Write(interactionRequestId);
                writer.Write((byte)interactionAssetBytes.Length);
                writer.Write(interactionAssetBytes);
                writer.Write((byte)interactionNodeBytes.Length);
                writer.Write(interactionNodeBytes);
                writer.Write(interactionAckId);
                writer.Write(inputDialogueRunId);
                if (type == 2)
                {
                    writer.Write(optionsActive);
                    writer.Write(optionIndex);
                    byte[] nodeBytes = Encoding.UTF8.GetBytes(optionNode ?? "");
                    if (nodeBytes.Length > 255) throw new InvalidOperationException("Dialogue node name too long for co-op packet.");
                    writer.Write((byte)nodeBytes.Length);
                    writer.Write(nodeBytes);
                    byte[] optionTextBytes = Encoding.UTF8.GetBytes(optionText ?? "");
                    if (optionTextBytes.Length > 255) throw new InvalidOperationException("Dialogue option text too long for co-op packet.");
                    writer.Write((byte)optionTextBytes.Length);
                    writer.Write(optionTextBytes);
                    writer.Write(optionSessionId);
                    byte[] commitNodeBytes = Encoding.UTF8.GetBytes(choiceCommitNode ?? "");
                    if (commitNodeBytes.Length > 255) throw new InvalidOperationException("Dialogue commit node name too long for co-op packet.");
                    writer.Write(choiceCommitId);
                    writer.Write(choiceCommitSessionId);
                    writer.Write(choiceCommitIndex);
                    writer.Write((byte)commitNodeBytes.Length);
                    writer.Write(commitNodeBytes);
                    byte[] commitTextBytes = Encoding.UTF8.GetBytes(choiceCommitText ?? "");
                    if (commitTextBytes.Length > 255) throw new InvalidOperationException("Dialogue choice text too long for co-op packet.");
                    writer.Write((byte)commitTextBytes.Length);
                    writer.Write(commitTextBytes);
                    byte[] dialogueAssetBytes = Encoding.UTF8.GetBytes(dialogueAsset ?? "");
                    byte[] dialogueNodeBytes = Encoding.UTF8.GetBytes(dialogueNode ?? "");
                    if (dialogueAssetBytes.Length > 255 || dialogueNodeBytes.Length > 255)
                        throw new InvalidOperationException("Dialogue asset or node name too long for co-op packet.");
                    writer.Write(dialogueRunId);
                    writer.Write(dialogueOption);
                    writer.Write((byte)dialogueAssetBytes.Length);
                    writer.Write(dialogueAssetBytes);
                    writer.Write((byte)dialogueNodeBytes.Length);
                    writer.Write(dialogueNodeBytes);
                    byte[] dialogueRunStreamBytes = Encoding.UTF8.GetBytes(dialogueRunStream ?? "");
                    if (dialogueRunStreamBytes.Length > 255) throw new InvalidOperationException("Dialogue run stream name too long.");
                    writer.Write((byte)dialogueRunStreamBytes.Length);
                    writer.Write(dialogueRunStreamBytes);
                    if (dialogueLines == null) dialogueLines = new List<DialogueLineEvent>();
                    if (dialogueLines.Count > byte.MaxValue) throw new InvalidOperationException("Too many dialogue line streams for packet.");
                    writer.Write((byte)dialogueLines.Count);
                    for (int i = 0; i < dialogueLines.Count; i++)
                    {
                        DialogueLineEvent line = dialogueLines[i];
                        byte[] streamBytes = Encoding.UTF8.GetBytes(line.Stream ?? "");
                        byte[] fingerprintBytes = Encoding.UTF8.GetBytes(line.Fingerprint ?? "");
                        byte[] textBytes = Encoding.UTF8.GetBytes(line.Text ?? "");
                        if (streamBytes.Length > 255 || fingerprintBytes.Length > 64 || textBytes.Length > 1024)
                            throw new InvalidOperationException("Dialogue stream name or fingerprint too long.");
                        writer.Write((byte)streamBytes.Length);
                        writer.Write(streamBytes);
                        writer.Write(line.Sequence);
                        writer.Write((byte)fingerprintBytes.Length);
                        writer.Write(fingerprintBytes);
                        writer.Write((ushort)textBytes.Length);
                        writer.Write(textBytes);
                    }
                    if (stream.Length > MaxPacketBytes) throw new InvalidOperationException("Co-op state packet exceeds safe UDP packet size.");
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
                if (stream.Length - stream.Position < 18) return false;
                p.State.ChoiceRequestIndex = reader.ReadInt32();
                p.State.ChoiceRequestSessionId = reader.ReadUInt32();
                if (p.State.ChoiceRequestIndex < -1) return false;
                if (stream.Length - stream.Position < 10) return false;
                p.State.HasAnimation = reader.ReadBoolean();
                p.State.AnimationHash = reader.ReadInt32();
                p.State.AnimationTime = reader.ReadSingle();
                p.State.Facing = reader.ReadInt32();
                p.State.Walking = reader.ReadBoolean();
                if (p.State.HasAnimation && p.State.AnimationHash == 0) return false;
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
                if (stream.Position >= stream.Length) return false;
                int ackCount = reader.ReadByte();
                p.DialogueLineAcks = new Dictionary<string, uint>(StringComparer.Ordinal);
                for (int i = 0; i < ackCount; i++)
                {
                    if (stream.Position >= stream.Length) return false;
                    int streamLength = reader.ReadByte();
                    if (stream.Length - stream.Position < streamLength + 4) return false;
                    string streamKey = Encoding.UTF8.GetString(reader.ReadBytes(streamLength));
                    uint ack = reader.ReadUInt32();
                    p.DialogueLineAcks[streamKey] = ack;
                }
                if (stream.Length - stream.Position < 6) return false;
                p.InteractionRequestId = reader.ReadUInt32();
                int interactionAssetLength = reader.ReadByte();
                if (stream.Length - stream.Position < interactionAssetLength + 1) return false;
                p.InteractionAsset = Encoding.UTF8.GetString(reader.ReadBytes(interactionAssetLength));
                int interactionNodeLength = reader.ReadByte();
                if (stream.Length - stream.Position < interactionNodeLength + 4) return false;
                p.InteractionNode = Encoding.UTF8.GetString(reader.ReadBytes(interactionNodeLength));
                p.InteractionAckId = reader.ReadUInt32();
                if (stream.Length - stream.Position < 4) return false;
                p.InputDialogueRunId = reader.ReadUInt32();
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
                    if (stream.Length - stream.Position < 1) return false;
                    int optionTextLength = reader.ReadByte();
                    if (stream.Length - stream.Position < optionTextLength + 4) return false;
                    p.OptionText = Encoding.UTF8.GetString(reader.ReadBytes(optionTextLength));
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
                    if (stream.Length - stream.Position < 1) return false;
                    int commitTextLength = reader.ReadByte();
                    if (stream.Length - stream.Position < commitTextLength) return false;
                    p.ChoiceCommitText = Encoding.UTF8.GetString(reader.ReadBytes(commitTextLength));
                    if (stream.Length - stream.Position < 10) return false;
                    p.DialogueRunId = reader.ReadUInt32();
                    p.DialogueOption = reader.ReadInt32();
                    if (p.DialogueOption < 0) return false;
                    if (stream.Length - stream.Position < 1) return false;
                    int dialogueAssetLength = reader.ReadByte();
                    if (stream.Length - stream.Position < dialogueAssetLength + 1) return false;
                    p.DialogueAsset = Encoding.UTF8.GetString(reader.ReadBytes(dialogueAssetLength));
                    int dialogueNodeLength = reader.ReadByte();
                    if (stream.Length - stream.Position < dialogueNodeLength) return false;
                    p.DialogueNode = Encoding.UTF8.GetString(reader.ReadBytes(dialogueNodeLength));
                    if (stream.Position >= stream.Length) return false;
                    int dialogueRunStreamLength = reader.ReadByte();
                    if (stream.Length - stream.Position < dialogueRunStreamLength + 1) return false;
                    p.DialogueRunStream = Encoding.UTF8.GetString(reader.ReadBytes(dialogueRunStreamLength));
                    if (stream.Position >= stream.Length) return false;
                    int dialogueLineCount = reader.ReadByte();
                    p.DialogueLines = new List<DialogueLineEvent>(dialogueLineCount);
                    for (int i = 0; i < dialogueLineCount; i++)
                    {
                        if (stream.Position >= stream.Length) return false;
                        int streamKeyLength = reader.ReadByte();
                        if (stream.Length - stream.Position < streamKeyLength + 5) return false;
                        string streamKey = Encoding.UTF8.GetString(reader.ReadBytes(streamKeyLength));
                        uint lineSequence = reader.ReadUInt32();
                        int fingerprintLength = reader.ReadByte();
                        if (stream.Length - stream.Position < fingerprintLength) return false;
                        string fingerprint = Encoding.UTF8.GetString(reader.ReadBytes(fingerprintLength));
                        if (stream.Length - stream.Position < 2) return false;
                        int textLength = reader.ReadUInt16();
                        if (textLength > 1024 || stream.Length - stream.Position < textLength) return false;
                        string lineText = Encoding.UTF8.GetString(reader.ReadBytes(textLength));
                        p.DialogueLines.Add(new DialogueLineEvent(streamKey, lineSequence, fingerprint, lineText));
                    }
                }
                if (stream.Position != stream.Length) return false;
                packet = p;
                return true;
            }
        }
    }
}
