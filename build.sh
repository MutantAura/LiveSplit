#!/usr/bin/env bash
# Builds the cross-platform (Avalonia) LiveSplit for Linux or macOS.
#
# Usage: ./build.sh [single|aot] [runtime]
#   single  self-contained single-file executable (default)
#   aot     Native AOT executable: no JIT, faster startup, smaller and lower memory use.
#           Must be built on the target OS. Needs clang and zlib (Linux: apt install clang
#           zlib1g-dev) or the Xcode command line tools (macOS).
#   runtime defaults to the current machine, e.g. linux-x64, linux-arm64, osx-arm64, osx-x64.
#
# Output: artifacts/publish/<runtime>-<mode>/LiveSplit.Avalonia

set -euo pipefail

mode="single"
runtime=""
for arg in "$@"; do
    case "$arg" in
        single|aot) mode="$arg" ;;
        -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
        *) runtime="$arg" ;;
    esac
done

if [ -z "$runtime" ]; then
    case "$(uname -s)" in
        Linux) os="linux" ;;
        Darwin) os="osx" ;;
        *) echo "Unsupported OS $(uname -s); pass a runtime identifier." >&2; exit 1 ;;
    esac
    case "$(uname -m)" in
        x86_64|amd64) arch="x64" ;;
        arm64|aarch64) arch="arm64" ;;
        *) echo "Unsupported architecture $(uname -m); pass a runtime identifier." >&2; exit 1 ;;
    esac
    runtime="$os-$arch"
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo "The .NET SDK was not found. Install the .NET 10 SDK from https://dot.net and try again." >&2
    exit 1
fi

cd "$(dirname "$0")"

output="artifacts/publish/$runtime-$mode"
# Build in a dedicated artifacts folder (bin, obj and build output) so this neither overwrites
# nor shares intermediate files with regular builds or with builds for other OSes/runtimes.
work="$PWD/artifacts/publish-work/$runtime-$mode"

args=(
    --configuration Release
    --runtime "$runtime"
    --self-contained true
    --output "$output"
    "-p:ArtifactsPath=$work"
    -p:DebugType=none
    -p:DebugSymbols=false
)

if [ "$mode" = "aot" ]; then
    args+=(-p:PublishAot=true)
else
    args+=(
        -p:PublishSingleFile=true
        -p:IncludeNativeLibrariesForSelfExtract=true
        -p:IncludeAllContentForSelfExtract=true
        -p:EnableCompressionInSingleFile=true
    )
fi

echo "Building LiveSplit.Avalonia ($mode) for $runtime..."
rm -rf "$output"
dotnet publish src/LiveSplit.Avalonia/LiveSplit.Avalonia.csproj "${args[@]}"

# Native libraries from NuGet may bring their own debug symbols; they aren't needed to run.
rm -f "$output"/*.pdb "$output"/*.dbg

echo
echo "Done: $PWD/$output/LiveSplit.Avalonia"
