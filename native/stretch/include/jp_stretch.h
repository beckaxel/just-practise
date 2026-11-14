/*
 * jp_stretch: schlanke C-Schnittstelle um signalsmith-stretch für P/Invoke.
 *
 * Alle Puffer sind interleaved (L R L R ...), Längen werden in Frames angegeben.
 * Nach jp_stretch_create legen seek, process und flush keinen Speicher an und
 * dürfen deshalb im Audio-Callback laufen. Ein Objekt gehört immer genau einem Thread.
 */
#ifndef JP_STRETCH_H
#define JP_STRETCH_H

#ifdef _WIN32
#  ifdef JP_STRETCH_BUILD
#    define JP_STRETCH_API __declspec(dllexport)
#  else
#    define JP_STRETCH_API __declspec(dllimport)
#  endif
#else
#  define JP_STRETCH_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Version der Schnittstelle. Steigt bei jeder inkompatiblen Änderung. */
#define JP_STRETCH_API_VERSION 1

/* Optionen für jp_stretch_create */
#define JP_STRETCH_CHEAPER 1 /* kleinere Blöcke, weniger Rechenlast, etwas schlechterer Klang */
#define JP_STRETCH_SPLIT 2   /* verteilt die Rechenlast gleichmäßiger, eine Blocklänge mehr Latenz */

typedef struct jp_stretch jp_stretch;

JP_STRETCH_API int jp_stretch_api_version(void);

/* Liefert NULL bei ungültigen Argumenten oder zu wenig Speicher. */
JP_STRETCH_API jp_stretch *jp_stretch_create(int channels, float sample_rate, int options);
JP_STRETCH_API void jp_stretch_destroy(jp_stretch *stretch);

JP_STRETCH_API void jp_stretch_reset(jp_stretch *stretch);
JP_STRETCH_API int jp_stretch_input_latency(const jp_stretch *stretch);
JP_STRETCH_API int jp_stretch_output_latency(const jp_stretch *stretch);

/* Empfohlene Anzahl Frames für jp_stretch_seek. */
JP_STRETCH_API int jp_stretch_seek_length(const jp_stretch *stretch);

/* Springt an eine neue Stelle: input enthält die Frames unmittelbar vor der neuen Position. */
JP_STRETCH_API void jp_stretch_seek(jp_stretch *stretch, const float *input, int frames, double rate);

/* Das Verhältnis input_frames / output_frames ist das Tempo (0.5 = halb so schnell). */
JP_STRETCH_API void jp_stretch_process(jp_stretch *stretch, const float *input, int input_frames,
                                       float *output, int output_frames);

/* Liest den Rest der Ausgabe am Ende eines Stücks, ohne weitere Eingabe. */
JP_STRETCH_API void jp_stretch_flush(jp_stretch *stretch, float *output, int frames, float rate);

/* Dehnt einen vollständigen Puffer exakt auf output_frames. Liefert 0, wenn input zu kurz ist.
 * Legt Speicher an, also nicht im Audio-Callback verwenden. */
JP_STRETCH_API int jp_stretch_exact(jp_stretch *stretch, const float *input, int input_frames,
                                    float *output, int output_frames);

#ifdef __cplusplus
}
#endif

#endif
