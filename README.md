# OTHRadio - Linux/Proton bridge edition

Patched source of the `OTHRadio` MelonLoader mod (IL2CPP mod for the game
*Over the Hill*), so the **PC / Spotify** station also works when the game runs
under **Proton on Linux**. Game Soundtrack and My Music were already fine; this
version routes the PC station through a small file bridge to `playerctl`
(MPRIS) running natively on the Linux host.

The Windows implementation (WinRT `GlobalSystemMediaTransportControlsSessionManager`
via `combase.dll` + WASAPI per-app volume) is kept unchanged for Windows users
and is never touched when the bridge is active, so nothing throws under Wine.

## How it works

```
┌────────────────────────────┐        ┌──────────────────────────────┐
│ game under Proton (Wine)   │        │ Linux host                   │
│ OTHRadio.dll (this mod)    │        │ othradio-bridge.sh           │
│                            │        │                              │
│ LinuxBridgeMedia  ─────────┼─ Z: ───┼─ ~/.cache/othradio/now.txt   │
│  polls now.txt             │        │   (status⇥player⇥artist⇥     │
│                            │        │    title⇥album⇥len_us⇥       │
│                            │        │    pos_us⇥volume) or "none"  │
│            appends to cmd ─┼────────┼─ ~/.cache/othradio/cmd       │
│                            │        │   (play|pause|play-pause|    │
│                            │        │    next|previous|vol <0..1>) │
└────────────────────────────┘        │  playerctl  <->  MPRIS       │
                                      └──────────────────────────────┘
```

The folder **must** be under `$HOME` (shared with Proton's pressure-vessel
container). `/tmp` is private per-container and will not work. The mod reads
`HOME` and uses `Z:\home\<user>\.cache\othradio`; you can override it with the
`BridgeFolder` preference.

## Backend selection (at startup)

| Situation | Backend |
|---|---|
| `UseLinuxBridge` preference = true | Linux bridge (for testing on Windows too) |
| Wine/Proton detected (`ntdll!wine_get_version`) | Linux bridge |
| Bridge folder can't be created (no HOME etc.) | falls back to Windows backend + warning in the log |
| plain Windows | Windows backend (unchanged original behavior) |

