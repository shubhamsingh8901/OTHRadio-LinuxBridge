#!/usr/bin/env bash
# OTHRadio Linux bridge - one-tap setup.
# Run once on the machine that has Over the Hill (Steam/Proton):
#   bash one-tap.sh            # everything automatic
#   bash one-tap.sh --dll ~/Downloads/OTHRadio.dll    # use a DLL you already have
#
# Does: playerctl install (best effort) -> get the mod DLL (release asset /
# local file / build from source) -> install it with a backup -> install and
# enable the bridge as a systemd --user unit -> verify.
set -u

REPO_SLUG="shubhamsingh8901/OTHRadio-LinuxBridge"
BRIDGE_DIR="${OTHRADIO_DIR:-$HOME/.cache/othradio}"
GAME=
DLL_SRC=          # empty -> auto (local file next to script, then release, then build)
NEED_BUILD=0
FAIL=0

say() { printf '\n\033[1m== %s ==\033[0m\n' "$*"; }
ok()  { printf '  \033[32mPASS\033[0m %s\n' "$*"; }
no()  { printf '  \033[31mFAIL\033[0m %s\n' "$*"; FAIL=1; }
warn(){ printf '  \033[33mnote\033[0m %s\n' "$*"; }

[ "$1" = "--dll" ] && { DLL_SRC="$2"; shift 2; }

say "playerctl"
if ! command -v playerctl >/dev/null 2>&1; then
  warn "playerctl missing - installing (sudo password may be asked)"
  if command -v pacman >/dev/null 2>&1; then sudo pacman -S --noconfirm --needed playerctl
  elif command -v apt-get >/dev/null 2>&1; then sudo apt-get update -qq && sudo apt-get install -y -qq playerctl
  elif command -v dnf >/dev/null 2>&1; then sudo dnf install -y -q playerctl
  elif command -v zypper >/dev/null 2>&1; then sudo zypper install -y -q playerctl
  fi
fi
command -v playerctl >/dev/null 2>&1 && ok "playerctl $(playerctl --version 2>/dev/null || echo present)" || no "playerctl still missing - install it manually"

say "the DLL"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" 2>/dev/null && pwd)"

if [ -z "$DLL_SRC" ]; then
  if [ -f "$SCRIPT_DIR/OTHRadio.dll" ]; then
    DLL_SRC="$SCRIPT_DIR/OTHRadio.dll"
    ok "found a DLL next to this script"
  fi
fi
if [ -z "$DLL_SRC" ]; then
  # newest GitHub release asset OTHRadio.dll (no build needed)
  URL="$(curl -fsSL "https://api.github.com/repos/$REPO_SLUG/releases/latest" 2>/dev/null \
    | grep -o '"browser_download_url": *"[^"]*OTHRadio\.dll"' \
    | tail -n1 | grep -o 'https[^"]*')"
  if [ -n "$URL" ]; then
    tmp="$BRIDGE_DIR/.OTHRadio.dll.new"; mkdir -p "$BRIDGE_DIR"
    curl -fsSL -o "$tmp" "$URL" && DLL_SRC="$tmp" && ok "downloaded DLL from the release"
  fi
fi
if [ -z "$DLL_SRC" ]; then
  warn "no prebuilt DLL found - building from the repo instead"
  NEED_BUILD=1
fi

SRC_DIR=""
if [ $NEED_BUILD -eq 1 ]; then
  command -v dotnet >/dev/null 2>&1 || { no "dotnet SDK missing (sudo apt install dotnet-sdk-8.0 / pacman -S dotnet-sdk)"; FAIL=1; }
fi
if [ $NEED_BUILD -eq 1 ] && command -v dotnet >/dev/null 2>&1; then
  if [ -d "$SCRIPT_DIR/OTHRadio_src" ]; then SRC_DIR="$SCRIPT_DIR/OTHRadio_src"
  else
    SRC_DIR="$HOME/.local/share/OTHRadio-LinuxBridge"
    git clone -q "https://github.com/$REPO_SLUG.git" "$HOME/.local/share/OTHrepo" 2>/dev/null || true
    [ -d "$HOME/.local/share/OTHrepo/OTHRadio_src" ] && SRC_DIR="$HOME/.local/share/OTHrepo/OTHRadio_src"
    rm -rf "$HOME/.local/share/OTHrepo" 2>/dev/null
  fi
  [ -d "$SRC_DIR" ] && ok "source ready" || no "couldn't get the source"
fi

say "the game folder"
GAME_ROOTS="$HOME/.steam/steam $HOME/.local/share/Steam $HOME/.var/app/com.valvesoftware.Steam/data/Steam"
for R in $GAME_ROOTS; do
  [ -f "$R/steamapps/libraryfolders.vdf" ] && grep '"path"' "$R/steamapps/libraryfolders.vdf" | awk -F'"' '{print $4}' >> "$BRIDGE_DIR/.libs" 2>/dev/null
  [ -d "$R/steamapps/common" ] && echo "$R" >> "$BRIDGE_DIR/.libs"
