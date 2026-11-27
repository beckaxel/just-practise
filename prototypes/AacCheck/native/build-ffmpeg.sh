#!/bin/sh
# Baut FFmpeg im aktuellen Ordner (out of tree) und installiert es nach --prefix.
#
#   build-ffmpeg.sh <quellordner> <jobs> <configure-optionen>...
#
# Läuft unter Linux und macOS mit sh, unter Windows in der Bash von MSYS2 (aufgerufen von CMakeLists.txt).
set -e

source_dir=$1
jobs=$2
shift 2

if command -v cygpath >/dev/null 2>&1; then
    source_dir=$(cygpath -u "$source_dir")
fi
for tool in make cmp; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "$tool fehlt. Unter MSYS2: pacman -S make diffutils" >&2
        exit 1
    fi
done

"$source_dir/configure" "$@"
make -j"$jobs"
make install
