cmake_minimum_required(VERSION 3.24)

# C-Wrapper um signalsmith-stretch, angesprochen per P/Invoke.
#
#   cmake -S native/stretch -B native/stretch/build
#   cmake --build native/stretch/build --config Release
#
# Die Bibliothek landet in native/bin/<rid>/, von dort holen sie die .NET-Projekte.

project(jpstretch VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

if(NOT CMAKE_CONFIGURATION_TYPES AND NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
endif()

# Unter Windows die C++-Laufzeit statisch einbinden, damit kein VC-Redistributable nötig ist.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

# Feste Versionen, angegeben als Commit. Beide Bibliotheken stehen unter MIT.
include(FetchContent)
FetchContent_Declare(
    signalsmith-linear
    GIT_REPOSITORY https://github.com/Signalsmith-Audio/linear.git
    GIT_TAG de55e6a50ffcf6f8f43f649692d94691c7025151 # 0.6.4
)
FetchContent_Declare(
    signalsmith-stretch
    GIT_REPOSITORY https://github.com/Signalsmith-Audio/signalsmith-stretch.git
    GIT_TAG a670068d9aeb64913331d5cc29337b19a457a7df # 1.4.0
)
FetchContent_MakeAvailable(signalsmith-linear signalsmith-stretch)

add_library(jpstretch SHARED src/jp_stretch.cpp)
target_include_directories(jpstretch PUBLIC include)
target_compile_definitions(jpstretch PRIVATE JP_STRETCH_BUILD)
target_link_libraries(jpstretch PRIVATE signalsmith-stretch)

if(MSVC)
    target_compile_options(jpstretch PRIVATE /W4 /O2 /bigobj)
else()
    target_compile_options(jpstretch PRIVATE -Wall -Wextra -O3)
endif()

if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    # Keine Abhängigkeit von der libstdc++-Version des Systems (wichtig für das AppImage).
    target_link_options(jpstretch PRIVATE -static-libstdc++ -static-libgcc)
endif()

# .NET-Runtime-Identifier für den Ausgabeordner bestimmen.
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

set(JP_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../bin/${JP_RID}")
set_target_properties(jpstretch PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${JP_OUTPUT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY "${JP_OUTPUT_DIR}"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
)
foreach(config Debug Release RelWithDebInfo MinSizeRel)
    string(TOUPPER ${config} CONFIG)
    set_target_properties(jpstretch PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY_${CONFIG} "${JP_OUTPUT_DIR}"
        RUNTIME_OUTPUT_DIRECTORY_${CONFIG} "${JP_OUTPUT_DIR}"
    )
endforeach()
