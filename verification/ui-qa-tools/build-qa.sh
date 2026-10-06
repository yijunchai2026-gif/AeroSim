#!/bin/bash
set -euo pipefail
qa_root="$(cd "$(dirname "$0")/../.." && pwd)"
qa_unity="/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents"
qa_original="$qa_root/AeroSim/Builds/macOS-AircraftV3/AeroSim.app"
qa_runtime="$qa_unity/Resources/Scripting/NetCoreRuntime/dotnet"
qa_compiler="$qa_unity/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
qa_mono="$qa_unity/Resources/Scripting/MonoBleedingEdge/bin/mono"
qa_cecil="$qa_unity/Resources/BuildPipeline/Compilation/ApiUpdater/Mono.Cecil.dll"
qa_tmp="$(mktemp -d /private/tmp/aerosim-uiqa.XXXXXX)"
qa_app="$qa_tmp/AeroSim-UIQA.app"
cp -R "$qa_original" "$qa_app"
qa_managed="$qa_app/Contents/Resources/Data/Managed"
qa_refs=()
for qa_ref in "$qa_managed"/*.dll; do qa_refs+=("-r:$qa_ref"); done
"$qa_runtime" "$qa_compiler" -nologo -noconfig -nostdlib+ -langversion:latest -target:library \
  "-out:$qa_managed/LayoutQa.dll" "${qa_refs[@]}" "$qa_root/verification/ui-qa-tools/LayoutQa.cs"
"$qa_runtime" "$qa_compiler" -nologo -noconfig -nostdlib+ -target:exe \
  "-out:$qa_tmp/Instrument.exe" "${qa_refs[@]}" "-r:$qa_cecil" "$qa_root/verification/ui-qa-tools/Instrument.cs"
cp "$qa_cecil" "$qa_tmp/Mono.Cecil.dll"
"$qa_mono" "$qa_tmp/Instrument.exe" "$qa_managed/AeroSim.Runtime.dll" "$qa_managed/LayoutQa.dll"
codesign --force --deep --sign - "$qa_app"
printf '%s\n' "$qa_tmp"
