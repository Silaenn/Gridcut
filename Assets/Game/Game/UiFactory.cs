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

        public static Image Panel(Transform parent, string name, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
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
            var btn = go.GetComponent<Button>();
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = sizeDelta;

            var t = Text(rt.transform, "Label", label, textSize, null, TextAlignmentOptions.Center);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
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

        /// <summary>Row of three sprite stars (32×32 each — spec bintang di ubin; evenly spaced).</summary>
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
                irt.anchoredPosition = new Vector2((i - 1) * 48f, 0f);
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
