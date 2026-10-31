#!/bin/bash
# Runs a list of `script run` steps in order and stops at the first failure.
# usage: run_steps.sh <steps file> <log dir> [timeout seconds per step, default 620]
# steps file: one step per line "file|Class.Method|json args" (args may be empty); blank lines and lines starting with # are skipped.
# Write the steps file with LF line endings (a CR ends up in the args and `script run` rejects them).
# Each step's full output goes to <log dir>/NN_<Method>.json; a summary line per step goes to stdout.
# Launch it with run_in_background and read the log when it finishes; never edit a generator while it runs
# (`script run` recompiles the file on every step).
steps="$1"; logs="$2"; secs="${3:-620}"
[[ -f "$steps" && -n "$logs" ]] || { echo "usage: run_steps.sh <steps file> <log dir> [timeout s]"; exit 2; }
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 1
P="$(cygpath -w "$PWD" 2>/dev/null || echo "$PWD")"
mkdir -p "$logs"
i=0
while IFS='|' read -r file entry args || [[ -n "$file" ]]; do
  file="${file%$'\r'}"; entry="${entry%$'\r'}"; args="${args%$'\r'}"
  [[ -z "$file" || "$file" == \#* ]] && continue
  i=$((i + 1))
  log="$logs/$(printf '%02d' $i)_${entry##*.}.json"
  start=$(date +%s)
  cmd=(unity command --project-path "$P" --timeout "$secs" --result-only script run --file "$file" --entry "$entry")
  [[ -n "$args" ]] && cmd+=(--args "$args")
  "${cmd[@]}" > "$log" 2>&1
  took=$(( $(date +%s) - start ))
  if grep -q '"success": true' "$log"; then
    echo "ok   $entry (${took}s): $(grep -o '"result": .*' "$log" | head -c 300)"
  else
    echo "FAIL $entry (${took}s) -> $log"
    head -c 1500 "$log"
    exit 1
  fi
done < "$steps"
echo "all $i steps done"
