#!/bin/bash
# No Unity/player launch, device polling, credentials or actual profile access.
# Optional source path is for an isolated compiled mutation, never production edits.
set -euo pipefail
qa_registry_root="$(cd "$(dirname "$0")/../../.." && pwd)"
qa_registry_unity="${AEROSIM_QA_UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents}"
qa_registry_runtime="$qa_registry_unity/Resources/Scripting/NetCoreRuntime/dotnet"
qa_registry_compiler="$qa_registry_unity/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
qa_registry_mono="$qa_registry_unity/Resources/Scripting/MonoBleedingEdge/bin/mono"
qa_registry_refs="$qa_registry_unity/Resources/Scripting/MonoBleedingEdge/lib/mono/4.8-api"
qa_registry_source="${1:-$qa_registry_root/AeroSim/Assets/AeroSim/Runtime/Input/InputIdentityRegistry.cs}"
[[ $# -le 1 ]] || { printf 'Usage: %s [isolated-registry-source.cs]\n' "$0" >&2; exit 2; }
for qa_registry_tool in "$qa_registry_runtime" "$qa_registry_compiler" "$qa_registry_mono" "$qa_registry_refs/mscorlib.dll" "$qa_registry_refs/System.Web.Extensions.dll" "$qa_registry_source"; do
  [[ -f "$qa_registry_tool" ]] || { printf 'Missing compiler/runtime/source: %s\n' "$qa_registry_tool" >&2; exit 2; }
done
qa_registry_tmp="$(mktemp -d /private/tmp/aerosim-registry-independent.XXXXXX)"
"$qa_registry_runtime" "$qa_registry_compiler" -nologo -noconfig -nostdlib+ -langversion:latest -warn:4 -warnaserror+ -target:exe \
  "-out:$qa_registry_tmp/InputIdentityRegistryIndependentTests.exe" \
  "-r:$qa_registry_refs/mscorlib.dll" "-r:$qa_registry_refs/System.dll" "-r:$qa_registry_refs/System.Core.dll" "-r:$qa_registry_refs/System.Web.Extensions.dll" \
  "$qa_registry_root/AeroSim/Assets/AeroSim/Runtime/Input/FlightControlFrame.cs" \
  "$qa_registry_root/AeroSim/Assets/AeroSim/Runtime/Input/InputDeviceIdentity.cs" \
  "$qa_registry_source" "$qa_registry_root/verification/v125-qa/registry/InputIdentityRegistryIndependentTests.cs"
printf 'STRICT_COMPILATION_SUCCESS %s\n' "$qa_registry_tmp/InputIdentityRegistryIndependentTests.exe"
"$qa_registry_mono" "$qa_registry_tmp/InputIdentityRegistryIndependentTests.exe" \
  "$qa_registry_root/verification/v125-qa/registry/prepared-vectors.json" \
  "$qa_registry_root/verification/v125-qa/registry/prepared-vectors-addendum-v3.json"
