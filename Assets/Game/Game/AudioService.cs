using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GridCut.Game
{
    /// <summary>
    /// Audio director: satu BGM loop per layar + SFX pendek dari Resources/Audio.
    /// Semua aset bersifat CC0 (lihat Resources/Audio/CREDITS.txt). Kalau aset
    /// belum ada, layanan tetap jalan dengan clip prosedural lama sebagai fallback.
    /// Volume musik akhir = slider × 0,5 (spec 11); slider SFX langsung ke PlayOneShot.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        const int Rate = 22050;
        const float MusicBase = 0.5f;
        const float MusicFade = 0.35f;

        AudioSource _src, _music;
        AudioClip _musicFallback;
        AudioClip _bgmHome, _bgmGame;
        string _musicId;
        Coroutine _musicCo, _duckCo;
        float _duck = 1f;
        int _tapIndex, _placeIndex;

        readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, float> _gain = new Dictionary<string, float>();

        // Fallback prosedural (dipakai hanya bila file Resources tidak ditemukan).
        AudioClip _tap, _place, _invalid, _win, _unlock, _drag, _step;

        static float _sfxPreview = -1f;

        public static void CreateIfMissing()
        {
            if (Instance != null) return;
            var go = new GameObject("AudioService");
            Instance = go.AddComponent<AudioService>();
            Instance.Setup();
        }

        void Setup()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;

            GenerateFallbackClips();
            LoadResources();
            GenerateFallbackMusic();
            _sfxPreview = SaveStore.SfxVolume;
            RefreshMusicVolume();
        }

        void LoadResources()
        {
            _bgmHome = Resources.Load<AudioClip>("Audio/BGM/bgm_home");
            _bgmGame = Resources.Load<AudioClip>("Audio/BGM/bgm_game");

            Add("tap", "sfx_tap_1");   // semua tombol memakai satu klik yang sama
            Add("place", "sfx_place_1");   // satu suara place, tanpa variasi
            Add("drag", "sfx_drag");
            Add("remove", "sfx_remove");
            Add("invalid", "sfx_invalid");
            Add("hint", "sfx_hint");
            Add("win", "sfx_win");
            Add("star1", "sfx_star_1");
            Add("star2", "sfx_star_2");
            Add("star3", "sfx_star_3");
            Add("newbest", "sfx_newbest");
            Add("theme_unlock", "sfx_theme_unlock");
            Add("level_unlock", "sfx_level_unlock");
            Add("step", "sfx_step");

            Gain("tap", 0.50f);
            Gain("drag", 0.35f);
            Gain("place", 0.90f);
            Gain("remove", 0.65f);
            Gain("invalid", 0.70f);
            Gain("hint", 0.65f);
            Gain("win", 0.90f);
            Gain("star1", 0.55f);
            Gain("star2", 0.55f);
            Gain("star3", 0.55f);
            Gain("newbest", 0.75f);
            Gain("theme_unlock", 0.80f);
            Gain("level_unlock", 0.65f);
            Gain("step", 0.60f);
        }

        void Add(string key, params string[] names)
        {
            var list = new List<AudioClip>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var c = Resources.Load<AudioClip>("Audio/SFX/" + names[i]);
                if (c != null) list.Add(c);
            }
            if (list.Count > 0) _clips[key] = list.ToArray();
        }

        void Gain(string key, float value) => _gain[key] = value;

        // Semua tombol/UI memakai satu klik; hanya event papan/reward yang beda.
        static bool IsUiAlias(string key) =>
            key == "popup_open" || key == "popup_close" ||
            key == "toggle_on" || key == "toggle_off" ||
            key == "theme_select" || key == "page_flip" ||
            key == "reset_confirm" || key == "reset_done";

        AudioClip ClipFor(string key)
        {
            if (IsUiAlias(key)) key = "tap";
            if (_clips.TryGetValue(key, out var arr) && arr.Length > 0)
            {
                if (arr.Length == 1) return arr[0];
                if (key == "tap") return arr[_tapIndex++ % arr.Length];
                if (key == "place") return arr[_placeIndex++ % arr.Length];
                return arr[Random.Range(0, arr.Length)];
            }
            return key switch
            {
                "place" => _place,
                "invalid" => _invalid,
                "win" => _win,
                "unlock" => _unlock,
                "drag" => _drag,
                "step" => _step,
                _ => _tap,
            };
        }

        public static void Play(string key)
        {
            if (!SaveStore.SoundOn || Instance == null) return;
            string lookup = IsUiAlias(key) ? "tap" : key;
            var clip = Instance.ClipFor(lookup);
            if (clip == null) return;

            float volume = _sfxPreview >= 0f ? _sfxPreview : SaveStore.SfxVolume;
            float gain = Instance._gain.TryGetValue(lookup, out var g) ? g : 1f;
            Instance._src.pitch = 1f;
            Instance._src.PlayOneShot(clip, Mathf.Clamp01(volume * gain));

            if (key == "win") Instance.DuckMusic(4.0f);
            else if (key == "theme_unlock") Instance.DuckMusic(1.2f);
        }

        // ------------------------------- music -------------------------------

        public static void PlayMusic(string id)
        {
            if (Instance != null) Instance.PlayMusicInternal(id);
        }

        void PlayMusicInternal(string id)
        {
            var clip = id == "game" ? _bgmGame : _bgmHome;
            if (clip == null) clip = _bgmHome != null ? _bgmHome : _bgmGame;
            if (clip == null)
            {
                if (_musicFallback != null && !_music.isPlaying) PlayFallbackMusic();
                return;
            }
            if (_musicId == id && _music.clip == clip && _music.isPlaying) return;

            _musicId = id;
            if (_musicCo != null) StopCoroutine(_musicCo);
            _musicCo = StartCoroutine(SwapMusic(clip));
        }

        IEnumerator SwapMusic(AudioClip clip)
        {
            if (_music.isPlaying)
            {
                float from = _music.volume;
                for (float t = 0f; t < MusicFade; t += Time.unscaledDeltaTime)
                {
                    _music.volume = Mathf.Lerp(from, 0f, t / MusicFade);
                    yield return null;
                }
            }
            _music.Stop();
            _music.clip = clip;
            _music.Play();

            float target = MusicTarget();
            for (float t = 0f; t < MusicFade; t += Time.unscaledDeltaTime)
            {
                _music.volume = target * Mathf.Clamp01(t / MusicFade);
                yield return null;
            }
            _music.volume = target;
            _musicCo = null;
        }

        void DuckMusic(float seconds)
        {
            if (_music == null || !_music.isPlaying) return;
            if (_duckCo != null) StopCoroutine(_duckCo);
            _duckCo = StartCoroutine(DuckCo(seconds));
        }

        IEnumerator DuckCo(float seconds)
        {
            _duck = 0.35f;
            ApplyMusicVolume();
            yield return new WaitForSecondsRealtime(seconds);
            _duck = 1f;
            ApplyMusicVolume();
            _duckCo = null;
        }

        float MusicTarget() =>
            SaveStore.MusicVolume * MusicBase * (SaveStore.SoundOn ? 1f : 0f) * _duck;

        void ApplyMusicVolume()
        {
            if (_music != null) _music.volume = MusicTarget();
        }

        /// <summary>Musik langsung mengikuti slider selama digeser (belum disimpan).</summary>
        public static void SetMusicPreview(float v)
        {
            if (Instance == null || Instance._music == null) return;
            Instance._music.volume = Mathf.Clamp01(v) * MusicBase
                * (SaveStore.SoundOn ? 1f : 0f) * Instance._duck;
        }

        /// <summary>SFX langsung mengikuti slider selama digeser (belum disimpan).</summary>
        public static void SetSfxPreview(float v) => _sfxPreview = Mathf.Clamp01(v);

        /// <summary>Terapkan volume tersimpan + state mute (dipanggil saat boot/toggle).</summary>
        public static void RefreshMusicVolume()
        {
            if (Instance == null) return;
            Instance.ApplyMusicVolume();
        }

        // ----------------------------- fallbacks -----------------------------

        void GenerateFallbackClips()
        {
            _tap = Tone(new (float f, float s, float a)[] { (660f, 0.05f, 0.5f) });
            _place = Tone(new (float f, float s, float a)[] { (420f, 0.07f, 0.6f), (640f, 0.09f, 0.5f) });
            _invalid = Tone(new (float f, float s, float a)[] { (150f, 0.14f, 0.7f), (110f, 0.14f, 0.6f) });
            _unlock = Tone(new (float f, float s, float a)[] { (523f, 0.1f, 0.5f), (659f, 0.1f, 0.5f), (784f, 0.14f, 0.5f) });
            _win = Tone(new (float f, float s, float a)[] { (523f, 0.12f, 0.55f), (659f, 0.12f, 0.55f), (784f, 0.12f, 0.55f), (1047f, 0.24f, 0.55f) });
            _drag = Tone(new (float f, float s, float a)[] { (880f, 0.03f, 0.3f) });
            _step = Tone(new (float f, float s, float a)[] { (659f, 0.08f, 0.45f), (880f, 0.12f, 0.5f) });
        }

        /// <summary>Loop ambient 8 dtk sebagai fallback bila aset BGM tidak ada.</summary>
        void GenerateFallbackMusic()
        {
            const int seconds = 8;
            int total = Rate * seconds;
            var data = new float[total];
            float[] f = { 110f, 165f, 220f, 264f, 330f, 440f };
            float[] a = { 0.036f, 0.030f, 0.026f, 0.021f, 0.018f, 0.014f };
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)Rate;
                float lfo = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                float s = 0f;
                for (int n = 0; n < f.Length; n++)
                    s += Mathf.Sin(2f * Mathf.PI * f[n] * t) * a[n];
                data[i] = s * lfo;
            }
            _musicFallback = AudioClip.Create("music_fallback", total, 1, Rate, false);
            _musicFallback.SetData(data, 0);

            if (_bgmHome == null && _bgmGame == null) PlayFallbackMusic();
        }

        void PlayFallbackMusic()
        {
            if (_musicFallback == null || _music == null) return;
            _music.clip = _musicFallback;
            _music.loop = true;
            _music.Play();
            _musicId = "fallback";
            ApplyMusicVolume();
        }

        static AudioClip Tone((float f, float s, float a)[] notes)
        {
            int total = 0;
            foreach (var n in notes) total += Mathf.CeilToInt(n.s * Rate);
            var data = new float[total];
            int off = 0;
            foreach (var n in notes)
            {
                int len = Mathf.CeilToInt(n.s * Rate);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-6f * t / n.s);
                    data[off + i] = Mathf.Sin(2f * Mathf.PI * n.f * t) * env * n.a;
                }
                off += len;
            }
            var clip = AudioClip.Create("sfx_fallback", total, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
