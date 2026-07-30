using UnityEngine;

namespace FormosaExpress.Audio
{
    /// <summary>
    /// A tiny offline synthesiser. The game has no audio files, so every engine note, horn,
    /// coin chime and the music loop is generated into an <see cref="AudioClip"/> at boot.
    /// </summary>
    public static class AudioSynth
    {
        public const int SampleRate = 44100;

        // ------------------------------------------------------------------ helpers

        static float Sine(float phase) => Mathf.Sin(phase * Mathf.PI * 2f);

        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));

        static float Square(float phase, float duty = 0.5f) => Mathf.Repeat(phase, 1f) < duty ? 1f : -1f;

        static float Triangle(float phase)
        {
            float t = Mathf.Repeat(phase, 1f);
            return t < 0.5f ? (t * 4f - 1f) : (3f - t * 4f);
        }

        /// <summary>Deterministic white noise, so a given clip always sounds identical.</summary>
        sealed class NoiseSource
        {
            uint _state;
            public NoiseSource(int seed) { _state = (uint)(seed == 0 ? 12345 : seed); }

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / 8388608f - 1f;
            }
        }

        /// <summary>One-pole low-pass. <paramref name="cutoff01"/> is 0 (dark) .. 1 (open).</summary>
        sealed class LowPass
        {
            float _z;
            public float Process(float input, float cutoff01)
            {
                float a = Mathf.Clamp(cutoff01, 0.0005f, 1f);
                _z += (input - _z) * a;
                return _z;
            }
        }

        static void Normalise(float[] data, float peak = 0.92f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 0.0001f) return;

            float scale = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }

        /// <summary>Cross-fades the tail into the head so a looping clip has no click.</summary>
        static void SmoothLoop(float[] data, int fadeSamples)
        {
            fadeSamples = Mathf.Min(fadeSamples, data.Length / 4);
            for (int i = 0; i < fadeSamples; i++)
            {
                float t = i / (float)fadeSamples;
                int tail = data.Length - fadeSamples + i;
                float blended = data[i] * t + data[tail] * (1f - t);
                data[i] = blended;
                data[tail] = blended;
            }
        }

        static AudioClip ToClip(string name, float[] data, bool loop)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void ApplyDecay(float[] data, float power)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                data[i] *= Mathf.Pow(1f - t, power);
            }
        }

        // ------------------------------------------------------------------ engine

        /// <summary>
        /// A looping four-stroke-ish buzz. <paramref name="fundamental"/> must divide evenly into
        /// one second so the loop is seamless. <paramref name="brightness"/> adds upper harmonics.
        /// </summary>
        public static AudioClip Engine(string name, float fundamental, float brightness, float roughness, int seed)
        {
            int cycles = Mathf.Max(1, Mathf.RoundToInt(fundamental));
            int length = Mathf.RoundToInt(SampleRate * cycles / fundamental);
            var data = new float[length];
            var noise = new NoiseSource(seed);
            var filter = new LowPass();

            // Harmonic series with a slightly uneven amplitude profile: even harmonics a touch
            // quieter than odd ones, which is what gives a small engine its rasp.
            const int harmonics = 14;
            var amplitudes = new float[harmonics];
            for (int h = 0; h < harmonics; h++)
            {
                float n = h + 1;
                float odd = (h % 2 == 0) ? 1f : 0.62f;
                amplitudes[h] = odd * Mathf.Pow(1f / n, Mathf.Lerp(1.55f, 0.92f, brightness));
            }

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;

                for (int h = 0; h < harmonics; h++)
                {
                    float freq = fundamental * (h + 1);
                    if (freq > SampleRate * 0.45f) break;
                    sample += amplitudes[h] * Saw(freq * t);
                }

                // Combustion irregularity: a slow amplitude wobble at half the firing rate.
                float wobble = 0.82f + 0.18f * Sine(fundamental * 0.5f * t);
                sample *= wobble;

                // Intake/exhaust hiss.
                sample += filter.Process(noise.Next(), 0.28f) * roughness;

                data[i] = sample;
            }

            SmoothLoop(data, 256);
            Normalise(data, 0.85f);
            return ToClip(name, data, true);
        }

        // ------------------------------------------------------------------ one-shots

        public static AudioClip Horn(string name, float baseFreq, float duration)
        {
            int length = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                // Two detuned square voices: the classic scooter parp.
                float voice = Square(baseFreq * t, 0.48f) * 0.6f + Square(baseFreq * 1.5f * t, 0.42f) * 0.4f;
                voice += Saw(baseFreq * 2.01f * t) * 0.18f;

                float attack = Mathf.Clamp01(t / 0.02f);
                float release = Mathf.Clamp01((duration - t) / 0.06f);
                data[i] = voice * attack * release;
            }

            Normalise(data, 0.7f);
            return ToClip(name, data, false);
        }

        public static AudioClip Crash(string name, float duration, int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[length];
            var noise = new NoiseSource(seed);
            var bright = new LowPass();
            var body = new LowPass();

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float n = noise.Next();

                // Bright shatter that closes down fast, over a low thump.
                float openness = Mathf.Lerp(0.85f, 0.05f, Mathf.Clamp01(t / (duration * 0.55f)));
                float shatter = bright.Process(n, openness);
                float thump = body.Process(n, 0.012f) * 2.6f;
                float sweep = Sine(Mathf.Lerp(160f, 42f, Mathf.Clamp01(t / 0.18f)) * t) * Mathf.Exp(-t * 22f);

                data[i] = shatter * 0.7f + thump * 0.6f + sweep * 0.8f;
            }

            ApplyDecay(data, 2.4f);
            Normalise(data, 0.95f);
            return ToClip(name, data, false);
        }

        public static AudioClip Whoosh(string name, float duration, int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[length];
            var noise = new NoiseSource(seed);
            var filter = new LowPass();

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / duration;

                // A band that sweeps up then down: air moving past your helmet.
                float cutoff = Mathf.Sin(u * Mathf.PI) * 0.55f + 0.05f;
                float n = filter.Process(noise.Next(), cutoff);
                float envelope = Mathf.Sin(u * Mathf.PI);
                data[i] = n * envelope * 1.6f;
            }

            Normalise(data, 0.55f);
            return ToClip(name, data, false);
        }

        /// <summary>A short arpeggio, used for coins, combo steps and UI confirmation.</summary>
        public static AudioClip Chime(string name, float[] semitones, float noteLength, float baseFreq, float bell)
        {
            int noteSamples = Mathf.RoundToInt(SampleRate * noteLength);
            int length = noteSamples * semitones.Length + Mathf.RoundToInt(SampleRate * 0.25f);
            var data = new float[length];

            for (int n = 0; n < semitones.Length; n++)
            {
                float freq = baseFreq * Mathf.Pow(2f, semitones[n] / 12f);
                int start = n * noteSamples;

                for (int i = 0; i < length - start; i++)
                {
                    float t = i / (float)SampleRate;
                    float envelope = Mathf.Exp(-t * Mathf.Lerp(14f, 5f, bell));
                    if (envelope < 0.0008f) break;

                    float voice = Sine(freq * t) * 0.7f
                                  + Sine(freq * 2f * t) * 0.22f * bell
                                  + Triangle(freq * 3.02f * t) * 0.10f * bell;

                    data[start + i] += voice * envelope;
                }
            }

            Normalise(data, 0.75f);
            return ToClip(name, data, false);
        }

        public static AudioClip Blip(string name, float freq, float duration, float sweep)
        {
            int length = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float f = freq * Mathf.Pow(2f, sweep * (t / duration));
                float envelope = Mathf.Exp(-t * 18f);
                data[i] = (Square(f * t, 0.5f) * 0.35f + Sine(f * t) * 0.65f) * envelope;
            }

            Normalise(data, 0.55f);
            return ToClip(name, data, false);
        }

        public static AudioClip Impactless(string name, float duration, int seed)
        {
            // Soft thud for pedestrian bumps: comedic, not violent.
            int length = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[length];
            var noise = new NoiseSource(seed);
            var filter = new LowPass();

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float thud = filter.Process(noise.Next(), 0.05f) * 2.2f;
                float boop = Sine(Mathf.Lerp(320f, 120f, Mathf.Clamp01(t / 0.14f)) * t);
                data[i] = (thud * 0.5f + boop * 0.6f) * Mathf.Exp(-t * 14f);
            }

            Normalise(data, 0.65f);
            return ToClip(name, data, false);
        }

        /// <summary>A seamless filtered-noise bed, used for wind at speed.</summary>
        public static AudioClip NoiseLoop(string name, float cutoff01, float duration, int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[length];
            var noise = new NoiseSource(seed);
            var filter = new LowPass();

            for (int i = 0; i < length; i++)
                data[i] = filter.Process(noise.Next(), cutoff01);

            SmoothLoop(data, 2048);
            Normalise(data, 0.8f);
            return ToClip(name, data, true);
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// A looping city-pop-flavoured backing track: walking bass, electric-piano chords, a
        /// bright arpeggio and a simple kit. Four bars, seamless.
        /// </summary>
        public static AudioClip MusicLoop(string name, float bpm, int seed, float energy)
        {
            float beat = 60f / bpm;
            int beats = 16;
            int length = Mathf.RoundToInt(SampleRate * beat * beats);
            var data = new float[length];
            var noise = new NoiseSource(seed);

            // Fmaj7 - G7 - Em7 - Am7, one bar each. Roots as MIDI-ish semitone offsets from A2.
            int[] roots = { 8, 10, 7, 0 };
            int[][] chords =
            {
                new[] { 8, 12, 15, 19 },   // F A C E
                new[] { 10, 14, 17, 20 },  // G B D F
                new[] { 7, 10, 14, 17 },   // E G B D
                new[] { 0, 3, 7, 10 }      // A C E G
            };

            const float a2 = 110f;
            float Freq(int semitone, int octave) => a2 * Mathf.Pow(2f, semitone / 12f + octave);

            void AddVoice(float startSeconds, float durationSeconds, float freq, float amplitude,
                System.Func<float, float> osc, float decay)
            {
                int start = Mathf.RoundToInt(startSeconds * SampleRate);
                int count = Mathf.RoundToInt(durationSeconds * SampleRate);

                for (int i = 0; i < count; i++)
                {
                    int index = start + i;
                    if (index < 0) continue;

                    float t = i / (float)SampleRate;
                    float attack = Mathf.Clamp01(t / 0.008f);
                    float envelope = attack * Mathf.Exp(-t * decay);
                    if (envelope < 0.0005f) break;

                    // Wrap so notes that overhang the loop point land back at the start.
                    data[index % length] += osc(freq * t) * amplitude * envelope;
                }
            }

            for (int bar = 0; bar < 4; bar++)
            {
                float barStart = bar * beat * 4f;
                int[] chord = chords[bar];

                // Walking bass: root on 1 and 3, a passing note on 4.
                AddVoice(barStart, beat * 1.2f, Freq(roots[bar], 0), 0.42f, Saw, 2.6f);
                AddVoice(barStart + beat * 2f, beat * 0.9f, Freq(roots[bar], 0), 0.36f, Saw, 3.0f);
                AddVoice(barStart + beat * 3.5f, beat * 0.45f, Freq(roots[bar] + 5, 0), 0.30f, Saw, 4.2f);

                // Electric-piano stabs on the off-beats.
                for (int stab = 0; stab < 2; stab++)
                {
                    float when = barStart + beat * (1.5f + stab * 2f);
                    foreach (int note in chord)
                        AddVoice(when, beat * 0.8f, Freq(note, 1), 0.11f, Triangle, 4.4f);
                }

                // Arpeggio: eight notes climbing and falling through the chord.
                for (int step = 0; step < 8; step++)
                {
                    int note = chord[step < 4 ? step : 6 - step + 1];
                    float when = barStart + beat * 0.5f * step;
                    AddVoice(when, beat * 0.45f, Freq(note, 2), 0.075f * energy, Sine, 7.5f);
                }
            }

            // Drums.
            for (int b = 0; b < beats; b++)
            {
                float when = b * beat;

                // Kick on 1 and 3 (plus a pickup before the loop point).
                if (b % 4 == 0 || b % 8 == 6)
                {
                    int start = Mathf.RoundToInt(when * SampleRate);
                    for (int i = 0; i < SampleRate / 6; i++)
                    {
                        float t = i / (float)SampleRate;
                        float f = Mathf.Lerp(120f, 44f, Mathf.Clamp01(t / 0.05f));
                        data[(start + i) % length] += Sine(f * t) * 0.55f * Mathf.Exp(-t * 17f);
                    }
                }

                // Snare on 2 and 4.
                if (b % 4 == 2)
                {
                    int start = Mathf.RoundToInt(when * SampleRate);
                    var filter = new LowPass();
                    for (int i = 0; i < SampleRate / 7; i++)
                    {
                        float t = i / (float)SampleRate;
                        float n = filter.Process(noise.Next(), 0.5f);
                        data[(start + i) % length] += (n * 0.42f + Sine(190f * t) * 0.16f) * Mathf.Exp(-t * 26f);
                    }
                }

                // Hats on every eighth.
                for (int h = 0; h < 2; h++)
                {
                    int start = Mathf.RoundToInt((when + h * beat * 0.5f) * SampleRate);
                    var filter = new LowPass();
                    float gain = h == 0 ? 0.16f : 0.10f;
                    for (int i = 0; i < SampleRate / 22; i++)
                    {
                        float t = i / (float)SampleRate;
                        float n = filter.Process(noise.Next(), 0.92f);
                        data[(start + i) % length] += n * gain * Mathf.Exp(-t * 90f);
                    }
                }
            }

            SmoothLoop(data, 512);
            Normalise(data, 0.62f);
            return ToClip(name, data, true);
        }
    }
}
