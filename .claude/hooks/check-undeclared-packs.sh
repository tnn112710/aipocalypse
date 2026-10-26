#!/usr/bin/env bash
# SessionStart hook: lists vendor pack folders that ASSETS.md / PLUGINS.md don't name yet,
# so the session asks the user for a decision and adds the rows.
cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}" || exit 0

docs="$(cat docs/technicals/ASSETS.md docs/technicals/PLUGINS.md 2>/dev/null)"
[ -n "$docs" ] || exit 0
docs="${docs,,}"

# Bash matching instead of grep -i: Git Bash grep aborts on the docs' non-ASCII characters.
missing=()
for dir in Assets/_StoreAssets/*/*/ Assets/Synty/*/ Assets/Synty/SidekickCharacters/Characters/*/ Assets/Plugins/*/; do
  [ -d "$dir" ] || continue
  name="$(basename "$dir")"
  [[ "$docs" == *"${name,,}"* ]] || missing+=("${dir%/}")
done

[ ${#missing[@]} -eq 0 ] && exit 0

echo "Undeclared third-party packs: these vendor folders are not named in docs/technicals/ASSETS.md or PLUGINS.md:"
printf -- '- %s\n' "${missing[@]}"
echo "Before other work, tell the user about them. For each one, look at what it contains (art style, render pipeline, demo scripts, overlap with packs already ranked), suggest a purpose group and a tag (Must/Should/Could/Maybe/Avoid/Undecided) with a one-line reason, and ask the user to decide. Then add the row with the exact folder name (ASSETS.md for content, PLUGINS.md for code and tools). If the user declines to decide now, add the row as Undecided."
