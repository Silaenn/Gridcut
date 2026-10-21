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

        static Sprite _starFull, _starEmpty;

        /// <summary>Star sprites are session-bound objects; without domain reload the cache
        /// must be cleared between plays or the second session would keep destroyed refs.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSpriteCaches()
        {
            _starFull = null;
            _starEmpty = null;
        }

        public static Sprite StarSprite(bool full)
        {
            if (full && _starFull != null) return _starFull;
            if (!full && _starEmpty != null) return _starEmpty;

            const int S = 48;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (S - 1) / 2f, cy = (S - 1) / 2f, R = 21.5f;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;   // first point on top
                float rad = i % 2 == 0 ? R : R * 0.45f;
                pts[i] = new Vector2(cx + rad * Mathf.Cos(ang), cy + rad * Mathf.Sin(ang));
            }

            var buf = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = EdgeDistance(p, pts);
                float a = InsidePolygon(p, pts) ? 1f : Mathf.Clamp01(1f - d / 1.2f);
                if (!full) a = Mathf.Clamp01(1f - d / 1.2f);     // hollow: edge ring only
                buf[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(buf);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = full ? "star-full" : "star-empty";
            if (full) _starFull = sprite; else _starEmpty = sprite;
            return sprite;
        }

        static float EdgeDistance(Vector2 p, Vector2[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + t * ab));
            }
            return best;
        }

        static bool InsidePolygon(Vector2 p, Vector2[] pts)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].y > p.y) != (pts[j].y > p.y) &&
                    p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Row of three sprite stars (36×36 each, evenly spaced).</summary>
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
                irt.sizeDelta = new Vector2(36f, 36f);
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
