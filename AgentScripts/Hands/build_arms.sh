#!/bin/bash
# Builds a first-person arm variant from any Sidekick character (.sk) and, with --use, shows it in game.
#   bash AgentScripts/Hands/build_arms.sh <source.sk> <Variant> [--use]
# e.g. bash AgentScripts/Hands/build_arms.sh Assets/Synty/SidekickCharacters/Characters/ApocalypseSurvivors/ApocalypseSurvivor_02/ApocalypseSurvivor_02.sk Survivor02 --use
# Run from the repo root with the editor open (steps: ArmSetup.cs header). Switch back with ArmSetup.Use "" .
set -e
SK="$1"; VARIANT="$2"
if [ -z "$SK" ] || [ -z "$VARIANT" ]; then echo "usage: build_arms.sh <source.sk> <Variant> [--use]"; exit 1; fi
P="$(cygpath -w "$PWD" 2>/dev/null || pwd)"
FOLDER="Assets/_Game/Content/Player/Arms/$VARIANT"
run() { unity command --project-path "$P" script run --file "$1" --entry "$2" --args "$3" --timeout 300 | tail -1 | cut -c1-300; }
for SIDE in Left Right; do
  run AgentScripts/Hands/ArmSetup.cs ArmSetup.WriteSk "[\"$SK\",\"$VARIANT\",\"$SIDE\"]"
  run .agents/skills/tools-unity-synty-characters/scripts/SidekickKit.cs SidekickKit.Build "[\"$FOLDER/Arm_$SIDE.sk\",\"$FOLDER\",\"Arm_$SIDE\",false,false,\"\"]"
  run AgentScripts/Hands/ArmSetup.cs ArmSetup.MakePrefab "[\"$VARIANT\",\"$SIDE\"]"
done
run AgentScripts/Hands/ArmSetup.cs ArmSetup.Clean "[\"$VARIANT\"]"
if [ "$3" = "--use" ]; then run AgentScripts/Hands/ArmSetup.cs ArmSetup.Use "[\"$VARIANT\"]"; fi
