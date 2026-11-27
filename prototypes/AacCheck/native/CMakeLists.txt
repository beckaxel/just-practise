cmake_minimum_required(VERSION 3.24)

# Prüfpunkt 4: C-Wrapper um ein schlankes, selbst gebautes FFmpeg (nur M4A/MP4 mit AAC und ALAC), per P/Invoke.
#
#   cmake -S prototypes/AacCheck/native -B prototypes/AacCheck/native/build
#   cmake --build prototypes/AacCheck/native/build --config Release
#
# FFmpeg steht unter LGPL 2.1 oder neuer und wird deshalb nur als eigene, austauschbare Bibliothek gebaut
# (--enable-shared, nie statisch, ohne --enable-gpl). Alles landet in native/bin/<rid>/.
#
# Linux und macOS: configure und make des Systems.
# Windows: configure braucht eine Unix-Shell. Es läuft in der Bash von MSYS2 (winget install MSYS2.MSYS2, dann in
# MSYS2: pacman -S make diffutils) und kompiliert mit MSVC. Dazu cmake aus der "x64 Native Tools Command Prompt"
# bzw. der "Developer PowerShell" von Visual Studio aufrufen, damit cl.exe und link.exe im PATH stehen.

project(jpaac VERSION 1.0.0 LANGUAGES C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

if(NOT CMAKE_CONFIGURATION_TYPES AND NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
endif()

# Wie jpstretch: unter Windows die C-Laufzeit statisch einbinden, damit kein VC-Redistributable nötig ist.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

set(JP_FFMPEG_VERSION 9.1)
set(JP_FFMPEG_SHA256 1a4d394ae63d190a526cd052eb4415c156b5c5e2df37efab41a7de0b8fde2433)
# Ergebnis von Prüfpunkt 4: --enable-small und ohne x86-Assembly. Ein Drittel kleiner (1,4 statt 2,1 MiB unter Linux),
# immer noch rund 600-mal schneller als Echtzeit, und der Bau braucht kein nasm.
option(JP_FFMPEG_SMALL "FFmpeg mit --enable-small bauen" ON)
option(JP_FFMPEG_X86ASM "FFmpeg mit x86-Assembly bauen (braucht nasm)" OFF)
set(JP_FFMPEG_EXTRA_OPTIONS "" CACHE STRING "Weitere Optionen für configure")

set(JP_FFMPEG_PREFIX "${CMAKE_CURRENT_BINARY_DIR}/ffmpeg")
set(JP_FFMPEG_OPTIONS
    --prefix=${JP_FFMPEG_PREFIX}
    --enable-shared --disable-static --enable-pic
    --disable-autodetect --disable-programs --disable-doc --disable-network --disable-debug
    --disable-everything
    --disable-avdevice --disable-avfilter --disable-swscale --disable-swresample
    --enable-protocol=file
    --enable-demuxer=mov
    --enable-decoder=aac --enable-decoder=alac
)
if(JP_FFMPEG_SMALL)
    list(APPEND JP_FFMPEG_OPTIONS --enable-small)
endif()
if(NOT JP_FFMPEG_X86ASM)
    list(APPEND JP_FFMPEG_OPTIONS --disable-x86asm)
endif()
list(APPEND JP_FFMPEG_OPTIONS ${JP_FFMPEG_EXTRA_OPTIONS})

include(ProcessorCount)
ProcessorCount(JP_JOBS)
if(JP_JOBS EQUAL 0)
    set(JP_JOBS 2)
endif()

if(MSVC)
    set(JP_MSYS2_ROOT "C:/msys64" CACHE PATH "Ordner von MSYS2")
    set(JP_BASH "${JP_MSYS2_ROOT}/usr/bin/bash.exe")
    if(NOT EXISTS "${JP_BASH}")
        message(FATAL_ERROR "MSYS2 fehlt in ${JP_MSYS2_ROOT}: winget install MSYS2.MSYS2, dann in MSYS2: pacman -S make diffutils "
                            "(oder -DJP_MSYS2_ROOT=<ordner>)")
    endif()
    find_program(JP_CL cl.exe)
    if(NOT JP_CL)
        message(FATAL_ERROR "cl.exe steht nicht im PATH. cmake aus der \"x64 Native Tools Command Prompt\" oder der "
                            "\"Developer PowerShell\" von Visual Studio aufrufen.")
    endif()
    # -MT: statische C-Laufzeit wie bei jpaac. Die Import-Bibliotheken (.lib) legt FFmpeg unter Windows nach bin/.
    set(JP_FFMPEG_COMMAND
        ${CMAKE_COMMAND} -E env MSYS2_PATH_TYPE=inherit CHERE_INVOKING=1 MSYSTEM=MSYS
        "${JP_BASH}" -l "${CMAKE_CURRENT_SOURCE_DIR}/build-ffmpeg.sh" <SOURCE_DIR> ${JP_JOBS}
        --toolchain=msvc --extra-cflags=-MT ${JP_FFMPEG_OPTIONS})
    set(JP_FFMPEG_LIBRARIES
        ${JP_FFMPEG_PREFIX}/bin/avformat.lib ${JP_FFMPEG_PREFIX}/bin/avcodec.lib ${JP_FFMPEG_PREFIX}/bin/avutil.lib)
else()
    set(JP_FFMPEG_COMMAND sh "${CMAKE_CURRENT_SOURCE_DIR}/build-ffmpeg.sh" <SOURCE_DIR> ${JP_JOBS} ${JP_FFMPEG_OPTIONS})
    set(JP_FFMPEG_LIBRARIES
        ${JP_FFMPEG_PREFIX}/lib/${CMAKE_SHARED_LIBRARY_PREFIX}avformat${CMAKE_SHARED_LIBRARY_SUFFIX}
        ${JP_FFMPEG_PREFIX}/lib/${CMAKE_SHARED_LIBRARY_PREFIX}avcodec${CMAKE_SHARED_LIBRARY_SUFFIX}
        ${JP_FFMPEG_PREFIX}/lib/${CMAKE_SHARED_LIBRARY_PREFIX}avutil${CMAKE_SHARED_LIBRARY_SUFFIX})
endif()

include(ExternalProject)
ExternalProject_Add(ffmpeg
    URL https://ffmpeg.org/releases/ffmpeg-${JP_FFMPEG_VERSION}.tar.xz
    URL_HASH SHA256=${JP_FFMPEG_SHA256}
    DOWNLOAD_EXTRACT_TIMESTAMP ON
    CONFIGURE_COMMAND ""
    BUILD_COMMAND ${JP_FFMPEG_COMMAND}
    INSTALL_COMMAND ""
    BUILD_BYPRODUCTS ${JP_FFMPEG_LIBRARIES}
)

add_library(jpaac SHARED src/jp_aac.c)
add_dependencies(jpaac ffmpeg)
target_include_directories(jpaac PUBLIC include PRIVATE ${JP_FFMPEG_PREFIX}/include)
target_compile_definitions(jpaac PRIVATE JP_AAC_BUILD)
target_link_libraries(jpaac PRIVATE ${JP_FFMPEG_LIBRARIES})
if(MSVC)
    target_compile_options(jpaac PRIVATE /W4 /O2)
else()
    target_compile_options(jpaac PRIVATE -Wall -Wextra -O2)
endif()
# Nur jpaac sucht im eigenen Ordner ($ORIGIN). Es braucht alle drei FFmpeg-Bibliotheken direkt; der Lader lädt sie
# zuerst und findet sie so auch für die Abhängigkeiten untereinander. Unter Windows sucht der Lader ohnehin im
# Ordner des Programms.
set_target_properties(jpaac PROPERTIES C_VISIBILITY_PRESET hidden BUILD_WITH_INSTALL_RPATH ON INSTALL_RPATH "\$ORIGIN")
if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    target_link_options(jpaac PRIVATE -static-libgcc)
endif()

# .NET-Runtime-Identifier für den Ausgabeordner, wie in native/stretch.
if(WIN32)
    set(JP_OS win)
elseif(APPLE)
    set(JP_OS osx)
else()
    set(JP_OS linux)
endif()
string(TOLOWER "${CMAKE_SYSTEM_PROCESSOR}" JP_CPU)
if(JP_CPU MATCHES "^(arm64|aarch64)$" OR CMAKE_GENERATOR_PLATFORM STREQUAL "ARM64")
    set(JP_ARCH arm64)
else()
    set(JP_ARCH x64)
endif()
set(JP_RID "${JP_OS}-${JP_ARCH}" CACHE STRING ".NET-Runtime-Identifier des Ausgabeordners")
set(JP_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../../native/bin/${JP_RID}")
set_target_properties(jpaac PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${JP_OUTPUT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY "${JP_OUTPUT_DIR}"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
)
foreach(config Debug Release RelWithDebInfo MinSizeRel)
    string(TOUPPER ${config} CONFIG)
    set_target_properties(jpaac PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY_${CONFIG} "${JP_OUTPUT_DIR}"
        RUNTIME_OUTPUT_DIRECTORY_${CONFIG} "${JP_OUTPUT_DIR}"
    )
endforeach()

# Die FFmpeg-Bibliotheken unter dem Namen, den der Lader sucht (libavcodec.so.63, avcodec-63.dll), daneben legen.
add_custom_command(TARGET jpaac POST_BUILD
    COMMAND ${CMAKE_COMMAND} -DPREFIX=${JP_FFMPEG_PREFIX} -DDEST=${JP_OUTPUT_DIR} -P ${CMAKE_CURRENT_SOURCE_DIR}/copy-ffmpeg.cmake
)
