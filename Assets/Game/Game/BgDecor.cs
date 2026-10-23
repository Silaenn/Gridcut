using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Latar dekoratif spesifikasi bag. 2: sprite pola titik (Image Tiled — tile 48px,
    /// dot diameter 9px, jarak 48px) + kotak Rounded 9-slice melayang (border 56).
    /// Kedua sprite putih; warna lewat Image.color mengikuti tema (GridLine utk titik,
    /// 6 warna rect utk kotak). Terpasang pada GO Background full-screen di luar
    /// SafeArea. Semua non-raycast agar tidak menghalangi input UI.
    /// </summary>
    public class BgDecor : MonoBehaviour
    {
        [Header("Wired in scene")]
        public Sprite DotsSprite;   // Assets/Game/UI/Dots.png (putih, tile 48, dot d=9)
        public Sprite BoxSprite;    // Assets/Game/UI/Rounded.png (putih 9-slice, border 56)

        [Header("Tuning")]
        public int BoxCount = 7;
        [Range(0f, 1f)] public float DotStrength = 0.45f;   // alpha titik
        [Range(0f, 1f)] public float BoxAlphaMax = 0.14f;
        public int Seed = 22234;

        struct DriftBox
        {
            public RectTransform Rt;
            public Image Img;
            public int ColorIndex;     // tint peran Rect i % 6
            public float Alpha;
            public float Speed;        // px/detik ke atas
            public float SwayAmp;      // goyang horizontal px
            public float SwayPhase;
        }

        Image _dots;
        readonly List<DriftBox> _boxes = new List<DriftBox>();
        System.Action<Color> _tint;

        void Awake()
        {
            if (!(transform is RectTransform)) return;
            if (DotsSprite == null || BoxSprite == null)
                Debug.LogWarning("[BgDecor] DotsSprite/BoxSprite belum di-wire — lapisan dekor dilewati");
            MakeDots();
            MakeBoxes();
            _tint = ApplyThemeColors;
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _tint, true);
        }

        void OnDestroy()
        {
            if (_tint != null) ThemeApplier.Unsubscribe(_tint);
        }

        /// <summary>Image Type Tiled memakai sprite 48px (PPU 100): jarak antartitik
        /// 48 px pada resolusi acuan, menutupi seluruh latar. Sprite Tiled tidak
        /// boleh masuk Sprite Atlas (spesifikasi bag. 2).</summary>
        void MakeDots()
        {
            if (DotsSprite == null) return;
            var go = new GameObject("Dots", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _dots = go.GetComponent<Image>();
            _dots.sprite = DotsSprite;
            _dots.type = Image.Type.Tiled;
            _dots.raycastTarget = false;
        }

        void MakeBoxes()
        {
            if (BoxSprite == null) return;
            var rng = new System.Random(Seed);
            var pr = ((RectTransform)transform).rect;
            for (int i = 0; i < BoxCount; i++)
            {
                var go = new GameObject($"Box{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<Image>();
                img.sprite = BoxSprite;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.pixelsPerUnitMultiplier = 1.56f;   // kotak melayang radius 36 (56/1.56)
                img.raycastTarget = false;

                var rt = (RectTransform)go.transform;
                float side = Mathf.Lerp(90f, 320f, (float)rng.NextDouble());
                float xr = pr.width * 0.95f;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(side, side * Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble()));
                rt.anchoredPosition = new Vector2(
                    Mathf.Lerp(-xr * 0.5f, xr * 0.5f, (float)rng.NextDouble()),
                    Mathf.Lerp(-pr.height * 0.6f, pr.height * 0.6f, (float)rng.NextDouble()));
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-6f, 6f, (float)rng.NextDouble()));

                _boxes.Add(new DriftBox
                {
                    Rt = rt,
                    Img = img,
                    ColorIndex = i % 6,
                    Alpha = Mathf.Lerp(0.06f, BoxAlphaMax, (float)rng.NextDouble()),
                    Speed = Mathf.Lerp(7f, 17f, (float)rng.NextDouble()),
                    SwayAmp = Mathf.Lerp(20f, 55f, (float)rng.NextDouble()),
                    SwayPhase = (float)rng.NextDouble() * Mathf.PI * 2f,
                });
            }
        }

        void Update()
        {
            var pr = ((RectTransform)transform).rect;
            float t = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            for (int i = 0; i < _boxes.Count; i++)
            {
                var b = _boxes[i];
                Vector2 p = b.Rt.anchoredPosition;
                p.y += b.Speed * dt;
                p.x += Mathf.Sin(t * 0.4f + b.SwayPhase) * b.SwayAmp * dt;
                if (p.y - b.Rt.rect.height * 0.5f > pr.height * 0.5f)
                    p.y = -pr.height * 0.5f - b.Rt.rect.height * 0.5f - 30f;
                b.Rt.anchoredPosition = p;
            }
        }

        void ApplyThemeColors(Color _)
        {
            if (_dots != null)
            {
                var c = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                c.a = DotStrength;
                _dots.color = c;
            }
            for (int i = 0; i < _boxes.Count; i++)
            {
                var c = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, _boxes[i].ColorIndex);
                c.a = _boxes[i].Alpha;
                _boxes[i].Img.color = c;
            }
        }
    }
}
