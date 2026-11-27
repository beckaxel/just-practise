#include "jp_aac.h"

#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/avutil.h>

#include <stdio.h>
#include <stdlib.h>

#ifdef _MSC_VER
#  define JP_THREAD_LOCAL __declspec(thread)
#else
#  define JP_THREAD_LOCAL _Thread_local
#endif

struct jp_aac {
    AVFormatContext *format;
    AVCodecContext *decoder;
    AVPacket *packet;
    AVFrame *frame;
    int stream;
    int channels;
    int sample_rate;
    int64_t length;
    int have_frame;   /* frame enthält noch nicht gelesene Samples ab frame_pos */
    int frame_pos;
    int flushing;     /* Dateiende erreicht, Decoder wird geleert */
    int finished;
};

int jp_aac_api_version(void) { return JP_AAC_API_VERSION; }

const char *jp_aac_ffmpeg_version(void) {
    static JP_THREAD_LOCAL char text[128];
    unsigned format = avformat_version(), codec = avcodec_version(), util = avutil_version();
    snprintf(text, sizeof text, "FFmpeg %s (avformat %u.%u.%u, avcodec %u.%u.%u, avutil %u.%u.%u)", av_version_info(),
             format >> 16, (format >> 8) & 0xff, format & 0xff, codec >> 16, (codec >> 8) & 0xff, codec & 0xff,
             util >> 16, (util >> 8) & 0xff, util & 0xff);
    return text;
}

/* Holt den nächsten decodierten Frame nach aac->frame. Liefert 1, 0 am Ende oder einen Fehlercode. */
static int next_frame(jp_aac *aac) {
    while (!aac->finished) {
        int result = avcodec_receive_frame(aac->decoder, aac->frame);
        if (result == 0) {
            if (aac->frame->nb_samples <= 0) {
                av_frame_unref(aac->frame);
                continue;
            }
            aac->have_frame = 1;
            aac->frame_pos = 0;
            return 1;
        }
        if (result == AVERROR_EOF) {
            aac->finished = 1;
            break;
        }
        if (result != AVERROR(EAGAIN) && result != AVERROR_INVALIDDATA) {
            return JP_AAC_ERROR_DECODE;
        }
        if (result == AVERROR_INVALIDDATA) {
            continue; /* kaputtes Paket überspringen, wie die FFmpeg-Programme */
        }
        if (aac->flushing) {
            aac->finished = 1;
            break;
        }

        result = av_read_frame(aac->format, aac->packet);
        if (result < 0) {
            /* Ende der Datei (oder nicht mehr lesbar): Decoder leeren. */
            aac->flushing = 1;
            avcodec_send_packet(aac->decoder, NULL);
            continue;
        }
        if (aac->packet->stream_index == aac->stream) {
            result = avcodec_send_packet(aac->decoder, aac->packet);
            if (result < 0 && result != AVERROR_INVALIDDATA && result != AVERROR(EAGAIN)) {
                av_packet_unref(aac->packet);
                return JP_AAC_ERROR_DECODE;
            }
        }
        av_packet_unref(aac->packet);
    }
    return 0;
}

static float sample_at(const AVFrame *frame, int channel, int index) {
    const int channels = frame->ch_layout.nb_channels;
    switch (frame->format) {
    case AV_SAMPLE_FMT_FLTP: return ((const float *)frame->extended_data[channel])[index];
    case AV_SAMPLE_FMT_FLT: return ((const float *)frame->extended_data[0])[index * channels + channel];
    case AV_SAMPLE_FMT_S16P: return ((const int16_t *)frame->extended_data[channel])[index] * (1.0f / 32768.0f);
    case AV_SAMPLE_FMT_S16:
        return ((const int16_t *)frame->extended_data[0])[index * channels + channel] * (1.0f / 32768.0f);
    case AV_SAMPLE_FMT_S32P:
        return (float)(((const int32_t *)frame->extended_data[channel])[index] * (1.0 / 2147483648.0));
    case AV_SAMPLE_FMT_S32:
        return (float)(((const int32_t *)frame->extended_data[0])[index * channels + channel] * (1.0 / 2147483648.0));
    default: return 0.0f;
    }
}

/* Kopiert frames Frames ab frame_pos nach out. Hat der Frame andere Kanäle als die Datei (HE-AAC v2 kann von
 * mono auf stereo wechseln), wird der letzte vorhandene Kanal wiederholt bzw. der Rest weggelassen. */
static void copy_frames(jp_aac *aac, float *out, int frames) {
    const AVFrame *frame = aac->frame;
    const int source_channels = frame->ch_layout.nb_channels;
    if (frame->format == AV_SAMPLE_FMT_FLTP && source_channels == aac->channels) {
        for (int i = 0; i < frames; i++) {
            for (int c = 0; c < aac->channels; c++) {
                *out++ = ((const float *)frame->extended_data[c])[aac->frame_pos + i];
            }
        }
        return;
    }
    for (int i = 0; i < frames; i++) {
        for (int c = 0; c < aac->channels; c++) {
            const int source = c < source_channels ? c : source_channels - 1;
            *out++ = sample_at(frame, source, aac->frame_pos + i);
        }
    }
}

static int is_supported(enum AVSampleFormat format) {
    return format == AV_SAMPLE_FMT_FLTP || format == AV_SAMPLE_FMT_FLT || format == AV_SAMPLE_FMT_S16P ||
           format == AV_SAMPLE_FMT_S16 || format == AV_SAMPLE_FMT_S32P || format == AV_SAMPLE_FMT_S32;
}

