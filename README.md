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

One-off bot-driven two-client loopback checks have demonstrated scene following, movement in selected scenes, numeric story-variable snapshots, and receipt/application of one opening-scene choice commit. These checks do not establish a complete co-op playthrough.

Failures and unresolved results:

- A host confirmation at the title screen was observed by the guest but did not advance the host.
- In `MaeHouse`, a left-movement pulse did not move Mae, and host/guest interaction did not open a Yarn option menu; the cause is unknown.
- An earlier `MaeRoom` variable mutation produced a whole-dictionary hash mismatch for at least six seconds before a later snapshot/test state caught up. The run is not evidence of immediate consistency.
- The latest fresh launch attempt did not connect the peers; the guest logged `UDP receive error: An invalid argument was supplied.`
- A live opening-scene test logged that the guest applied one host choice commit at index 0. The resulting selected branch and scripted effects were not verified.

No maintained automated suite has been run. Normal dialogue choices, scripted events, persistent journal/counter state, save progression, packet loss, reconnection, and LAN/VPN play remain unverified. Treat the prototype as not yet playable co-op.

## QA bot (developer use)

For local QA, the plugin reads `BepInEx/config/CoopBot-Host.txt` or `CoopBot-Client.txt`. Commands include `confirm`, `move_left:<frames>`, `move_right:<frames>`, `move_up:<frames>`, `move_down:<frames>`, `scene:<name>`, and `mutate_first_var`. The scene and mutation commands are test shortcuts, not player features. Status files report connection state, scene, movement and story-variable counts/hashes.

## License and game requirements

The repository contains only this project's source and build instructions. It does not contain game assets or game binaries. The build requires a legally obtained copy of the matching game build and BepInEx 5.4.23.5.

