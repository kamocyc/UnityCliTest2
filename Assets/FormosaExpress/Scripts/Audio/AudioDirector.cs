using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.Audio
{
    /// <summary>
    /// Owns every sound in the game. The engine is a pair of looping synth voices whose pitch
    /// and mix follow the throttle; everything else is a one-shot fired through a small pool of
    /// sources so overlapping events never cut each other off.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const int OneShotVoices = 10;

        AudioSource _engineLow;
        AudioSource _engineHigh;
        AudioSource _wind;
        AudioSource _music;
        AudioSource[] _oneShots;
        int _oneShotCursor;

        AudioClip _horn;
        AudioClip _trafficHorn;
        AudioClip _crashHard;
        AudioClip _crashSoft;
        AudioClip _bump;
        AudioClip _whoosh;
        AudioClip _coin;
        AudioClip _pickup;
        AudioClip _delivery;
        AudioClip _expire;
        AudioClip _uiMove;
        AudioClip _uiConfirm;
        AudioClip _uiBack;
        AudioClip _levelUp;
        AudioClip[] _comboSteps;

        float _musicTargetVolume = 0.34f;
        float _engineTargetVolume;
        bool _muted;

        public void Initialise()
        {
            BuildClips();
            BuildSources();
        }

        void BuildClips()
        {
            _horn = AudioSynth.Horn("FE_Horn", 392f, 0.42f);
            _trafficHorn = AudioSynth.Horn("FE_TrafficHorn", 294f, 0.55f);
            _crashHard = AudioSynth.Crash("FE_CrashHard", 0.85f, 7717);
            _crashSoft = AudioSynth.Crash("FE_CrashSoft", 0.45f, 3313);
            _bump = AudioSynth.Impactless("FE_Bump", 0.32f, 991);
            _whoosh = AudioSynth.Whoosh("FE_Whoosh", 0.34f, 5087);

            _coin = AudioSynth.Chime("FE_Coin", new[] { 0f, 7f }, 0.055f, 880f, 0.7f);
            _pickup = AudioSynth.Chime("FE_Pickup", new[] { 0f, 4f, 7f }, 0.065f, 587f, 0.55f);
            _delivery = AudioSynth.Chime("FE_Delivery", new[] { 0f, 4f, 7f, 12f }, 0.085f, 523f, 0.85f);
            _levelUp = AudioSynth.Chime("FE_LevelUp", new[] { 0f, 5f, 9f, 12f, 16f }, 0.10f, 523f, 0.95f);
            _expire = AudioSynth.Chime("FE_Expire", new[] { 0f, -3f, -8f }, 0.13f, 392f, 0.35f);

            _uiMove = AudioSynth.Blip("FE_UiMove", 640f, 0.07f, 0.15f);
            _uiConfirm = AudioSynth.Blip("FE_UiConfirm", 520f, 0.14f, 1.0f);
            _uiBack = AudioSynth.Blip("FE_UiBack", 420f, 0.12f, -0.8f);

            // Eight rising steps, one per combo tier.
            _comboSteps = new AudioClip[Tuning.ComboMultipliers.Length];
            for (int i = 0; i < _comboSteps.Length; i++)
                _comboSteps[i] = AudioSynth.Chime($"FE_Combo{i}", new[] { 0f }, 0.06f,
                    523f * Mathf.Pow(2f, i * 2f / 12f), 0.6f);
        }

        void BuildSources()
        {
            AudioClip engineLowClip = AudioSynth.Engine("FE_EngineLow", 55f, 0.30f, 0.055f, 1234);
            AudioClip engineHighClip = AudioSynth.Engine("FE_EngineHigh", 88f, 0.85f, 0.10f, 4321);
            AudioClip windClip = AudioSynth.NoiseLoop("FE_Wind", 0.16f, 1.5f, 8765);
            AudioClip musicClip = AudioSynth.MusicLoop("FE_Music", 112f, 2024, 1f);

            _engineLow = MakeSource("EngineLow", engineLowClip, true, 0f);
            _engineHigh = MakeSource("EngineHigh", engineHighClip, true, 0f);
            _wind = MakeSource("Wind", windClip, true, 0f);
            _music = MakeSource("Music", musicClip, true, 0f);

            _oneShots = new AudioSource[OneShotVoices];
            for (int i = 0; i < OneShotVoices; i++)
            {
                _oneShots[i] = MakeSource($"OneShot{i}", null, false, 1f);
                _oneShots[i].playOnAwake = false;
            }
        }

        AudioSource MakeSource(string name, AudioClip clip, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            if (loop && clip != null) source.Play();
            return source;
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            var player = Services.Player;
            float dt = Time.unscaledDeltaTime;

            _music.volume = MathX.ExpSmooth(_music.volume, _muted ? 0f : _musicTargetVolume, 2.5f, dt);

            if (player == null || !player.ControlEnabled)
            {
                _engineTargetVolume = MathX.ExpSmooth(_engineTargetVolume, 0f, 4f, dt);
                _engineLow.volume = _engineTargetVolume * 0.35f;
                _engineHigh.volume = 0f;
                _wind.volume = MathX.ExpSmooth(_wind.volume, 0f, 4f, dt);
                return;
            }

            float rpm = player.Rpm01;
            float speed01 = player.Speed01;

            // Pitch tracks rpm across roughly two octaves; the low voice carries the body,
            // the high voice the whine, and they cross over around half throttle.
            float pitch = Mathf.Lerp(0.72f, 2.35f, rpm) * (player.IsBoosting ? 1.12f : 1f);
            _engineLow.pitch = pitch;
            _engineHigh.pitch = pitch * 1.005f;

            float master = _muted ? 0f : 1f;
            float lowMix = Mathf.Lerp(0.34f, 0.16f, rpm);
            float highMix = Mathf.Lerp(0.02f, 0.30f, Mathf.Pow(rpm, 1.4f)) * (player.IsBoosting ? 1.35f : 1f);

            _engineLow.volume = MathX.ExpSmooth(_engineLow.volume, lowMix * master, 12f, dt);
            _engineHigh.volume = MathX.ExpSmooth(_engineHigh.volume, highMix * master, 12f, dt);

            float windTarget = Mathf.Pow(speed01, 2.1f) * 0.30f * master;
            _wind.volume = MathX.ExpSmooth(_wind.volume, windTarget, 6f, dt);
            _wind.pitch = Mathf.Lerp(0.85f, 1.5f, speed01);

            InputRouter input = Services.Input;
            if (input != null && input.HornPressed) PlayHorn();
        }

        // ------------------------------------------------------------------ one-shots

        void Play(AudioClip clip, float volume, float pitch = 1f, Vector3? position = null)
        {
            if (clip == null || _muted) return;

            AudioSource source = _oneShots[_oneShotCursor];
            _oneShotCursor = (_oneShotCursor + 1) % OneShotVoices;

            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;

            if (position.HasValue)
            {
                source.spatialBlend = 0.85f;
                source.minDistance = 6f;
                source.maxDistance = 70f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.transform.position = position.Value;
            }
            else
            {
                source.spatialBlend = 0f;
                source.transform.localPosition = Vector3.zero;
            }

            source.Play();
        }

        public void PlayHorn() => Play(_horn, 0.42f, Random.Range(0.96f, 1.05f));
        public void PlayTrafficHorn(Vector3 position) => Play(_trafficHorn, 0.36f, Random.Range(0.88f, 1.12f), position);
        public void PlayWhoosh(float intensity) => Play(_whoosh, Mathf.Lerp(0.14f, 0.42f, intensity), Random.Range(0.9f, 1.15f));
        public void PlayCoin() => Play(_coin, 0.34f, Random.Range(0.98f, 1.06f));
        public void PlayPickup() => Play(_pickup, 0.42f);
        public void PlayDelivery() => Play(_delivery, 0.52f);
        public void PlayExpire() => Play(_expire, 0.40f);
        public void PlayLevelUp() => Play(_levelUp, 0.55f);
        public void PlayUiMove() => Play(_uiMove, 0.24f);
        public void PlayUiConfirm() => Play(_uiConfirm, 0.32f);
        public void PlayUiBack() => Play(_uiBack, 0.28f);

        public void PlayImpact(float severity, Vector3 position, bool pedestrian)
        {
            if (pedestrian)
            {
                Play(_bump, 0.45f, Random.Range(0.9f, 1.15f), position);
                return;
            }

            AudioClip clip = severity > 0.45f ? _crashHard : _crashSoft;
            Play(clip, Mathf.Lerp(0.30f, 0.85f, severity), Random.Range(0.92f, 1.08f), position);
        }

        public void PlayComboStep(int step)
        {
            if (_comboSteps == null || _comboSteps.Length == 0) return;
            Play(_comboSteps[Mathf.Clamp(step, 0, _comboSteps.Length - 1)], 0.30f);
        }

        // ------------------------------------------------------------------ mixing

        public void SetMusicVolume(float volume) => _musicTargetVolume = Mathf.Clamp01(volume);

        /// <summary>Ducks the music and lifts the engine when a shift is under way.</summary>
        public void SetRidingMix(bool riding)
        {
            _musicTargetVolume = riding ? 0.26f : 0.38f;
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            if (!muted) return;

            foreach (AudioSource source in _oneShots) source.Stop();
        }
    }
}
