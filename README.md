# Night in the Woods Co-op prototype

A BepInEx host/client prototype for the original Windows x64 Steam release of *Night in the Woods* (build 5940043, Unity 5.6.2p4, Mono). This is experimental software, not a finished or verified playable co-op mod.

The host is authoritative for one shared Mae, scene and position snapshots, dialogue inputs, and numeric `Continuity.vars` snapshots. The plugin also includes experimental host-authoritative dialogue-choice cursor and commit handling. There is no second player character. Scripted effects, saves, persistent counters, full story progression, and real-network play are not verified.

## Build

Build against the game's own managed assemblies and BepInEx 5.4.23.5:

```powershell
.\build.ps1 -GameDir 'C:\Games\Night in the Woods'
```

The build writes `NightInTheWoodsCoop.dll` and `NightInTheWoodsSavePathPatcher.dll` under `dist/`. Game files and compiled binaries are not included in this source repository.

## Install and connect

1. Install BepInEx 5 x64 in each player's own copy of the game.
2. Copy `NightInTheWoodsSavePathPatcher.dll` to `BepInEx/patchers/` and `NightInTheWoodsCoop.dll` to `BepInEx/plugins/`.
3. Configure one copy with `Role = Host` and the other with `Role = Client` in `BepInEx/config/coop-anything.nitw.cfg`.
4. Set the client's `HostAddress` to the host's LAN or VPN IPv4 address. Set the same UDP `Port` on both copies; allow that port through the host's firewall or use a VPN.
5. Start both copies at the same scene and story point.

The preloader redirects `Continuity` save paths to `BepInEx/config/NightInTheWoodsCoop/Saves` before the game assembly loads. The joining player's `Continuity.Save()` call is suppressed. Keep backups of existing saves before trying experimental software.

## QA status

One-off bot-driven two-client loopback checks on v0.6.10 (2026-10-21) demonstrated a connected host/client pair, host scene following, both participants moving the same Mae, host-authoritative numeric story-variable snapshots, host confirmation reaching `Global.IsConfirmOrInteractDown`, and application of one delayed host choice commit on the guest. These checks do not establish a complete co-op playthrough.

In a fresh-save run, the guest confirmed through `Title` and both clients reached `IntroPoem` with matching `Continuity.vars` hash `236FB091`. A diagnostic run showed the guest's `SetOption` callback arriving before the host commit and using a different local option-session id (231 vs. 232). v0.6.10 defers that guest callback briefly. In the repeat run, the host committed index 0 and the guest applied the deferred callback; both clients reported commit id 1 applied and matching hash `236FB091`. The branch's scripted effects were not independently verified. A later QA-only v0.6.11 experiment forced a host commit at index 1, but the guest did not enter `SetOption` or apply the commit, even after a guest confirm; nonzero-choice synchronization remains unresolved. In a separate connected run, host and guest movement both moved the same Mae; the guest's movement also led to both clients following a scene change. Scene transitions briefly produced different story-variable hashes; later snapshots converged them. A Playground interaction pulse did not open a Yarn option menu.

Failures and unresolved results:

- An earlier host confirmation at the title screen was observed by the guest but did not advance the host. In the latest fresh-save run, a guest confirmation did advance both clients from `Title` to `IntroPoem`.
- In `MaeHouse`, a left-movement pulse did not move Mae, and host/guest interaction did not open a Yarn option menu; the cause is unknown.
- An earlier `MaeRoom` variable mutation produced a whole-dictionary hash mismatch for at least six seconds before a later snapshot/test state caught up. The run is not evidence of immediate consistency.
- Before v0.6.8, the guest logged `UDP receive error: An invalid argument was supplied.` The client socket now binds to an ephemeral local UDP port; v0.6.10 connected over loopback. Real LAN/VPN behavior has not been tested.
- An earlier opening-scene test left the guest's `lastAppliedChoiceCommitId` at zero after a host commit. v0.6.10 applied that delayed index-0 callback in a repeat run. A later QA-only index-1 commit was not applied by the guest; branch effects and choice synchronization remain incomplete.

No maintained automated suite has been run. Normal dialogue choices, scripted events, persistent journal/counter state, save progression, packet loss, reconnection, and LAN/VPN play remain unverified. Treat the prototype as not yet playable co-op.

## QA bot (developer use)

For local QA, the plugin reads `BepInEx/config/CoopBot-Host.txt` or `CoopBot-Client.txt`. Commands include `confirm`, `move_left:<frames>`, `move_right:<frames>`, `move_up:<frames>`, `move_down:<frames>`, `scene:<name>`, and `mutate_first_var`. The scene and mutation commands are test shortcuts, not player features. Status files report connection state, scene, movement and story-variable counts/hashes.

## License and game requirements

The repository contains only this project's source and build instructions. It does not contain game assets or game binaries. The build requires a legally obtained copy of the matching game build and BepInEx 5.4.23.5.

