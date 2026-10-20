using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Shared button feel per UI rules: pressed state scales the button to 0.95.
    /// Attached automatically to every Button under the app root; disabled alpha
    /// is baked in AppRoot.BakeButtonFeel.
    /// </summary>
    public class ButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Vector3 _base;

        void OnEnable()
        {
            _base = transform.localScale;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (TryGetComponent(out Button b) && b.interactable)
                transform.localScale = _base * 0.95f;
        }

        public void OnPointerUp(PointerEventData e) => Reset();
        public void OnPointerExit(PointerEventData e) => Reset();

        void OnDisable() => Reset();

        void Reset()
        {
            if (_base == Vector3.zero) _base = Vector3.one;
            transform.localScale = _base;
        }
    }
}
