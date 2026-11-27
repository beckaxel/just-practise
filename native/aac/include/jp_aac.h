/*
 * jp_aac: schlanke C-Schnittstelle um FFmpeg (libavformat/libavcodec) für P/Invoke.
 *
 * Liest M4A/MP4 mit AAC oder ALAC und liefert float, interleaved (L R L R ...), Längen in Frames.
 * Encoder-Verzögerung und Auffüllung am Ende entfernt FFmpeg anhand der Angaben in der Datei.
 * Ein Objekt gehört immer genau einem Thread.
 */
#ifndef JP_AAC_H
#define JP_AAC_H

#include <stdint.h>

#ifdef _WIN32
#  ifdef JP_AAC_BUILD
#    define JP_AAC_API __declspec(dllexport)
#  else
#    define JP_AAC_API __declspec(dllimport)
#  endif
#else
#  define JP_AAC_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define JP_AAC_API_VERSION 1

/* Fehlercodes (negativ) */
#define JP_AAC_ERROR_OPEN -1      /* Datei nicht lesbar oder kein unterstütztes Format */
#define JP_AAC_ERROR_NO_AUDIO -2  /* keine Tonspur mit AAC oder ALAC */
#define JP_AAC_ERROR_DECODER -3   /* Decoder ließ sich nicht öffnen */
#define JP_AAC_ERROR_MEMORY -4
#define JP_AAC_ERROR_DECODE -5    /* Fehler mitten in der Datei */

typedef struct jp_aac jp_aac;

JP_AAC_API int jp_aac_api_version(void);

/* Versionen der geladenen FFmpeg-Bibliotheken als Text, z. B. für den Über-Dialog. */
JP_AAC_API const char *jp_aac_ffmpeg_version(void);

/* Öffnet path (UTF-8). Liefert 0 und das Objekt in *out oder einen Fehlercode. */
JP_AAC_API int jp_aac_open(const char *path, jp_aac **out);
JP_AAC_API void jp_aac_close(jp_aac *aac);

JP_AAC_API int jp_aac_channels(const jp_aac *aac);
JP_AAC_API int jp_aac_sample_rate(const jp_aac *aac);

/* Länge laut Datei in Frames, -1 wenn unbekannt. Die tatsächlich gelesene Zahl kann leicht abweichen. */
JP_AAC_API int64_t jp_aac_length(const jp_aac *aac);

/* Name des Codecs, z. B. "aac" oder "alac", und das Profil, z. B. "LC" oder "HE-AAC" (oder ""). */
JP_AAC_API const char *jp_aac_codec(const jp_aac *aac);
JP_AAC_API const char *jp_aac_profile(const jp_aac *aac);

/* Liest bis zu max_frames Frames nach buffer (max_frames * channels floats).
 * Liefert die Zahl der Frames, 0 am Ende oder einen Fehlercode. */
JP_AAC_API int jp_aac_read(jp_aac *aac, float *buffer, int max_frames);

#ifdef __cplusplus
}
#endif

#endif
