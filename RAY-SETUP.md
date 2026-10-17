# Ray's setup sheet (Linux PC with the game)

Goal: make the **PC / Spotify** station of the OTHRadio mod work under
Steam/Proton by bridging it to `playerctl`. Two paths - use the one you got:

- **Path A (prebuilt)**: you received `OTHRadio.dll` (1.3.0) directly from Sai
  -> skip step 2.
- **Path B (build here)**: you build the repo yourself.

---

## 1. Prerequisites

```bash
dotnet --version          # Path B only: needs .NET SDK 6/8/9/10
playerctl -l              # any Linux: lists running MPRIS players
# Arch: sudo pacman -S playerctl | Debian/Ubuntu: sudo apt install playerctl
```

`playerctl -l` should list Spotify/browsers *while they are playing*. If empty,
start some music first - the whole mod depends on this one tool.

## 2. Path B only - build

```bash
git clone https://github.com/shubhamsingh8901/OTHRadio-LinuxBridge.git
cd OTHRadio-LinuxBridge/OTHRadio_src

# find your game folder if unsure:
awk -F'"' '/"path"/ {print $4}' ~/.steam/steam/steamapps/libraryfolders.vdf
GAME="$HOME/.steam/steam/steamapps/common/Over the Hill"

# requires that the game was launched ONCE with MelonLoader so that
# MelonLoader/Il2CppAssemblies exists (you already run the original mod, so yes)
dotnet build -c Release -p:GameDir="$GAME"
```

Must end with `Build succeeded` -> output at `bin/Release/net6.0/OTHRadio.dll`.

## 3. Install

```bash
GAME="$HOME/.steam/steam/steamapps/common/Over the Hill"
cp "$GAME/Mods/OTHRadio.dll" "$GAME/Mods/OTHRadio_v1.2.0.bak"  # keep the old one
# Path B:
cp OTHRadio-LinuxBridge/OTHRadio_src/bin/Release/net6.0/OTHRadio.dll "$GAME/Mods/"
# Path A: drop the DLL you received into $GAME/Mods/ (overwrite)
```

## 4. Bridge script (runs on the Linux host, forever)

```bash
install -Dm755 OTHRadio-LinuxBridge/othradio-bridge.sh ~/.local/bin/othradio-bridge.sh   # Path B
sudo install -Dm755 othradio-bridge.sh /usr/local/bin/othradio-bridge.sh               # Path A

# foreground test (leave it open while testing):
~/.local/bin/othradio-bridge.sh
```

Sanity check from a second terminal after ~10 s:

```bash
awk -F'\t' '{print "fields="NF" status="$1" player="$2}' ~/.cache/othradio/now.txt
# want: fields=8 ... status=Playing player=spotify   (or "none" when nothing plays)
```

Then make it permanent:

```bash
mkdir -p ~/.config/systemd/user
cat > ~/.config/systemd/user/othradio-bridge.service <<'EOF'
[Unit]
Description=OTHRadio MPRIS <-> file bridge

[Service]
ExecStart=%h/.local/bin/othradio-bridge.sh
Restart=on-failure

[Install]
WantedBy=default.target
EOF
systemctl --user daemon-reload
systemctl --user enable --now othradio-bridge
```

Trick: `OTHRADIO_DIR` env var can move the folder, but leave it at the default
`~/.cache/othradio` unless you have a reason.

## 5. Launch

Steam launch options for the game (keep anything else you already had there):

```
WINEDLLOVERRIDES="version=n,b" %command%
```

Start music on the host first, then the game.

## 6. Verify (checklist - report each as PASS/FAIL)

`tail -30 "$GAME/MelonLoader/Latest.log" | grep OTHRadio` right after loading in:

1. `[OTHRadio] PC station: Linux bridge folder Z:\home\<you>\.cache\othradio`
2. In-game (F6): PC/Spotify station shows current track + artist
3. Play/pause/next/previous buttons control the host player
4. On-screen player (tilt gauges) updates and shows a moving progress bar
5. Volume slider changes `playerctl volume`
6. "Open folder" shows a notification with the path (no explorer, expected)

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| Log: `PC station: Windows media controls` | OTHRadio.dll wasn't replaced (still 1.2.0), or got a fallback - read the `[OTHRadio]` warning line |
| Log: `Linux bridge unavailable (...)` | Read the reason; if `no usable HOME` set `BridgeFolder` in `UserData/OTHRadio.cfg` to `Z:\home\<you>\.cache\othradio` |
| Track never shows | bridge script running? fields=8 in now.txt? music actually playing? |
| Buttons dead | game can't write `cmd`, or script not consuming - check `cat ~/.cache/othradio/cmd` after a click |

Send back: the build tail (Path B), all `[OTHRadio]` lines from `Latest.log`,
and the PASS/FAIL of the 6 checks.