New preferences in ``UserData/OTHRadio.cfg`:

- `UseLinuxBridge` (bool, default false) - force the bridge.
- `BridgeFolder` (string, default "") - override the bridge folder path.

Behavior differences under the bridge:

- Per-app volume slider now maps to `playerctl volume` (0..1). If the player
  doesn't report a volume, the slider shows as off but still works.
- "Open folder" doesn't launch `explorer.exe` under Proton; it shows the folder
  path as an in-game notification and in `MelonLoader/Latest.log` instead.

## Build (on a machine that has the game installed once with MelonLoader)

Requirements: .NET SDK 6+ (the project targets `net6.0`; `CheckEolTargetFramework`
is off). The reference DLLs come from the game install:

- `<game>/MelonLoader/net6/`: `MelonLoader.dll`, `0Harmony.dll`, `Il2CppInterop.Runtime.dll`
- `<game>/MelonLoader/Il2CppAssemblies/`: `Assembly-CSharp`, `Il2Cppmscorlib`,
  `Il2CppFMOD`, `Il2CppFMODUnity`, `Il2CppFunselektor.FunCore.Runtime`,
  `UnityEngine.CoreModule`, `UnityEngine.UIModule`, `UnityEngine.UI`,
  `Unity.InputSystem`, `Unity.TextMeshPro`

```bash
cd OTHRadio_src
dotnet build -c Release -p:GameDir="/path/to/steamapps/common/Over the Hill"
# or edit the <GameDir> default inside OTHRadio.csproj and just run:
# dotnet build -c Release
cp bin/Release/net6.0/OTHRadio.dll "<game>/Mods/OTHRadio.dll"
```

## Install / run

1. Build + copy the DLL into `<game>/Mods/` as above.
2. Copy `othradio-bridge.sh` to the Linux host, then:

```bash
chmod +x othradio-bridge.sh
./othradio-bridge.sh &          # or run it in a terminal / systemd user unit
```

3. Steam launch options for the game: `WINEDLLOVERRIDES="version=n,b" %command%`
4. Start music in Spotify (or any MPRIS player), launch the game, switch to the
   PC / Spotify station. `[OTHRadio]` lines appear in `MelonLoader/Latest.log`.

## What was verified (and what wasn't)

Verified here (Windows):
- Full decompile of the shipped `OTHRadio.dll` 1.2.0 (clean, all code reviewed).
- The Windows WinRT worker was transcribed 1:1 into `WindowsMedia`;
  cross-checked against a fresh decompile: identical vtable slot sequence,
  call-site sequence and combase imports.
- `LinuxBridgeMedia`, `PcCommand`, `IPcBackend`, `WineDetector` compiled and
  executed in a test harness (`tests/OthRadioBridgeTests.csproj, multi-target
  net8/net10`): **49/49 passed**, including a live file-protocol round trip.
- The mod itself **builds clean**: `dotnet build -c Release
  -p:RefsDir=<MelonLoader/net6> -p:ProxiesDir=<Il2CppAssemblies>` → 0 errors
  (warnings are expected Windows-only-COM notes).

Verified end-to-end on real Linux (WSL2 Ubuntu 26.04, real DBus):
- Real MPRIS player (VLC headless) + real `playerctl` + this repo's
  `othradio-bridge.sh`: `now.txt` carries the exact 8-field tab-separated line
  (status/player/artist/title/album/length_us/position_us/volume) with correct
  values, and the command matrix (`pause`, `play`, `next`, `previous`, `vol`,
  incl. multi-line batch appends) executes **3× full PASS** through the
  production script.
- Lifecycle: bridge exits cleanly on TERM (systemd-stop semantics), publishes
  `none`, and republishes a live line after relaunch.
- Backend harness re-run on native Linux: **49/49 PASS** (net10 target).

Defects found by the live Linux run and fixed:
1. `playerctl --format` does not expand `\t` - the script emitted literal
   backslash-t; the mod parses on real tabs. (fatal: every line would read as
   one garbage field)
2. `WineDetector` used `DllImport("kernel32")` for the probe and would throw
   `DllNotFoundException` on a non-Wine Linux host. Now uses `NativeLibrary`.
3. The bridge ignored `SIGTERM` (bash resumes the loop after a trap) - systemd
   stop would look hung. Handler now exits.
4. `cmd` intake did read-then-truncate; a mid-append write could lose its first
   line. Now snapshot-then-truncate, and a missing final newline is tolerated.

NOT verified here (needs the actual game):
- In-game behavior under Proton; the WinRT/WASAPI Windows path after the
  refactor; MelonLoader preference wiring at runtime. Check
  `MelonLoader/Latest.log` for `[OTHRadio]` lines as described above.

So: status is **code complete, mod builds, bridge protocol verified live on
Linux; in-game verification pending**.

## File map

- `OTHRadio_src/OTHRadio/LinuxBridgeMedia.cs` - new: the bridge backend
- `OTHRadio_src/OTHRadio/WindowsMedia.cs` - original WinRT worker, now behind the interface
- `OTHRadio_src/OTHRadio/PcMedia.cs` - facade, same static API as before; picks the backend
- `OTHRadio_src/OTHRadio/IPcBackend.cs`, `PcCommand.cs` - interface + command enum
- `OTHRadio_src/OTHRadio/WineDetector.cs` - `ntdll!wine_get_version` check
- edits in `Radio.cs` (prefs, volume, texts), `RadioMenu.cs` (hint, folder), `AssemblyInfo.cs` (version 1.3.0)
- `othradio-bridge.sh` - host-side bridge script
- `tests/` - protocol/backend test harness (`dotnet run --project tests -c Release`)
