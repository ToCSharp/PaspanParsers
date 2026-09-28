#!/usr/bin/env bash
# Fetches the external C++ corpora of the clang oracle into a directory and prints the commands that
# measure them: {fmt}, nlohmann/json (single include) and LLVM's llvm/include/llvm/ADT and llvm/lib/Support.
# LLVM needs the headers that CMake generates (llvm/Config/*.h); they are written with the settings of
# an x86-64 Linux host.
#
#   src/PaspanParsers.Tests/Cpp/fetch-external-corpora.sh /tmp/cpp-corpora
set -euo pipefail

target=${1:?usage: fetch-external-corpora.sh DIRECTORY}
mkdir -p "$target"
cd "$target"

[ -d fmt ] || git clone --depth 1 https://github.com/fmtlib/fmt.git
if [ ! -d json ]; then
  git clone --depth 1 --filter=blob:none --sparse https://github.com/nlohmann/json.git
  git -C json sparse-checkout set single_include
fi
if [ ! -d llvm-project ]; then
  git clone --depth 1 --filter=blob:none --sparse https://github.com/llvm/llvm-project.git
  git -C llvm-project sparse-checkout set --no-cone /llvm/include/ /llvm/lib/Support/
fi

# llvm/Config/{llvm-config,abi-breaking,config}.h from their .cmake templates
python3 - llvm-project/llvm/include/llvm/Config llvm-generated/llvm/Config <<'EOF'
import os, re, sys
source, output = sys.argv[1], sys.argv[2]
values = {
    'LLVM_DEFAULT_TARGET_TRIPLE': 'x86_64-unknown-linux-gnu', 'LLVM_HOST_TRIPLE': 'x86_64-unknown-linux-gnu',
    'LLVM_NATIVE_ARCH': 'X86', 'LLVM_ON_UNIX': '1', 'LLVM_VERSION_MAJOR': '22', 'LLVM_VERSION_MINOR': '0',
    'LLVM_VERSION_PATCH': '0', 'PACKAGE_VERSION': '22.0.0git', 'PACKAGE_NAME': 'LLVM', 'PACKAGE_STRING': 'LLVM 22.0.0git',
    'PACKAGE_BUGREPORT': 'https://github.com/llvm/llvm-project/issues/', 'LTDL_SHLIB_EXT': '.so', 'LLVM_PLUGIN_EXT': '.so',
    'BACKTRACE_HEADER': 'execinfo.h', 'LLVM_ENABLE_THREADS': '1', 'LLVM_HAS_ATOMICS': '1', 'LLVM_ENABLE_DUMP': '1',
    'LLVM_ENABLE_BACKTRACES': '1', 'LLVM_ENABLE_CRASH_OVERRIDES': '1', 'ENABLE_CRASH_OVERRIDES': '1',
    'HAVE_BUILTIN_THREAD_POINTER': '1', 'HAVE_DECL_FE_ALL_EXCEPT': '1', 'HAVE_DECL_FE_INEXACT': '1',
    'HAVE_DECL_ARC4RANDOM': '0', 'HAVE_DECL_STRERROR_S': '0',
}
# The functions and headers of glibc
for name in ['BACKTRACE', 'DLADDR', 'DLOPEN', 'ERRNO_H', 'FCNTL_H', 'FUTIMENS', 'FUTIMES', 'GETAUXVAL', 'GETPAGESIZE',
             'GETRUSAGE', 'ISATTY', 'LIBPTHREAD', 'LINK_H', 'MALLINFO2', 'POSIX_SPAWN', 'PTHREAD_GETNAME_NP', 'PTHREAD_H',
             'PTHREAD_MUTEX_LOCK', 'PTHREAD_RWLOCK_INIT', 'PTHREAD_SETNAME_NP', 'REGISTER_FRAME', 'DEREGISTER_FRAME', 'SBRK',
             'SETENV', 'SIGALTSTACK', 'SIGNAL_H', 'STRERROR_R', 'STRUCT_STAT_ST_MTIM_TV_NSEC', 'SYSCONF', 'SYSEXITS_H',
             'SYS_IOCTL_H', 'SYS_MMAN_H', 'SYS_PARAM_H', 'SYS_RESOURCE_H', 'SYS_STAT_H', 'SYS_TIME_H', 'SYS_TYPES_H',
             'TERMIOS_H', 'UNISTD_H', 'DLFCN_H']:
    values['HAVE_' + name] = '1'

def substitute(text):
    return re.sub(r'\$\{(\w+)\}', lambda m: values.get(m.group(1), ''), text)

os.makedirs(output, exist_ok=True)
for name in ['llvm-config.h', 'abi-breaking.h', 'config.h']:
    lines = []
    for line in open(os.path.join(source, name + '.cmake')).read().split('\n'):
        m = re.match(r'#cmakedefine01 (\w+)', line)
        if m:
            lines.append(f'#define {m.group(1)} {1 if values.get(m.group(1), "0") not in ("0", "") else 0}')
            continue
        m = re.match(r'#cmakedefine (\w+)(.*)', line)
        if m:
            defined = values.get(m.group(1), '0') not in ('0', '')
            lines.append(f'#define {m.group(1)}{substitute(m.group(2))}' if defined else f'/* #undef {m.group(1)} */')
            continue
        lines.append(substitute(line))
    open(os.path.join(output, name), 'w').write('\n'.join(lines))
EOF

here=$(pwd)
llvm_include="$here/llvm-project/llvm/include:$here/llvm-generated"
filter='--filter "FullyQualifiedName~Cpp.CppCorpusTests.Oracle_ExternalCorpus"'
cat <<EOF
Corpora are in $here. From the repository root:

CPP_CORPUS_DIR=$here/fmt/include/fmt:$here/fmt/src CPP_CORPUS_INCLUDE=$here/fmt/include dotnet run --project src/PaspanParsers.Tests -- $filter
CPP_CORPUS_DIR=$here/json/single_include/nlohmann CPP_CORPUS_INCLUDE=$here/json/single_include dotnet run --project src/PaspanParsers.Tests -- $filter
CPP_CORPUS_DIR=$here/llvm-project/llvm/include/llvm/ADT CPP_CORPUS_INCLUDE=$llvm_include dotnet run --project src/PaspanParsers.Tests -- $filter
CPP_CORPUS_DIR=$here/llvm-project/llvm/lib/Support CPP_CORPUS_INCLUDE=$llvm_include dotnet run --project src/PaspanParsers.Tests -- $filter
EOF