int jp_aac_open(const char *path, jp_aac **out) {
    if (out == NULL) {
        return JP_AAC_ERROR_OPEN;
    }
    *out = NULL;
    if (path == NULL) {
        return JP_AAC_ERROR_OPEN;
    }
    av_log_set_level(AV_LOG_QUIET); /* Fehler meldet jp_aac über Rückgabewerte */

    jp_aac *aac = calloc(1, sizeof *aac);
    if (aac == NULL) {
        return JP_AAC_ERROR_MEMORY;
    }
    int error = 0;
    if (avformat_open_input(&aac->format, path, NULL, NULL) < 0) {
        error = JP_AAC_ERROR_OPEN;
        goto fail;
    }

    const AVCodec *codec = NULL;
    aac->stream = av_find_best_stream(aac->format, AVMEDIA_TYPE_AUDIO, -1, -1, &codec, 0);
    if (aac->stream < 0) {
        /* Rohes AAC (ADTS) nennt Rate und Kanäle erst nach dem Lesen einiger Pakete; M4A hat sie im Kopf. */
        if (avformat_find_stream_info(aac->format, NULL) >= 0) {
            aac->stream = av_find_best_stream(aac->format, AVMEDIA_TYPE_AUDIO, -1, -1, &codec, 0);
        }
    }
    if (aac->stream < 0 || codec == NULL) {
        error = JP_AAC_ERROR_NO_AUDIO;
        goto fail;
    }
    /* Alle anderen Spuren (z. B. Video in MP4) gar nicht erst lesen. */
    for (unsigned i = 0; i < aac->format->nb_streams; i++) {
        if ((int)i != aac->stream) {
            aac->format->streams[i]->discard = AVDISCARD_ALL;
        }
    }

    const AVStream *stream = aac->format->streams[aac->stream];
    aac->decoder = avcodec_alloc_context3(codec);
    aac->packet = av_packet_alloc();
    aac->frame = av_frame_alloc();
    if (aac->decoder == NULL || aac->packet == NULL || aac->frame == NULL) {
        error = JP_AAC_ERROR_MEMORY;
        goto fail;
    }
    if (avcodec_parameters_to_context(aac->decoder, stream->codecpar) < 0) {
        error = JP_AAC_ERROR_DECODER;
        goto fail;
    }
    aac->decoder->pkt_timebase = stream->time_base;
    aac->decoder->thread_count = 1;
    if (avcodec_open2(aac->decoder, codec, NULL) < 0) {
        error = JP_AAC_ERROR_DECODER;
        goto fail;
    }

    /* Den ersten Frame gleich decodieren: Erst er zeigt sicher Rate und Kanäle (bei HE-AAC verdoppelt SBR die Rate
     * gegenüber dem Kopf). */
    if (next_frame(aac) != 1) {
        error = JP_AAC_ERROR_DECODE;
        goto fail;
    }
    aac->channels = aac->frame->ch_layout.nb_channels;
    aac->sample_rate = aac->frame->sample_rate;
    if (aac->channels <= 0 || aac->sample_rate <= 0 || !is_supported(aac->frame->format)) {
        error = JP_AAC_ERROR_DECODER;
        goto fail;
    }

    aac->length = -1;
    if (stream->duration != AV_NOPTS_VALUE && stream->duration > 0) {
        aac->length = av_rescale_q(stream->duration, stream->time_base, (AVRational){1, aac->sample_rate});
    } else if (aac->format->duration != AV_NOPTS_VALUE && aac->format->duration > 0) {
        aac->length = av_rescale(aac->format->duration, aac->sample_rate, AV_TIME_BASE);
    }

    *out = aac;
    return 0;

fail:
    jp_aac_close(aac);
    return error;
}

void jp_aac_close(jp_aac *aac) {
    if (aac == NULL) {
        return;
    }
    av_frame_free(&aac->frame);
    av_packet_free(&aac->packet);
    avcodec_free_context(&aac->decoder);
    avformat_close_input(&aac->format);
    free(aac);
}

int jp_aac_channels(const jp_aac *aac) { return aac != NULL ? aac->channels : 0; }

int jp_aac_sample_rate(const jp_aac *aac) { return aac != NULL ? aac->sample_rate : 0; }

int64_t jp_aac_length(const jp_aac *aac) { return aac != NULL ? aac->length : -1; }

const char *jp_aac_codec(const jp_aac *aac) {
    return aac != NULL && aac->decoder != NULL && aac->decoder->codec != NULL ? aac->decoder->codec->name : "";
}

const char *jp_aac_profile(const jp_aac *aac) {
    if (aac == NULL || aac->decoder == NULL) {
        return "";
    }
    const char *name = avcodec_profile_name(aac->decoder->codec_id, aac->decoder->profile);
    return name != NULL ? name : "";
}

int jp_aac_read(jp_aac *aac, float *buffer, int max_frames) {
    if (aac == NULL || buffer == NULL || max_frames < 0) {
        return JP_AAC_ERROR_DECODE;
    }
    int written = 0;
    while (written < max_frames) {
        if (!aac->have_frame) {
            const int result = next_frame(aac);
            if (result == 0) {
                break;
            }
            if (result < 0) {
                return written > 0 ? written : result;
            }
        }
        int count = aac->frame->nb_samples - aac->frame_pos;
        if (count > max_frames - written) {
            count = max_frames - written;
        }
        copy_frames(aac, buffer + (int64_t)written * aac->channels, count);
        aac->frame_pos += count;
        written += count;
        if (aac->frame_pos >= aac->frame->nb_samples) {
            av_frame_unref(aac->frame);
            aac->have_frame = 0;
        }
    }
    return written;
}
