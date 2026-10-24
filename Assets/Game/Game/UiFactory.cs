using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GridCut.Game
{
    /// <summary>
    /// Construction helpers for the DYNAMIC parts only (board cells/lines/fills and
    /// level-select buttons). Static UI lives in the scene; runtime helpers never
    /// create screens or panels.
    /// </summary>
    public static class UiFactory
    {
        public static RectTransform NewRt(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            return (RectTransform)go.transform;
        }

        /// <summary>Scene-ref fallback: keep the wired value, otherwise look it up by child path.</summary>
        public static T Wire<T>(Component host, T current, string path) where T : Component
        {
            if (current != null) return current;   // Unity overloaded ==: destroyed refs recover too
            var ch = host.transform.Find(path);
            return ch != null ? ch.GetComponentInChildren<T>(true) : null;
        }

        public static GameObject WireGo(Component host, GameObject current, string path)
        {
            if (current != null) return current;
            var ch = host.transform.Find(path);
            return ch != null ? ch.gameObject : null;
        }

        public static Image Panel(Transform parent, string name, Color color, bool raycast = false,
            Sprite sprite = null, float ppuMultiplier = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            // panel bulat (kartu/kotak) memakai sprite Rounded 9-slice; radius = border(56)/multiplier
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;   // method local bernama Image → kelas harus dieja penuh
                img.pixelsPerUnitMultiplier = ppuMultiplier;
            }
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return img;
        }

        public static Image Image(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.transform.SetParent(parent, false);
            return img;
        }

        public static TMP_Text Text(Transform parent, string name, string content,
            float size, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Left,
            Vector2? sizeDelta = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.color = color ?? Color.white;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            go.transform.SetParent(parent, false);
            if (sizeDelta.HasValue) t.rectTransform.sizeDelta = sizeDelta.Value;
            return t;
        }

        /// <summary>Solid button with a stretched TMP label (used for dynamic level cells).</summary>
        public static Button Button(string name, Transform parent, string label,
            float textSize, Action onClick, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var img = go.GetComponent<Image>();
            img.color = color;
            // tombol/ubin level = Rounded Sliced, radius 44 (56/1.27) — tabel resep komponen
            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            if (rounded != null)
            {
                img.sprite = rounded;
                img.type = UnityEngine.UI.Image.Type.Sliced;   // method local bernama Image → kelas harus dieja penuh
                img.pixelsPerUnitMultiplier = 1.27f;
            }
            var btn = go.GetComponent<Button>();
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = sizeDelta;

            var t = Text(rt.transform, "Label", label, textSize, null, TextAlignmentOptions.Center);
            // tombol runtime (level & CTA) = angka/tombol utama → font Fredoka-Bold (spesifikasi font)
            var bold = AppRoot.Instance != null ? AppRoot.Instance.FontBold : null;
            if (bold != null) t.font = bold;
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // ------------------------------ shadow & tombol utama ------------------------------
        /// <summary>Shadow bawaan UI "tombol tebal": satu komponen, tanpa objek
        /// tambahan; Use Graphic Alpha dimatikan sesuai spec.</summary>
        public static void ApplyShadow(Component host, Vector2 distance, Color color)
        {
            var sh = host.GetComponent<Shadow>();
            if (sh == null) sh = host.gameObject.AddComponent<Shadow>();
            sh.effectColor = color;
            sh.effectDistance = distance;
            sh.useGraphicAlpha = false;
        }

        /// <summary>Resep tombol utama: latar warna Utama (Primary), bayangan tebal
        /// (0, -shadowDy) = bayangan warna Utama; label auto-kontras gelap/putih;
        /// tanpa bingkai. (Tabel bag. 4: tombol utama = peran Utama.)</summary>
        public static void PrimaryTint(Button b, float shadowDy)
        {
            if (b == null) return;
            var primary = ThemeApplier.ColorOf(ThemeApplier.Role.Primary);
            var img = b.GetComponent<Image>();
            if (img != null) img.color = primary;
            ApplyShadow(b.transform, new Vector2(0f, shadowDy), ThemeApplier.ColorOf(ThemeApplier.Role.ShadowPrimary));
            var lbl = b.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.color = ThemeApplier.ContrastTextOn(primary);
        }

        /// <summary>Kerangka tombol sekunder/ikon: akar warna Tepi (GridLine) +
        /// bayangan (0,-12); anak "Fill" warna Permukaan (CellEmpty); pada layout
        /// ikon, gambar anak selain Fill diwarnai Teks.</summary>
        public static void FrameTint(Button b, bool circleIcons)
        {
            if (b == null) return;
            var edge = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            var img = b.GetComponent<Image>();
            if (img != null) img.color = edge;
            ApplyShadow(b.transform, new Vector2(0f, -12f), edge);
            foreach (Transform child in b.transform)
            {
                var gi = child.GetComponent<Image>();
                if (gi == null) continue;
                if (child.name == "Fill")
                    gi.color = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
                else if (circleIcons)
                    gi.color = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            }
        }

        /// <summary>Kartu: akar warna Tepi (GridLine) + bayangan (0,-12); anak "Fill"
        /// warna Permukaan (CellEmpty).</summary>
        public static void CardTint(Component card)
        {
            if (card == null) return;
            var edge = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            var img = card.GetComponent<Image>();
            if (img != null) img.color = edge;
            ApplyShadow(card.transform, new Vector2(0f, -12f), edge);
            var fill = card.transform.Find("Fill");
            if (fill != null)
            {
                var fi = fill.GetComponent<Image>();
                if (fi != null) fi.color = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            }
        }

        /// <summary>Keadaan tombol: ditekan turun (|dy| - 4) px & bayangan menyusut ke -4
        /// dalam 100 ms; nonaktif = CanvasGroup alpha 0,45 (dikerjakan ButtonFX). Tombol
        /// yang jadi anak langsung LayoutGroup dibungkus "PressSeat" agar offset tekan
        /// tidak ditimpa layout.</summary>
        public static void PressFx(Button b, float shadowDy)
        {
            if (b == null) return;
            var host = (RectTransform)b.transform;
            var pr = host.parent as RectTransform;
            if (pr != null && pr.GetComponent<LayoutGroup>() != null && pr.name != "PressSeat")
            {
                var seatGo = new GameObject("PressSeat", typeof(RectTransform));
                seatGo.layer = host.gameObject.layer;
                var seat = (RectTransform)seatGo.transform;
                seat.SetParent(pr, false);
                seat.anchorMin = host.anchorMin;
                seat.anchorMax = host.anchorMax;
                seat.pivot = host.pivot;
                seat.anchoredPosition = host.anchoredPosition;
                seat.sizeDelta = host.sizeDelta;
                host.SetParent(seat, false);
                host.anchorMin = new Vector2(0.5f, 0.5f);
                host.anchorMax = new Vector2(0.5f, 0.5f);
                host.anchoredPosition = Vector2.zero;
            }
            var fx = b.GetComponent<ButtonFX>();
            if (fx == null) fx = b.gameObject.AddComponent<ButtonFX>();
            fx.ShadowDy = shadowDy;
        }

        // ----------------------------- star sprites -----------------------------
        // LiberationSans SDF (TMP essentials) has no ★/☆ glyphs; stars are rasterized
        // procedurally once and shown as Images so progress reads the same everywhere.

        // ------------------------------ shared sprites ------------------------------
        // Ikon Kenney (putih-alpha, 128x128, Assets/Game/UI) wired di AppRoot scene —
        // bintang/gembok untuk sel level; tanpa rasterisasi runtime.

        /// <summary>Sprite from AppRoot's serialized refs; warns when the scene wiring is missing.</summary>
        public static Sprite StarSprite(bool full)
        {
            var r = AppRoot.Instance;
            var s = r == null ? null : full ? r.StarFull : r.StarEmpty;
            if (s == null) Debug.LogWarning("[UiFactory] Sprite bintang belum di-wire di AppRoot (StarFull/StarEmpty)");
            return s;
        }

        /// <summary>Row of three sprite stars (32×32, jarak antar bintang 8 px — spec).</summary>
        public static RectTransform StarRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            for (int i = 0; i < 3; i++)
            {
                var imgGo = new GameObject($"star{i}", typeof(RectTransform), typeof(Image));
                var irt = (RectTransform)imgGo.transform;
                irt.SetParent(rt, false);
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(32f, 32f);
                irt.anchoredPosition = new Vector2((i - 1) * 40f, 0f);   // 32 + 8 (jarak spec)
                var img = imgGo.GetComponent<Image>();
                img.sprite = StarSprite(true);
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            return rt;
        }

        /// <summary>Refreshes a StarRow: sprite per slot plus theme colors.</summary>
        public static void SetStars(Transform row, int filled, Color onColor, Color offColor)
        {
            for (int i = 0; i < row.childCount; i++)
            {
                var img = row.GetChild(i).GetComponent<Image>();
                bool on = i < filled;
                img.sprite = StarSprite(on);
                img.color = on ? onColor : offColor;
            }
        }
    }
}
