#!/bin/bash
# Independent pure C# QA only; optional source override is for isolated mutations.
# Does not launch Unity or edit implementation, fixtures, catalog, or review page.
set -euo pipefail
qa_root="$(cd "$(dirname "$0")/../.." && pwd)"
qa_unity="${AEROSIM_QA_UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents}"
qa_runtime="$qa_unity/Resources/Scripting/NetCoreRuntime/dotnet"
qa_compiler="$qa_unity/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
qa_mono="$qa_unity/Resources/Scripting/MonoBleedingEdge/bin/mono"
qa_references="$qa_unity/Resources/Scripting/MonoBleedingEdge/lib/mono/4.8-api"
qa_source="${1:-$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/TouchInputState.cs}"
[[ $# -le 1 ]] || { printf 'Usage: %s [isolated-touch-source.cs]\n' "$0" >&2; exit 2; }
for qa_tool in "$qa_runtime" "$qa_compiler" "$qa_mono" "$qa_references/mscorlib.dll" "$qa_source"; do
  [[ -f "$qa_tool" ]] || { printf 'Missing compiler/runtime/source: %s\n' "$qa_tool" >&2; exit 2; }
done
qa_tmp="$(mktemp -d /private/tmp/aerosim-touch-independent.XXXXXX)"
"$qa_runtime" "$qa_compiler" -nologo -noconfig -nostdlib+ -langversion:latest -warn:4 -warnaserror+ -target:exe \
  "-out:$qa_tmp/TouchInputStateIndependentTests.exe" "-r:$qa_references/mscorlib.dll" "-r:$qa_references/System.dll" \
  "-r:$qa_references/System.Core.dll" \
  "$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/FlightControlFrame.cs" \
  "$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/InputRouter.cs" \
  "$qa_source" \
  "$qa_root/verification/v125-qa/TouchInputStateIndependentTests.cs"
printf 'Strict compilation passed; isolated executable: %s\n' "$qa_tmp"
"$qa_mono" "$qa_tmp/TouchInputStateIndependentTests.exe"
