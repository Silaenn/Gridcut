using UnityEngine;

namespace GridCut.Game
{
    /// <summary>Adjusts rect offsets to the device safe area (notches / rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        RectTransform _rt;
        Rect _applied;

        void OnEnable()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            var sa = Screen.safeArea;
            if (sa != _applied) Apply();
        }

        void Apply()
        {
            var sa = Screen.safeArea;
            _applied = sa;
            var rt = _rt != null ? _rt : (RectTransform)transform;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = sa.position;
            Vector2 max = sa.position + sa.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
