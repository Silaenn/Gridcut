using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridCut.Game
{
    /// <summary>
    /// Central theme state (spec bagian 4). UI elements register an updater per color
    /// role; when the theme changes every updater re-runs with the role's new color —
    /// smoothly over 300 ms (ColorOf returns the lerped value while transitioning).
    ///
    /// Roles (tabel bag. 4): Background=Latar, Dot=Titik, CellEmpty=Permukaan,
    /// GridLine=Tepi, Board=Papan, Primary=Utama, Accent=Aksen, Text=Teks,
    /// TextMuted=Teks redup, Locked=Ubin terkunci, Lock=Gembok, Rect=keping i.
    /// </summary>
    public static class ThemeApplier
    {
        public enum Role { Background, Dot, CellEmpty, GridLine, Board, Primary, Accent, Text, TextMuted, Locked, Lock, Rect,
                           ShadowPrimary, ShadowAccent, ShadowBoard, RectEdge }

        /// <summary>Durasi transisi warna antar tema (spec bag. 4: 300 ms, mulus).</summary>
        public const float TransitionSeconds = 0.3f;

        public static ThemeLibrary.Theme Current { get; private set; } = ThemeLibrary.Paper;

        /// <summary>True selama interpolasi 300 ms antar tema.</summary>
        public static bool IsTransitioning => _t < 1f;

        static readonly List<(Role role, int rectIndex, Action<Color> cb)> _subscribers =
            new List<(Role, int, Action<Color>)>();

        // warna yang sedang ditampilkan per kunci (role, rectIndex); selama transisi
        // nilainya = lerp dari tema lama ke tema baru, selebihnya = warna tema mutlak
        static readonly Dictionary<(Role, int), Color> _cur = new Dictionary<(Role, int), Color>();
        static Dictionary<(Role, int), Color> _from, _to;
        static float _t = 1f;                                  // 1 = netral (tidak transisi)
        static ThemeRunner _runner;

#if UNITY_EDITOR
        // Play-mode without domain reload keeps statics across sessions; stale
        // closures would then touch destroyed objects. Clear them on load.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            _subscribers.Clear();
            _cur.Clear(); _from = null; _to = null;
            _t = 1f; _runner = null;
            var old = GameObject.Find("[ThemeApplier]");
            if (old != null) UnityEngine.Object.Destroy(old);   // runner sisa play sebelumnya (tanpa domain reload)
        }
#endif

        public static Color ColorOf(Role role, int rectIndex = 0)
        {
            if (role == Role.Rect || role == Role.RectEdge)
                rectIndex = Mathf.Clamp(rectIndex, 0, Current.Rects.Length - 1);
            var key = (role, rectIndex);
            if (_cur.TryGetValue(key, out var c)) return c;
            c = BaseOf(role, rectIndex);
            _cur[key] = c;
            return c;
        }

        /// <summary>Warna peran langsung dari tema (tanpa buffer transisi).</summary>
        static Color BaseOf(Role role, int i) => role switch
        {
            Role.Background => Parse(Current.Background),
            Role.Dot        => Parse(Current.Dot),
            Role.CellEmpty  => Parse(Current.CellEmpty),
            Role.GridLine   => Parse(Current.GridLine),
            Role.Board      => Parse(Current.Board),
            Role.Primary    => Parse(Current.Primary),
            Role.Accent     => Parse(Current.Accent),
            Role.Text       => Parse(Current.Text),
            Role.TextMuted  => Parse(Current.TextMuted),
            Role.Locked     => Parse(Current.Locked),
            Role.Lock       => Parse(Current.Lock),
            Role.Rect       => Parse(Current.Rects[i]),
            Role.ShadowPrimary => Parse(Current.ShadowPrimary),
            Role.ShadowAccent  => Parse(Current.ShadowAccent),
            Role.ShadowBoard   => Parse(Current.ShadowBoard),
            Role.RectEdge      => Parse(Current.PieceEdges[i]),
            _ => Color.white,
        };

        public static void Subscribe(Role role, Action<Color> updater, bool applyImmediately,
            int rectIndex = 0)
        {
            // idempotent: screens re-subscribe on every enable; never stack duplicates,
            // and an updater moved to another role/index drops its stale entry
            for (int i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (_subscribers[i].cb != updater) continue;
                if (_subscribers[i].role == role && _subscribers[i].rectIndex == rectIndex)
                {
                    if (applyImmediately) updater(ColorOf(role, rectIndex));
                    return;
                }
                _subscribers.RemoveAt(i);
            }
            _subscribers.Add((role, rectIndex, updater));
            if (applyImmediately) updater(ColorOf(role, rectIndex));
        }

        public static void Unsubscribe(Action<Color> updater)
        {
            for (int i = _subscribers.Count - 1; i >= 0; i--)
                if (_subscribers[i].cb == updater) _subscribers.RemoveAt(i);
        }

        public static void Apply(int themeIndex)
        {
            SaveStore.SetTheme(themeIndex);
            Switch(ThemeLibrary.All[Mathf.Clamp(themeIndex, 0, ThemeLibrary.All.Length - 1)], false);
        }

        /// <summary>Applies the stored selection without re-saving (boot path).</summary>
        public static void ApplySaved()
        {
            Switch(ThemeLibrary.All[Mathf.Clamp(SaveStore.ThemeIndex, 0, ThemeLibrary.All.Length - 1)], true);
        }

        static void Switch(ThemeLibrary.Theme next, bool instant)
        {
            Current = next;
            EnsureKeys();
            if (instant)
            {
                // salin kunci dulu: indexer Dictionary menaikkan versi enumerator
                foreach (var k in new List<(Role, int)>(_cur.Keys))
                    _cur[k] = BaseOf(k.Item1, k.Item2);
                _t = 1f;
                Fire();
                return;
            }
            // transisi mulus 300 ms: ambil nilai saat ini sebagai awal, warna tema
            // baru sebagai tujuan, lerp diejalakan ThemeRunner tiap frame
            _from = new Dictionary<(Role, int), Color>(_cur);
            _to = new Dictionary<(Role, int), Color>(_cur.Count);
            foreach (var k in _cur.Keys) _to[k] = BaseOf(k.Item1, k.Item2);
            _t = 0f;
            EnsureRunner();
        }

        /// <summary>Pastikan semua kunci kanonik ada di buffer sebelum transisi —
        /// role non-per-index memakai index 0; Rect & RectEdge memakai 6 keping.</summary>
        static void EnsureKeys()
        {
            foreach (Role r in Enum.GetValues(typeof(Role)))
            {
                if (r == Role.Rect || r == Role.RectEdge)
                    for (int i = 0; i < Current.Rects.Length; i++) _ = ColorOf(r, i);
                else _ = ColorOf(r, 0);
            }
        }

        static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("[ThemeApplier]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<ThemeRunner>();
        }

        internal static void Tick(float dt)
        {
            if (_t >= 1f) return;
            _t = Mathf.Clamp01(_t + dt / TransitionSeconds);
            float e = _t * _t * (3f - 2f * _t);   // smoothstep — masuk/keluar mulus
            // salin kunci dulu: indexer Dictionary menaikkan versi enumerator
            foreach (var k in new List<(Role, int)>(_cur.Keys))
                _cur[k] = Color.Lerp(_from[k], _to[k], e);
            Fire();
        }

        static void Fire()
        {
            // snapshot: callback boleh Unsubscribe/Subscribe di tengah iterasi
            var snap = new List<(Role role, int rectIndex, Action<Color> cb)>(_subscribers);
            foreach (var s in snap)
            {
                if (!_subscribers.Contains(s)) continue;   // dibatalkan oleh callback lain
                s.cb(ColorOf(s.role, s.rectIndex));
            }
        }

        // rumus turunan bag. 3 (V×0,85, S+0,10..0,14) sudah dibenamkan ke tabel tema
        // (ShadowPrimary/ShadowAccent/ShadowBoard/PieceEdges) supaya flat & konsisten;
        // bayangan permukaan memakai peran Tepi persis (ditangani FrameTint/CardTint).

        /// <summary>Warna gelap pilihan auto-kontras (tabel bag. 4): #1B1B24.</summary>
        public static Color ContrastDark => new Color32(0x1B, 0x1B, 0x24, 0xFF);

        /// <summary>Luminansi relatif WCAG (kanal sRGB linearized).</summary>
        public static float RelativeLuminance(Color c)
        {
            float Lin(float u) =>
                u <= 0.04045f ? u / 12.92f : Mathf.Pow((u + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        /// <summary>Rasio kontras WCAG dua warna (>= 4,5 untuk teks biasa, >= 3 untuk teks besar).</summary>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a);
            float lb = RelativeLuminance(b);
            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05f) / (lb + 0.05f);
        }

        /// <summary>Auto-kontras (bag. 4): teks di atas Utama, Aksen, keping, dan
        /// huruf logo memilih #1B1B24 atau putih berdasar rasio kontras WCAG —
        /// minimal 4,5:1 (teks besar minimal 3:1); bila keduanya di bawah ambang,
        /// ambil rasio tertinggi (tetap ≥3:1 pada praktiknya).</summary>
        public static Color ContrastTextOn(Color bg)
        {
            var dark = ContrastDark;
            float rd = ContrastRatio(dark, bg);       // rasio teks gelap
            float rw = ContrastRatio(Color.white, bg); // rasio teks putih
            bool darkOk = rd >= 4.5f, whiteOk = rw >= 4.5f;
            if (darkOk && whiteOk) return rd >= rw ? dark : Color.white;
            if (whiteOk) return Color.white;
            if (darkOk) return dark;
            return rd >= rw ? dark : Color.white;
        }

        public static Color Parse(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;

        sealed class ThemeRunner : MonoBehaviour
        {
            void Update() => Tick(Time.unscaledDeltaTime);
        }
    }
}
