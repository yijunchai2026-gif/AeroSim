#!/bin/bash
set -euo pipefail
qa_root="$(cd "$(dirname "$0")/../.." && pwd)"
qa_app="${1:?Pass an isolated instrumented AeroSim-UIQA.app path}"
case "$qa_app" in /private/tmp/aerosim-uiqa.*/AeroSim-UIQA.app) ;; *) printf 'Refusing non-QA app\n' >&2; exit 2;; esac
qa_output="${2:-$qa_root/verification/v1-qa/results/native}"
case "$qa_output" in "$qa_root"/verification/v1-qa/results/*) ;; *) printf 'Output must be inside verification/v1-qa/results\n' >&2; exit 2;; esac
qa_exe="$qa_app/Contents/MacOS/AeroSim"
qa_python="${AEROSIM_QA_PYTHON:-/Users/bytedance/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3}"
qa_failed_runs=0
while read -r qa_name qa_width qa_height qa_scales qa_text; do
  qa_profile="$(mktemp -d /private/tmp/aerosim-v1-layout-profile.XXXXXX)"
  mkdir -p "$qa_output/$qa_name"
  printf '%s requested=%sx%s UI/HUD=%s Text=%s\n' "$qa_name" "$qa_width" "$qa_height" "$qa_scales" "$qa_text"
  if [[ -f "$qa_output/$qa_name/report.json" ]]; then
    printf 'Refusing stale report reuse: %s; choose a fresh output directory\n' "$qa_output/$qa_name/report.json" >&2
    exit 2
  fi
  if "$qa_exe" --layout-qa "$qa_output/$qa_name" --qa-scales "$qa_scales" \
    --qa-text-scales "$qa_text" --data-dir "$qa_profile" \
    -screen-width "$qa_width" -screen-height "$qa_height" -screen-fullscreen 0 \
    -logFile "$qa_output/$qa_name/player.log" > "$qa_output/$qa_name/stdout.log" 2>&1; then
    qa_exitcode=0
  else
    qa_exitcode=$?
    qa_failed_runs=$((qa_failed_runs + 1))
    printf 'Native config %s exited %s; retaining evidence and continuing\n' "$qa_name" "$qa_exitcode" >&2
  fi
  "$qa_python" "$qa_root/verification/v1-qa/aggregate-native.py" --record-run "$qa_name" \
    --exitcode "$qa_exitcode" --output "$qa_output"
done <<'QA_MATRIX'
desktop-1920x1080 1920 1080 0.8,0.9,1,1.1,1.25,1.5 1,1.25,1.5,2
desktop-1280x720 1280 720 0.8,0.9,1,1.1,1.25,1.5 1,1.25,1.5,2
acceptance-2560x1080 2560 1080 0.8,0.9,1,1.1,1.25,1.5 1,1.25,1.5,2
desktop-3840x2160 3840 2160 0.8,0.9,1,1.1,1.25,1.5 1,1.25,1.5,2
tablet-1024x768 1024 768 0.8,1,1.5 1,2
phone-landscape-844x390 844 390 0.8,1,1.5 1,2
phone-portrait-390x844 390 844 0.8,1,1.5 1,2
QA_MATRIX
if "$qa_python" "$qa_root/verification/v1-qa/aggregate-native.py" --output "$qa_output"; then
  qa_summary_exit=0
else
  qa_summary_exit=$?
fi
if [[ "$qa_failed_runs" -gt 0 || "$qa_summary_exit" -ne 0 ]]; then
  exit 2
fi
