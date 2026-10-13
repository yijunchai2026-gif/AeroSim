#!/bin/bash
# Frozen independent JSON consumer; optional source only for /private/tmp mutations.
set -euo pipefail
qa_root="$(cd "$(dirname "$0")/../../.." && pwd)"
qa_dir="$qa_root/verification/v125-qa/raw-mapping"
qa_unity="/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents"
qa_runtime="$qa_unity/Resources/Scripting/NetCoreRuntime/dotnet"
qa_compiler="$qa_unity/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
qa_mono="$qa_unity/Resources/Scripting/MonoBleedingEdge/bin/mono"
qa_refs="$qa_unity/Resources/Scripting/MonoBleedingEdge/lib/mono/4.8-api"
qa_source="${1:-$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/RawInputMapping.cs}"
[[ $# -le 1 ]] || { printf 'Usage: %s [isolated-mapping-source.cs]\n' "$0" >&2; exit 2; }
for qa_tool in "$qa_runtime" "$qa_compiler" "$qa_mono" "$qa_source" "$qa_refs/System.Web.Extensions.dll"; do
  [[ -f "$qa_tool" ]] || { printf 'Missing runtime/reference/source: %s\n' "$qa_tool" >&2; exit 2; }
done
qa_tmp="$(mktemp -d /private/tmp/aerosim-raw-mapping-independent.XXXXXX)"
printf 'OUTPUT_DIRECTORY=%s\n' "$qa_tmp"
"$qa_runtime" "$qa_compiler" -nologo -noconfig -nostdlib+ -langversion:latest -warn:4 -warnaserror+ -target:exe \
  "-out:$qa_tmp/IndependentRawMappingQa.exe" "-r:$qa_refs/mscorlib.dll" "-r:$qa_refs/System.dll" \
  "-r:$qa_refs/System.Core.dll" "-r:$qa_refs/System.Web.Extensions.dll" \
  "$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/FlightControlFrame.cs" \
  "$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/InputDeviceIdentity.cs" "$qa_source" "$qa_dir/independent-driver.cs"
printf 'COMPILE_EXIT=0\n'
set +e
"$qa_mono" "$qa_tmp/IndependentRawMappingQa.exe" "$qa_dir" "$qa_source" 2>&1 | tee "$qa_tmp/runner.log"
qa_run_exit=${PIPESTATUS[0]}
set -e
printf 'RUNNER_EXIT=%s\n' "$qa_run_exit"
exit "$qa_run_exit"
