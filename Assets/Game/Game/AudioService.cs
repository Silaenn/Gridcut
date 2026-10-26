using UnityEngine;

namespace GridCut.Game
{
    /// <summary>
    /// Procedural music + SFX (tanpa aset audio). Satu AudioSource untuk SFX dan
    /// satu loop ambient untuk musik. Volume musik akhir = slider × 0,5 (spec 11);
    /// slider SFX langsung memengaruhi PlayOneShot. Mute SoundOn mematikan keduanya.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        AudioClip _place, _invalid, _win, _tap, _unlock, _drag, _musicClip;
        AudioSource _src, _music;
        static float _sfxPreview = -1f;

        const int Rate = 22050;
        const float MusicBase = 0.5f;   // pengali dasar musik (spec 11)

        public static void CreateIfMissing()
        {
            if (Instance == null)
            {
                var go = new GameObject("AudioService");
                Instance = go.AddComponent<AudioService>();
                Instance.GenerateClips();
                Instance.GenerateMusic();
                _sfxPreview = SaveStore.SfxVolume;
                RefreshMusicVolume();
            }
        }

        void GenerateClips()
        {
            _tap = Tone(new (float f, float s, float a)[] { (660f, 0.05f, 0.5f) });
            _place = Tone(new (float f, float s, float a)[] { (420f, 0.07f, 0.6f), (640f, 0.09f, 0.5f) });
            _invalid = Tone(new (float f, float s, float a)[] { (150f, 0.14f, 0.7f), (110f, 0.14f, 0.6f) });
            _unlock = Tone(new (float f, float s, float a)[] { (523f, 0.1f, 0.5f), (659f, 0.1f, 0.5f), (784f, 0.14f, 0.5f) });
            _win = Tone(new (float f, float s, float a)[] { (523f, 0.12f, 0.55f), (659f, 0.12f, 0.55f), (784f, 0.12f, 0.55f), (1047f, 0.24f, 0.55f) });
            _drag = Tone(new (float f, float s, float a)[] { (880f, 0.03f, 0.3f) });
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
        }

        /// <summary>Loop ambient 8 dtk; semua frekuensi kelipatan 1/8 dtk agar mulus.</summary>
        void GenerateMusic()
        {
            const int seconds = 8;
            int total = Rate * seconds;
            float[] data = new float[total];
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
            _musicClip = AudioClip.Create("music", total, 1, Rate, false);
            _musicClip.SetData(data, 0);
            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = _musicClip;
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = SaveStore.MusicVolume * MusicBase;
            _music.Play();
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
            var clip = AudioClip.Create("sfx", total, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(string key)
        {
            if (!SaveStore.SoundOn || Instance == null) return;
            var clip = key switch
            {
                "place" => Instance._place,
                "invalid" => Instance._invalid,
                "win" => Instance._win,
                "unlock" => Instance._unlock,
                "drag" => Instance._drag,
                _ => Instance._tap,
            };
            float volume = _sfxPreview >= 0f ? _sfxPreview : SaveStore.SfxVolume;
            Instance._src.pitch = 1f;
            Instance._src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Musik langsung mengikuti slider selama digeser (belum disimpan).</summary>
        public static void SetMusicPreview(float v)
        {
            if (Instance == null || Instance._music == null) return;
            Instance._music.volume = Mathf.Clamp01(v) * MusicBase
                * (SaveStore.SoundOn ? 1f : 0f);
        }

        /// <summary>SFX langsung mengikuti slider selama digeser (belum disimpan).</summary>
        public static void SetSfxPreview(float v) => _sfxPreview = Mathf.Clamp01(v);

        /// <summary>Terapkan volume tersimpan + state mute (dipanggil saat boot/toggle).</summary>
        public static void RefreshMusicVolume()
        {
            if (Instance == null || Instance._music == null) return;
            Instance._music.volume = SaveStore.MusicVolume * MusicBase
                * (SaveStore.SoundOn ? 1f : 0f);
        }
    }
}