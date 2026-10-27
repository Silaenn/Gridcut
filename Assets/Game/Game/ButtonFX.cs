using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Keadaan tombol (spec bagian 8):
    /// - hover desktop: naik 10 px & jarak bayangan +10 dalam 100 ms;
    /// - ditekan: turun (|ShadowDy| − 4) px & jarak bayangan 4 dalam 100 ms;
    /// - nonaktif: CanvasGroup alpha 0,45 + Button.interactable.
    /// Menempel otomatis lewat AppRoot.BakeButtonFeel dan per tombol resep PressFx.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonFX : MonoBehaviour,
        IPointerDownHandler, IPointerExitHandler, IPointerUpHandler, IPointerEnterHandler
    {
        /// <summary>Jarak bayangan saat istirahat (utama -16, sekunder/ikon -12).</summary>
        public float ShadowDy = -12f;

        const float Durasi = 0.1f;
        const float HoverNaik = 8f;

        Button _btn;
        RectTransform _rt;
        Shadow _shadow;
        CanvasGroup _group;
        float _restY;
        bool _restKnown;
        Coroutine _tween;
        bool _interactable = true;
        bool _hover;
        bool _pressed;

        void Awake()
        {
            _btn = GetComponent<Button>();
            _rt = (RectTransform)transform;
            _shadow = GetComponent<Shadow>();
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _interactable = _btn == null || _btn.interactable;
            _group.alpha = _interactable ? 1f : 0.45f;
        }

        void Update()
        {
            if (SaveStore.ReduceMotion)
            {
                if (_tween != null) { StopCoroutine(_tween); _tween = null; }
                _hover = false;
                _pressed = false;
                if (_rt != null && _restKnown)
                {
                    var p = _rt.anchoredPosition;
                    p.y = _restY;
                    _rt.anchoredPosition = p;
                }
                if (_shadow != null) _shadow.effectDistance = new Vector2(0f, ShadowDy);
            }

            bool inter = _btn == null || _btn.interactable;
            if (inter == _interactable) return;
            _interactable = inter;
            if (_group != null) _group.alpha = inter ? 1f : 0.45f;
            if (!inter)
            {
                _hover = false;
                _pressed = false;
                // PastikanRest() harus dipanggil SEBELUM membaca _restY: kalau tidak,
                // tombol yang belum pernah di-hover akan memakai nilai awal 0 dan naik.
                PastikanRest();
                TweenKe(_restY, ShadowDy);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable) return;
            // InputSystemUIInputModule memakai deviceId sebagai pointerId (mouse = 1),
            // jadi pointerId >= 0 bukan penanda sentuh. Cek tipe pointer-nya.
            if (eventData is UnityEngine.InputSystem.UI.ExtendedPointerEventData ext
                && ext.pointerType == UnityEngine.InputSystem.UI.UIPointerType.Touch) return;
            PastikanRest();
            _hover = true;
            if (!_pressed) TweenKe(_restY + HoverNaik, ShadowDy - HoverNaik);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PastikanRest();
            _hover = false;
            _pressed = false;
            TweenKe(_restY, ShadowDy);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            PastikanRest();
            _pressed = true;
            float jarak = Mathf.Max(4f, Mathf.Abs(ShadowDy)) - 4f;
            TweenKe(_restY - jarak, -4f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PastikanRest();
            _pressed = false;
            if (_hover) TweenKe(_restY + HoverNaik, ShadowDy - HoverNaik);
            else TweenKe(_restY, ShadowDy);
        }

        void PastikanRest()
        {
            if (_restKnown) return;
            _restY = _rt.anchoredPosition.y;
            _restKnown = true;
        }

        /// <summary>Kunci ulang posisi istirahat (dipakai setelah animasi intro
        /// menggeser tombol) dan bersihkan state hover/tekan yang tertinggal.</summary>
        public void RebaseRest()
        {
            if (_rt == null) return;
            if (_tween != null) { StopCoroutine(_tween); _tween = null; }
            _hover = false;
            _pressed = false;
            _restY = _rt.anchoredPosition.y;
            _restKnown = true;
            var p = _rt.anchoredPosition;
            p.y = _restY;
            _rt.anchoredPosition = p;
            if (_shadow != null) _shadow.effectDistance = new Vector2(0f, ShadowDy);
        }

        void TweenKe(float targetY, float targetShadow)
        {
            if (_rt == null) return;
            PastikanRest();
            if (_tween != null) StopCoroutine(_tween);
            if (SaveStore.ReduceMotion)
            {
                _tween = null;
                var p = _rt.anchoredPosition;
                p.y = _restY;
                _rt.anchoredPosition = p;
                if (_shadow != null) _shadow.effectDistance = new Vector2(0f, ShadowDy);
                return;
            }
            _tween = StartCoroutine(Animasi(targetY, targetShadow));
        }

        IEnumerator Animasi(float targetY, float targetShadow)
        {
            float y0 = _rt.anchoredPosition.y;
            float d0 = _shadow != null ? _shadow.effectDistance.y : 0f;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Clamp01(t + Time.unscaledDeltaTime / Durasi);
                float e = t * t * (3f - 2f * t);   // smoothstep
                var p = _rt.anchoredPosition;
                p.y = Mathf.Lerp(y0, targetY, e);
                _rt.anchoredPosition = p;
                if (_shadow != null)
                    _shadow.effectDistance = new Vector2(0f, Mathf.Lerp(d0, targetShadow, e));
                yield return null;
            }
            _tween = null;
        }

        void OnDisable()
        {
            _hover = false;
            _pressed = false;
            if (_tween != null) { StopCoroutine(_tween); _tween = null; }
            if (_rt != null && _restKnown)
            {
                var p = _rt.anchoredPosition;
                p.y = _restY;
                _rt.anchoredPosition = p;
            }
            if (_shadow != null) _shadow.effectDistance = new Vector2(0f, ShadowDy);
        }
    }
}