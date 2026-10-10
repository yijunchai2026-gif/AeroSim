#!/bin/bash
set -euo pipefail
qa_root="$(cd "$(dirname "$0")/../.." && pwd)"
qa_app="${1:?Pass the isolated AeroSim-UIQA.app path}"
case "$qa_app" in /private/tmp/aerosim-uiqa.*/AeroSim-UIQA.app) ;; *) exit 2;; esac
qa_exe="$qa_app/Contents/MacOS/AeroSim"
qa_output="${2:-$qa_root/verification/ui-scale-qa-v04-ui-fix-20261007-release}"
case "$qa_output" in "$qa_root"/verification/ui-scale-qa-v04-ui-fix-*) ;; *) exit 2;; esac
while read -r qa_name qa_width qa_height qa_scales qa_stress qa_fullscreen; do
  qa_profile="$(mktemp -d /private/tmp/aerosim-layout-profile.XXXXXX)"
  mkdir -p "$qa_output/$qa_name"
  printf '%s requested=%sx%s scales=%s text-stress=%s\n' "$qa_name" "$qa_width" "$qa_height" "$qa_scales" "$qa_stress"
  "$qa_exe" --layout-qa "$qa_output/$qa_name" --qa-scales "$qa_scales" \
    --qa-text-stress "$qa_stress" --data-dir "$qa_profile" \
    -screen-width "$qa_width" -screen-height "$qa_height" -screen-fullscreen "${qa_fullscreen:-0}" \
    -logFile "$qa_output/$qa_name/player.log" > "$qa_output/$qa_name/stdout.log" 2>&1
done <<'QA_MATRIX'
desktop-1280x720 1280 720 0.8,0.9,1,1.1,1.25,1.5 true
desktop-1680x945 1680 945 0.8,0.9,1,1.1,1.25,1.5 false
desktop-1920x1080 1920 1080 0.8,0.9,1,1.1,1.25,1.5 true
desktop-2560x1440 2560 1440 0.8,0.9,1,1.1,1.25,1.5 false
desktop-3840x2160 3840 2160 0.8,0.9,1,1.1,1.25,1.5 false
desktop-4k-fullscreen 3840 2160 0.8,0.9,1,1.1,1.25,1.5 false 1
ultrawide-2520x1080 2520 1080 0.8,0.9,1,1.1,1.25,1.5 false
acceptance-2560x1080 2560 1080 0.8,0.9,1,1.1,1.25,1.5 false
tablet-1024x768 1024 768 1 false
phone-landscape-844x390 844 390 1 false
phone-portrait-390x844 390 844 1 false
dpi2-2048x1536 2048 1536 2 false
dpi3-2532x1170 2532 1170 3 false
QA_MATRIX
