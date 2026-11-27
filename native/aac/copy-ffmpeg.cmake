# Kopiert die FFmpeg-Bibliotheken aus PREFIX nach DEST, ohne Symlinks und unter dem Namen, den der Lader sucht:
# Linux libavcodec.so.63 (SONAME), macOS libavcodec.63.dylib, Windows avcodec-63.dll.
file(GLOB libraries "${PREFIX}/lib/libav*.so.*" "${PREFIX}/lib/libav*.*.dylib" "${PREFIX}/bin/av*.dll")
foreach(library IN LISTS libraries)
    get_filename_component(name "${library}" NAME)
    if(name MATCHES "^libav[a-z]+\\.so\\.[0-9]+$" OR name MATCHES "^libav[a-z]+\\.[0-9]+\\.dylib$"
       OR name MATCHES "^av[a-z]+-[0-9]+\\.dll$")
        file(REAL_PATH "${library}" target)
        file(COPY_FILE "${target}" "${DEST}/${name}" ONLY_IF_DIFFERENT)
    endif()
endforeach()
