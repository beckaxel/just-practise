# Test files for AacDecoderTests

Made with an FFmpeg 9.1 build that has the `aac` and `alac` encoders (not part of the app), from 16-bit WAV files:

| File | Content | Command |
|---|---|---|
| `pattern-alac.m4a` | ALAC, stereo, 44.1 kHz, 20 000 frames; frame n, channel c has the value (n % 1000 + 100 c) / 32768, like `TestWav` | `ffmpeg -i pattern.wav -c:a alac pattern-alac.m4a` |
| `sine-440-aac.m4a` | AAC-LC 96 kbit/s, stereo, 48 kHz, 1 s sine 440 Hz at half scale | `ffmpeg -i sine.wav -c:a aac -b:a 96k sine-440-aac.m4a` |
| `sine-440-adts.aac` | the same as raw AAC (ADTS) | `ffmpeg -i sine.wav -c:a aac -b:a 96k sine-440-adts.aac` |
