using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Latar dekoratif spesifikasi bag. 2 + 5. Semua elemen sudah dibake di scene —
    /// GO "Dots" (Image Tiled) dan enam kotak Rounded "Box0".."Box5" di bawah
    /// Background. Kode tidak membuat objek runtime: hanya mewarnai peran tema
    /// (Titik untuk pola, warna keping i%6 untuk kotak) dan menggerakkan kotak
    /// naik-turun pelan. Nama BoxN = indeks warna keping (bukan urutan posisi).
    /// </summary>
    public class BgDecor : MonoBehaviour
    {
        [Header("Tuning")]
        [Range(0f, 1f)] public float DotStrength = 0.45f;   // alpha titik
        [Range(0f, 1f)] public float BoxAlpha = 0.55f;      // alpha kotak (spec bag. 5)
        public float BobAmplitude = 6f;                     // px, naik-turun
        public float BobSpeed = 0.35f;                      // rad/detik
        public float SwayAmplitude = 3f;                    // px, goyang horizontal
        public float SwaySpeed = 0.23f;

        struct DriftBox
        {
            public RectTransform Rt;
            public Image Img;
            public int ColorIndex;     // tint peran Rect i % 6
            public Vector2 Base;       // posisi bake (pivot tengah)
            public float Phase;
        }

        Image _dots;
        readonly List<DriftBox> _boxes = new List<DriftBox>();
        System.Action<Color> _tint;

        void Awake()
        {
            var dotsT = transform.Find("Dots");
            if (dotsT != null) _dots = dotsT.GetComponent<Image>();
            if (_dots == null) Debug.LogWarning("[BgDecor] GO 'Dots' belum dibake di scene");

            for (int i = 0; i < 6; i++)
            {
                var t = transform.Find($"Box{i}");
                var img = t != null ? t.GetComponent<Image>() : null;
                if (img == null)
                {
                    Debug.LogWarning($"[BgDecor] GO 'Box{i}' belum dibake di scene");
                    continue;
                }
                var rt = (RectTransform)t;
                _boxes.Add(new DriftBox
                {
                    Rt = rt,
                    Img = img,
                    ColorIndex = i,
                    Base = rt.anchoredPosition,
                    Phase = i * 1.047f,      // 60° per kotak
                });
            }

            _tint = _ => ApplyThemeColors();
            ThemeApplier.Subscribe(ThemeApplier.Role.Dot, _tint, true);
        }

        void OnDestroy()
        {
            if (_tint != null) ThemeApplier.Unsubscribe(_tint);
        }

        void Update()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < _boxes.Count; i++)
            {
                var b = _boxes[i];
                float ph = t * BobSpeed + b.Phase;
                b.Rt.anchoredPosition = b.Base + new Vector2(
                    Mathf.Sin(ph * 0.7f + 1.3f) * SwayAmplitude,
                    Mathf.Sin(ph) * BobAmplitude);
            }
        }

        void ApplyThemeColors()
        {
            if (_dots != null)
            {
                var c = ThemeApplier.ColorOf(ThemeApplier.Role.Dot);
                c.a = DotStrength;
                _dots.color = c;
            }
            for (int i = 0; i < _boxes.Count; i++)
            {
                var c = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, _boxes[i].ColorIndex);
                c.a = BoxAlpha;
                _boxes[i].Img.color = c;
            }
        }
    }
}
