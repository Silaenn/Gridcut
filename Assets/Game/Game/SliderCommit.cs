using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>Simpan nilai slider saat jari/mouse dilepas (spec 11).</summary>
    public class SliderCommit : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        public Slider Slider;
        public System.Action<float> Commit;

        float _last;
        bool _down;

        public void OnPointerDown(PointerEventData eventData) => _down = true;

        public void OnPointerUp(PointerEventData eventData) => Fire();

        public void OnEndDrag(PointerEventData eventData) => Fire();

        void Fire()
        {
            if (!_down) return;
            _down = false;
            if (Time.unscaledTime - _last < 0.05f) return;
            _last = Time.unscaledTime;
            if (Slider != null && Commit != null) Commit(Slider.value);
        }
    }
}