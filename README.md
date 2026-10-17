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

Verified here (Windows machine, no game install):

- Full decompile of the shipped `OTHRadio.dll` 1.2.0 (clean, all code reviewed).
- The Windows WinRT worker was transcribed 1:1 into `WindowsMedia`;
  cross-checked against a fresh decompile: identical vtable slot sequence,
  call-site sequence and combase imports.
- `LinuxBridgeMedia`, `PcCommand`, `IPcBackend`, `WineDetector` were compiled
  for real and executed in a test harness (`tests/`): **49/49 passed**, including
  a live file-protocol round trip (real thread polling `now.txt`, commands
  appended to `cmd`, folder resolution from `HOME`, volume ordering).
- Whole-tree syntax gate: parses with zero syntax errors (only expected
  unresolved-reference errors without game DLLs).

NOT verified here (needs the game):

- `dotnet build` of the mod itself (reference DLLs unavailable on this machine).
- In-game behavior under Proton/Spotify; the WinRT/WASAPI Windows path after
  the refactor; MelonLoader preference wiring at runtime.

So: status is **code complete + bridge protocol tested; in-game test pending** -
do the build + in-game test on the machine that has the game, and check
`MelonLoader/Latest.log` for `[OTHRadio]` lines as described above.

## File map

- `OTHRadio_src/OTHRadio/LinuxBridgeMedia.cs` - new: the bridge backend
- `OTHRadio_src/OTHRadio/WindowsMedia.cs` - original WinRT worker, now behind the interface
- `OTHRadio_src/OTHRadio/PcMedia.cs` - facade, same static API as before; picks the backend
- `OTHRadio_src/OTHRadio/IPcBackend.cs`, `PcCommand.cs` - interface + command enum
- `OTHRadio_src/OTHRadio/WineDetector.cs` - `ntdll!wine_get_version` check
- edits in `Radio.cs` (prefs, volume, texts), `RadioMenu.cs` (hint, folder), `AssemblyInfo.cs` (version 1.3.0)
- `othradio-bridge.sh` - host-side bridge script
- `tests/` - protocol/backend test harness (`dotnet run --project tests -c Release`)
