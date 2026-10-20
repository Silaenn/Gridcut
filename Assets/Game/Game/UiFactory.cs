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
    }
}
