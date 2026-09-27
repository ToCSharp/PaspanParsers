#!/usr/bin/env bash
# Downloads the C# corpora of the parser benchmarks (src/PaspanParsers.Benchmarks) at fixed commits,
# so that measurements taken at different times compare the same files.
#
#   scripts/get-csharp-bench-corpora.sh <directory>
#   CSHARP_BENCH_CORPORA=<directory> dotnet run -c Release --project src/PaspanParsers.Benchmarks -- --filter "*"
#
# Each corpus is a subdirectory of <directory>; only its .cs files are checked out.
set -euo pipefail

target="${1:?usage: $0 <directory>}"
mkdir -p "$target"

fetch() {
    local name="$1" repository="$2" commit="$3"
    shift 3
    local directory="$target/$name"

    if [ -d "$directory/.git" ] && [ "$(git -C "$directory" rev-parse HEAD 2>/dev/null)" = "$commit" ]; then
        echo "$name: already at $commit"
        return
    fi

    rm -rf "$directory"
    git init -q "$directory"
    git -C "$directory" remote add origin "$repository"
    git -C "$directory" sparse-checkout set --no-cone "$@"
    git -C "$directory" fetch -q --depth 1 --filter=blob:none origin "$commit"
    git -C "$directory" checkout -q FETCH_HEAD
    echo "$name: $(find "$directory" -name '*.cs' | wc -l) files at $commit"
}

fetch roslyn https://github.com/dotnet/roslyn.git 90083ecf59c688110a69cbf3871e5edb2693aadc \
    'src/Compilers/CSharp/Portable/**/*.cs' \
    'src/Compilers/Core/Portable/**/*.cs'

fetch runtime https://github.com/dotnet/runtime.git 33baf8ee337b20dd0f184b69a6f09be92850bf9e \
    'src/libraries/System.Private.CoreLib/src/**/*.cs' \
    'src/libraries/System.Text.Json/src/**/*.cs' \
    'src/libraries/System.Linq/src/**/*.cs' \
    'src/libraries/System.Collections/src/**/*.cs'

fetch aspnetcore https://github.com/dotnet/aspnetcore.git c7cef3bfadeb7416a67e6b06bf2a736dd540806b \
    'src/Mvc/**/src/**/*.cs' \
    'src/Http/**/src/**/*.cs'
