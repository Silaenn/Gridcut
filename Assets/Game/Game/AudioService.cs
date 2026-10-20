using UnityEngine;

namespace GridCut.Game
{
    /// <summary>
    /// Procedural sound effects (no audio assets in the build): tiny synthesized
    /// envelopes generated once at runtime. One shared AudioSource; mute via
    /// SaveStore.SoundOn.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        AudioClip _place, _invalid, _win, _tap, _unlock;
        AudioSource _src;

        const int Rate = 22050;

        public static void CreateIfMissing()
        {
            if (Instance == null)
            {
                var go = new GameObject("AudioService");
                Instance = go.AddComponent<AudioService>();
                Instance.GenerateClips();
            }
        }

        void GenerateClips()
        {
            _tap = Tone(new (float f, float s, float a)[] { (660f, 0.05f, 0.5f) });
            _place = Tone(new (float f, float s, float a)[] { (420f, 0.07f, 0.6f), (640f, 0.09f, 0.5f) });
            _invalid = Tone(new (float f, float s, float a)[] { (150f, 0.14f, 0.7f), (110f, 0.14f, 0.6f) });
            _unlock = Tone(new (float f, float s, float a)[] { (523f, 0.1f, 0.5f), (659f, 0.1f, 0.5f), (784f, 0.14f, 0.5f) });
            _win = Tone(new (float f, float s, float a)[] { (523f, 0.12f, 0.55f), (659f, 0.12f, 0.55f), (784f, 0.12f, 0.55f), (1047f, 0.24f, 0.55f) });
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
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
                _ => Instance._tap,
            };
            Instance._src.pitch = 1f;
            Instance._src.PlayOneShot(clip);
        }
    }
}
