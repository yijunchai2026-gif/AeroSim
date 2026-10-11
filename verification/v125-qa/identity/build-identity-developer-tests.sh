#!/bin/bash
# Pure identity developer checks only; no Unity, hardware, profile or product writes.
set -euo pipefail
qa_root="$(cd "$(dirname "$0")/../../.." && pwd)"
qa_unity="${AEROSIM_QA_UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents}"
qa_runtime="$qa_unity/Resources/Scripting/NetCoreRuntime/dotnet"
qa_compiler="$qa_unity/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
qa_mono="$qa_unity/Resources/Scripting/MonoBleedingEdge/bin/mono"
qa_references="$qa_unity/Resources/Scripting/MonoBleedingEdge/lib/mono/4.8-api"
qa_tmp="$(mktemp -d /private/tmp/aerosim-identity-developer.XXXXXX)"
for qa_tool in "$qa_runtime" "$qa_compiler" "$qa_mono" "$qa_references/mscorlib.dll"; do
  [[ -f "$qa_tool" ]] || { printf 'Missing compiler/runtime reference: %s\n' "$qa_tool" >&2; exit 2; }
done
"$qa_runtime" "$qa_compiler" -nologo -noconfig -nostdlib+ -langversion:latest -warn:4 -warnaserror+ -target:exe \
  "-out:$qa_tmp/InputDeviceIdentityDeveloperTests.exe" "-r:$qa_references/mscorlib.dll" \
  "-r:$qa_references/System.dll" "-r:$qa_references/System.Core.dll" \
  "$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/FlightControlFrame.cs" \
  "$qa_root/AeroSim/Assets/AeroSim/Runtime/Input/InputDeviceIdentity.cs" \
  "$qa_root/verification/v125-qa/identity/InputDeviceIdentityDeveloperTests.cs"
"$qa_mono" "$qa_tmp/InputDeviceIdentityDeveloperTests.exe"
printf 'Compiled developer test output: %s\n' "$qa_tmp"
