#!/usr/bin/env sh
# Fails if any frozen path differs from the given base ref (default: 9d735f2, end of the logic round).
# Usage: tools/check-frozen.sh [base-ref]
base="${1:-9d735f2}"
changed=$(git diff --name-only "$base" -- \
  "Assets/Scenes/MainMenu.unity" "Assets/Scenes/MainMenu.unity.meta" \
  "Assets/Scenes/VariableInput.unity" "Assets/Scenes/VariableInput.unity.meta" \
  "Assets/Scenes/MainMenu2.unity" "Assets/Scenes/MainMenu2.unity.meta" \
  "Assets/Textures/9sprite_8bit_circle*" "Assets/Textures/meteorshower-cover_q5gt.jpg*" \
  "Assets/TextMesh Pro" \
  "Assets/Scripts/Main menu" "Assets/Scripts/Inputs")
if [ -n "$changed" ]; then
  echo "FROZEN PATHS CHANGED vs $base:"; echo "$changed"; exit 1
fi
echo "Frozen paths unchanged vs $base."
