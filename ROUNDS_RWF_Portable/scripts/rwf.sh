#!/usr/bin/env bash
# ROUNDS + RoundsWithFriends portable launcher for Linux / Steam Deck (Proton)
set -euo pipefail

APPID=1557740
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME_FILES="$ROOT/game_files"
PRELOADER="$ROOT/BepInEx/core/BepInEx.Preloader.dll"

die() { echo "エラー: $*" >&2; exit 1; }

# Linux パス -> Wine から見える Windows パス (Z:\...)
win_path() { printf 'Z:%s' "${1//\//\\}"; }

steam_roots() {
  local r
  for r in "$HOME/.steam/steam" "$HOME/.local/share/Steam" \
           "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam" \
           "$HOME/snap/steam/common/.local/share/Steam"; do
    [ -d "$r/steamapps" ] && echo "$r"
  done
}

find_game_dir() {
  local root lib libs
  while read -r root; do
    [ -n "$root" ] || continue
    mapfile -t libs < <(echo "$root"; grep -oP '"path"\s+"\K[^"]+' "$root/steamapps/libraryfolders.vdf" 2>/dev/null || true)
    for lib in "${libs[@]}"; do
      if [ -f "$lib/steamapps/common/ROUNDS/ROUNDS.exe" ]; then
        echo "$lib/steamapps/common/ROUNDS"; return 0
      fi
    done
  done < <(steam_roots)
  return 1
}

launch_options() {
  printf 'WINEDLLOVERRIDES="winhttp=n,b" %%command%%'
}

launch_options_always_mod() {
  printf 'WINEDLLOVERRIDES="winhttp=n,b" %%command%% --doorstop-enable true --doorstop-target "%s"' "$(win_path "$PRELOADER")"
}

same_file() { [ -f "$1" ] && [ -f "$2" ] && cmp -s "$1" "$2"; }

steam_cmd() {
  if command -v steam >/dev/null 2>&1; then echo steam
  elif command -v flatpak >/dev/null 2>&1 && flatpak info com.valvesoftware.Steam >/dev/null 2>&1; then echo "flatpak run com.valvesoftware.Steam"
  else return 1; fi
}

case "${1:-}" in
  install)
    GAME="$(find_game_dir)" || die "ROUNDS のインストール先が見つかりません"
    echo "ROUNDS のフォルダ: $GAME"
    for f in winhttp.dll doorstop_config.ini; do
      if [ -f "$GAME/$f" ] && ! same_file "$GAME_FILES/$f" "$GAME/$f"; then
        bak="$GAME/$f.bak_$(date +%Y%m%d%H%M%S)"
        mv "$GAME/$f" "$bak"; echo "既存の $f を $bak に退避しました"
      fi
      cp "$GAME_FILES/$f" "$GAME/$f"
    done
    cat <<EOF

ゲームフォルダへの導入が終わりました。最後に Steam の設定が 1 つ必要です。
Steam で ROUNDS を右クリック -> プロパティ -> 一般 -> 起動オプション に、
次のどちらかを貼り付けてください。

[A] 普段は mod なし、play.sh で起動したときだけ mod 入り（おすすめ）
$(launch_options)

[B] Steam から起動すると常に mod 入り（Steam Deck のゲームモード向け）
$(launch_options_always_mod)

EOF
    case "$ROOT" in *" "*) echo "注意: このフォルダのパスにスペースが含まれています。英数字だけのパスに移すことをおすすめします。";; esac
    ;;
  play)
    [ -f "$PRELOADER" ] || die "BepInEx が見つかりません: $PRELOADER"
    GAME="$(find_game_dir)" || die "ROUNDS のインストール先が見つかりません"
    same_file "$GAME_FILES/winhttp.dll" "$GAME/winhttp.dll" || die "先に install.sh を実行してください"
    STEAM="$(steam_cmd)" || die "steam コマンドが見つかりません"
    echo "mod 入り ROUNDS を起動します..."
    echo "(起動オプションに WINEDLLOVERRIDES=\"winhttp=n,b\" %command% が必要です。mod が効かないときは install.sh の表示を確認してください)"
    # shellcheck disable=SC2086
    nohup $STEAM -applaunch "$APPID" --doorstop-enable true --doorstop-target "$(win_path "$PRELOADER")" >/dev/null 2>&1 &
    ;;
  uninstall)
    GAME="$(find_game_dir)" || die "ROUNDS のインストール先が見つかりません"
    for f in winhttp.dll doorstop_config.ini; do
      if same_file "$GAME_FILES/$f" "$GAME/$f"; then rm "$GAME/$f"; echo "$f を削除しました"
      elif [ -f "$GAME/$f" ]; then echo "$f はこのパッケージ以外のものなので残しました"; fi
    done
    echo "Steam の起動オプションに入れた WINEDLLOVERRIDES=... も消しておいてください。"
    ;;
  *)
    echo "使い方: bash $0 install|play|uninstall"; exit 1;;
esac
