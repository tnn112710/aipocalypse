#!/usr/bin/env bash
# Usage: shot.sh <out.png> — captures the game view (1280×720) to any path. capture_game_view's own save_path
# only writes under Assets/, which would import the image.
P="$(cygpath -w "$PWD")"
unity command capture_game_view --width 1280 --height 720 --project-path "$P" --json 2>&1 | node -e "let s='';process.stdin.on('data',d=>s+=d).on('end',()=>{const j=JSON.parse(s);require('fs').writeFileSync(process.argv[1],Buffer.from(j.data.result.base64,'base64'));console.log('saved '+process.argv[1])})" "$1"
