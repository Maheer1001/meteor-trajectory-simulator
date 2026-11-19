#!/usr/bin/env sh
# Fails if any frozen path differs from the given base ref (default: 9d735f2, end of the logic round).
# Round 2 unfroze the menus and the input screen (MainMenu, MainMenu2, VariableInput and their scripts) for restyling;
# the shared pixel-art textures and the TextMesh Pro font assets stay frozen (copy them, never edit them).
# Usage: tools/check-frozen.sh [base-ref]
base="${1:-9d735f2}"
changed=$(git diff --name-only "$base" -- \
  "Assets/Textures/9sprite_8bit_circle*" "Assets/Textures/meteorshower-cover_q5gt.jpg*" \
  "Assets/TextMesh Pro")
if [ -n "$changed" ]; then
  echo "FROZEN PATHS CHANGED vs $base:"; echo "$changed"; exit 1
fi
echo "Frozen paths unchanged vs $base."
