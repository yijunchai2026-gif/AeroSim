#!/bin/bash
# Build an independent QA COPY only. Never starts Unity or the native application.
set -euo pipefail
qa_root="$(cd "$(dirname "$0")/../.." && pwd)"
qa_original="${1:?Pass the explicit uninstrumented candidate AeroSim.app path}"
[[ "$qa_original" = /*/AeroSim.app && -d "$qa_original/Contents/Resources/Data/Managed" ]] || exit 2
qa_unity="${AEROSIM_QA_UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents}"
qa_runtime="$qa_unity/Resources/Scripting/NetCoreRuntime/dotnet"
qa_compiler="$qa_unity/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
qa_mono="$qa_unity/Resources/Scripting/MonoBleedingEdge/bin/mono"
qa_cecil="$qa_unity/Resources/BuildPipeline/Compilation/ApiUpdater/Mono.Cecil.dll"
qa_tmp="$(mktemp -d /private/tmp/aerosim-space-submit.XXXXXX)"
qa_app="$qa_tmp/AeroSim-UIQA.app"
cp -R "$qa_original" "$qa_app"
qa_managed="$qa_app/Contents/Resources/Data/Managed"
[[ ! -e "$qa_managed/LayoutQa.dll" ]] || { printf 'Refusing previously instrumented candidate\n' >&2; exit 2; }
qa_refs=()
for qa_ref in "$qa_managed"/*.dll; do qa_refs+=("-r:$qa_ref"); done
"$qa_runtime" "$qa_compiler" -nologo -noconfig -nostdlib+ -langversion:latest -target:library \
  "-out:$qa_managed/LayoutQa.dll" "${qa_refs[@]}" "$qa_root/verification/v1-qa/SpaceSubmitProbe.cs"
"$qa_runtime" "$qa_compiler" -nologo -noconfig -nostdlib+ -target:exe \
  "-out:$qa_tmp/Instrument.exe" "${qa_refs[@]}" "-r:$qa_cecil" "$qa_root/verification/ui-qa-tools/Instrument.cs"
cp "$qa_cecil" "$qa_tmp/Mono.Cecil.dll"
"$qa_mono" "$qa_tmp/Instrument.exe" "$qa_managed/AeroSim.Runtime.dll" "$qa_managed/LayoutQa.dll"
codesign --force --deep --sign - "$qa_app"
printf '%s\n' "$qa_app"
