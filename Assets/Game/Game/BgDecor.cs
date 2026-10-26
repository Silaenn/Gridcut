using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Latar dekoratif spesifikasi bag. 2 + 5 + 8. Semua elemen sudah dibake di scene —
    /// GO "Dots" (Image Tiled) dan enam kotak Rounded "Box0".."Box5" di bawah Background.
    /// Satu Update menggerakkan keenam kotak: naik-turun sinus amplitudo 12 px dengan
    /// periode acak 4–7 dtk dan fase acak, serta rotasi ±4° periode 6–9 dtk. Tidak ada
    /// alokasi per frame. Gerak berhenti saat menu tidak tampil atau Kurangi gerakan aktif.
    /// </summary>
    public class BgDecor : MonoBehaviour
    {
        [Header("Tuning")]
        [Range(0f, 1f)] public float DotStrength = 0.45f;   // alpha titik
        [Range(0f, 1f)] public float BoxAlpha = 0.55f;      // alpha kotak (spec bag. 5)
        public float Amplitude = 12f;                       // px, naik-turun
        public float RotAmplitude = 4f;                     // derajat, rotasi

        struct DriftBox
        {
            public RectTransform Rt;
            public Image Img;
            public int ColorIndex;     // tint peran Rect i % 6
            public Vector2 Base;       // posisi bake
            public float BaseRotZ;     // rotasi bake
            public float BobPeriod;    // 4–7 dtk
            public float BobPhase;     // 0..2π
            public float RotPeriod;    // 6–9 dtk
            public float RotPhase;     // 0..2π
        }

        Image _dots;
        readonly List<DriftBox> _boxes = new List<DriftBox>();
        System.Action<Color> _tint;
        bool _running;
        bool _reducedApplied;

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
                    BaseRotZ = rt.localEulerAngles.z,
                    BobPeriod = Random.Range(4f, 7f),
                    BobPhase = Random.Range(0f, Mathf.PI * 2f),
                    RotPeriod = Random.Range(6f, 9f),
                    RotPhase = Random.Range(0f, Mathf.PI * 2f),
                });
            }

            _tint = _ => ApplyThemeColors();
            ThemeApplier.Subscribe(ThemeApplier.Role.Dot, _tint, true);
        }

        void OnDestroy()
        {
            if (_tint != null) ThemeApplier.Unsubscribe(_tint);
        }

        /// <summary>Kotak hanya tampil di menu utama; animasi ikut berhenti total
        /// saat layar lain tampil (spec bagian 8).</summary>
        public void SetBoxesVisible(bool show)
        {
            _running = show;
            for (int i = 0; i < _boxes.Count; i++)
            {
                var go = _boxes[i].Rt.gameObject;
                if (go.activeSelf != show) go.SetActive(show);
            }
            if (show && SaveStore.ReduceMotion) ApplyStill();
        }

        void Update()
        {
            if (!_running) return;

            if (SaveStore.ReduceMotion)
            {
                if (!_reducedApplied) ApplyStill();
                return;
            }
            _reducedApplied = false;

            float t = Time.unscaledTime;
            for (int i = 0; i < _boxes.Count; i++)
            {
                var b = _boxes[i];
                float bob = Mathf.Sin((t / b.BobPeriod) * (Mathf.PI * 2f) + b.BobPhase) * Amplitude;
                float rot = Mathf.Sin((t / b.RotPeriod) * (Mathf.PI * 2f) + b.RotPhase) * RotAmplitude;
                b.Rt.anchoredPosition = new Vector2(b.Base.x, b.Base.y + bob);
                b.Rt.localRotation = Quaternion.Euler(0f, 0f, b.BaseRotZ + rot);
            }
        }

        void ApplyStill()
        {
            _reducedApplied = true;
            for (int i = 0; i < _boxes.Count; i++)
            {
                var b = _boxes[i];
                b.Rt.anchoredPosition = b.Base;
                b.Rt.localRotation = Quaternion.Euler(0f, 0f, b.BaseRotZ);
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