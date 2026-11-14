#include "jp_stretch.h"

#include <signalsmith-stretch/signalsmith-stretch.h>

#include <new>
#include <vector>

namespace {

// Zugriff auf einen interleaved Puffer in der Form buffer[channel][frame],
// wie ihn signalsmith-stretch erwartet. Kopiert nichts.
template <class Sample>
struct Interleaved {
    Sample *data;
    int channels;

    struct Channel {
        Sample *data;
        int channels;

        Sample &operator[](int frame) const { return data[static_cast<long>(frame) * channels]; }
    };

    Channel operator[](int channel) const { return {data + channel, channels}; }
};

using Stretcher = signalsmith::stretch::SignalsmithStretch<float>;

} // namespace

struct jp_stretch {
    Stretcher stretcher;
    int channels = 0;
};

extern "C" {

int jp_stretch_api_version(void) { return JP_STRETCH_API_VERSION; }

jp_stretch *jp_stretch_create(int channels, float sample_rate, int options) {
    if (channels <= 0 || sample_rate <= 0) {
        return nullptr;
    }

    try {
        auto *stretch = new jp_stretch();
        stretch->channels = channels;
        const bool split = (options & JP_STRETCH_SPLIT) != 0;
        if ((options & JP_STRETCH_CHEAPER) != 0) {
            stretch->stretcher.presetCheaper(channels, sample_rate, split);
        } else {
            stretch->stretcher.presetDefault(channels, sample_rate, split);
        }

        // Interne Hilfspuffer einmal auf volle Größe bringen, damit seek und process
        // später im Audio-Callback keinen Speicher mehr anlegen.
        const int length = stretch->stretcher.seekLength();
        std::vector<float> silence(static_cast<size_t>(length) * channels);
        Interleaved<float> buffer{silence.data(), channels};
        stretch->stretcher.seek(buffer, length, 1.0);
        stretch->stretcher.process(buffer, length, buffer, length);
        stretch->stretcher.reset();
        return stretch;
    } catch (...) {
        return nullptr;
    }
}

void jp_stretch_destroy(jp_stretch *stretch) { delete stretch; }

void jp_stretch_reset(jp_stretch *stretch) { stretch->stretcher.reset(); }

int jp_stretch_input_latency(const jp_stretch *stretch) { return stretch->stretcher.inputLatency(); }

int jp_stretch_output_latency(const jp_stretch *stretch) { return stretch->stretcher.outputLatency(); }

int jp_stretch_seek_length(const jp_stretch *stretch) { return stretch->stretcher.seekLength(); }

void jp_stretch_seek(jp_stretch *stretch, const float *input, int frames, double rate) {
    Interleaved<const float> in{input, stretch->channels};
    stretch->stretcher.seek(in, frames, rate);
}

void jp_stretch_process(jp_stretch *stretch, const float *input, int input_frames, float *output,
                        int output_frames) {
    Interleaved<const float> in{input, stretch->channels};
    Interleaved<float> out{output, stretch->channels};
    stretch->stretcher.process(in, input_frames, out, output_frames);
}

void jp_stretch_flush(jp_stretch *stretch, float *output, int frames, float rate) {
    Interleaved<float> out{output, stretch->channels};
    stretch->stretcher.flush(out, frames, rate);
}

int jp_stretch_exact(jp_stretch *stretch, const float *input, int input_frames, float *output,
                     int output_frames) {
    try {
        Interleaved<const float> in{input, stretch->channels};
        Interleaved<float> out{output, stretch->channels};
        return stretch->stretcher.exact(in, input_frames, out, output_frames) ? 1 : 0;
    } catch (...) {
        return 0;
    }
}

} // extern "C"