done
mkdir -p "$BRIDGE_DIR"
CAND=""
[ -f "$BRIDGE_DIR/.libs" ] && CAND="$(cat "$BRIDGE_DIR/.libs" 2>/dev/null)"
rm -f "$BRIDGE_DIR/.libs"
for L in $CAND; do
  for D in "$L/steamapps/common/Over the Hill" "$L/common/Over the Hill"; do
    [ -d "$D" ] && GAME="$D" && break 2
  done
done
if [ -z "$GAME" ]; then
  printf 'Path to the game folder (the one containing MelonLoader/): '
  read -r GAME
fi
[ -d "$GAME" ] && ok "$GAME" || { no "game folder not found"; exit 1; }
[ -d "$GAME/MelonLoader/Il2CppAssemblies" ] && ok "MelonLoader assemblies present" || { no "game has not been launched once with MelonLoader - do that first (with the original mod)"; exit 1; }

if [ $NEED_BUILD -eq 1 ]; then
  say "building"
  if (cd "$SRC_DIR" && dotnet build -c Release -p:GameDir="$GAME" -v q 2>&1 | tail -n 2); then
    BUILD_DLL="$SRC_DIR/bin/Release/net6.0/OTHRadio.dll"
    [ -f "$BUILD_DLL" ] && DLL_SRC="$BUILD_DLL" && ok "built $BUILD_DLL"
  fi
  [ -n "$DLL_SRC" ] || { no "build failed"; exit 1; }
fi

say "installing the mod"
mkdir -p "$GAME/Mods"
if [ ! -f "$GAME/Mods/OTHRadio_v1.2.0.bak" ]; then
  [ -f "$GAME/Mods/OTHRadio.dll" ] && cp "$GAME/Mods/OTHRadio.dll" "$GAME/Mods/OTHRadio_v1.2.0.bak" && ok "original backed up"
else
  warn "backup already exists, not overwriting"
fi
cp -f "$DLL_SRC" "$GAME/Mods/OTHRadio.dll" && ok "$GAME/Mods/OTHRadio.dll (1.3.0)"

say "bridge service"
install -Dm755 "$SCRIPT_DIR/othradio-bridge.sh" "$HOME/.local/bin/othradio-bridge.sh" 2>/dev/null \
  || { warn "script not next to me - downloading it"; mkdir -p "$HOME/.local/bin"; curl -fsSL -o "$HOME/.local/bin/othradio-bridge.sh" "https://raw.githubusercontent.com/$REPO_SLUG/main/othradio-bridge.sh"; }
chmod +x "$HOME/.local/bin/othradio-bridge.sh"
mkdir -p "$HOME/.config/systemd/user"
cat > "$HOME/.config/systemd/user/othradio-bridge.service" <<EOF
[Unit]
Description=OTHRadio MPRIS <-> file bridge

[Service]
ExecStart=$HOME/.local/bin/othradio-bridge.sh
Restart=on-failure

[Install]
WantedBy=default.target
EOF
if command -v systemctl >/dev/null 2>&1 && systemctl --user daemon-reload 2>/dev/null && systemctl --user enable --now othradio-bridge.service 2>/dev/null; then
  sleep 1
  systemctl --user is-active othradio-bridge.service >/dev/null 2>&1 && ok "bridge running as a user service (survives reboots)" || warn "service installed but not active - run ~/.local/bin/othradio-bridge.sh in a terminal to see why"
else
  warn "systemd --user not available - run ~/.local/bin/othradio-bridge.sh yourself (e.g. add to startup)"
fi

say "verify"
sleep 1
[ -f "$BRIDGE_DIR/now.txt" ] && ok "now.txt exists ($(head -c 80 "$BRIDGE_DIR/now.txt" | head -1))" || warn "no now.txt yet - the bridge writes it within a second once running"
if playerctl -l >/dev/null 2>&1; then ok "players: $(playerctl -l 2>/dev/null | head -3 | tr '\n' ' ')"; else warn "no players running right now"; fi
APPID="$(grep -l 'Over the Hill' "$GAME"/../*.acf 2>/dev/null | head -1)"
APPID="$(basename "${APPID:-x}" | grep -o '[0-9]*')"
printf '\n%s\n' "LAST MANUAL STEP - in Steam: right-click the game -> Properties -> Launch Options:"
if [ -n "$APPID" ]; then
  printf '%s\n' "  WINEDLLOVERRIDES=\"version=n,b\" %command%     (game app id: $APPID)"
else
  printf '%s\n' "  WINEDLLOVERRIDES=\"version=n,b\" %command%"
fi
printf '%s\n' "then start music on the host, launch the game, and check:"
printf '%s\n' "  grep OTHRadio \"$GAME/MelonLoader/Latest.log\" | head -5"
[ $FAIL -eq 0 ] && printf '\n\033[32mSETUP COMPLETE\033[0m\n' || printf '\n\033[31mSETUP FINISHED WITH FAILURES (see above)\033[0m\n'
exit $FAIL
