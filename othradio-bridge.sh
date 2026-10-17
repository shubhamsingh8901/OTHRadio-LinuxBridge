#!/usr/bin/env bash
# OTHRadio Linux bridge.
# Translates MPRIS (playerctl) <-> the file protocol the mod reads/writes inside
# ${OTHRADIO_DIR:-$HOME/.cache/othradio}:
#   now.txt : one tab-separated line
#             status, playerName, artist, title, album, mpris:length (us), position (us), volume
#             (volume is 0..1; the mod treats a missing/invalid field as "volume off")
#             "none" when no player is running.
#   cmd     : the game appends one command per line:
#             play | pause | play-pause | next | previous | vol <0..1>
# The folder must be under $HOME (shared with Proton's pressure-vessel container);
# /tmp inside the container is private and would never be seen by the game.

D="${OTHRADIO_DIR:-$HOME/.cache/othradio}"

if ! command -v playerctl >/dev/null 2>&1; then
  echo "othradio-bridge: playerctl not installed (pacman -S playerctl / apt install playerctl)" >&2
  exit 1
fi

mkdir -p "$D" || { echo "othradio-bridge: cannot create $D" >&2; exit 1; }

# single instance (a second loop would eat each other's commands)
if command -v flock >/dev/null 2>&1; then
  exec 9>"$D/.lock"
  flock -n 9 || { echo "othradio-bridge: already running (lock $D/.lock)" >&2; exit 0; }
fi

: > "$D/cmd"
echo "othradio-bridge: watching $D (press Ctrl-C to stop)"

on_exit() {
  printf 'none\n' > "$D/now.txt" 2>/dev/null
  : > "$D/cmd"
}
trap on_exit INT TERM EXIT

while true; do
  TAB=$'\t'
  if playerctl metadata --format "{{status}}${TAB}{{playerName}}${TAB}{{artist}}${TAB}{{title}}${TAB}{{album}}${TAB}{{mpris:length}}${TAB}{{position}}${TAB}{{volume}}" > "$D/now.tmp" 2>/dev/null && [ -s "$D/now.tmp" ]; then
    mv -f "$D/now.tmp" "$D/now.txt"
  else
    printf 'none\n' > "$D/now.tmp" && mv -f "$D/now.tmp" "$D/now.txt"
  fi

  if [ -s "$D/cmd" ]; then
    while IFS= read -r c; do
      [ -z "$c" ] && continue
      case "$c" in
        play-pause|play|pause|next|previous)
          playerctl "$c" >/dev/null 2>&1 ;;
        vol\ *)
          a="${c#vol }"
          # only plain decimal numbers, so nothing else reaches playerctl
          if [[ "$a" =~ ^[0-9]+(\.[0-9]+)?$ ]]; then
            playerctl volume "$a" >/dev/null 2>&1
          fi ;;
      esac
    done < "$D/cmd"
    : > "$D/cmd"
  fi

  sleep 0.5
done
