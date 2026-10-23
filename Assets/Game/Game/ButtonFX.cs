using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Keadaan tombol (spec): ditekan turun (jarak bayangan - 4) px dan jarak bayangan
    /// menjadi 4 dalam 100 ms - kembali saat dilepas. Nonaktif: CanvasGroup alpha 0,45;
    /// klik terkunci lewat Button.interactable. Menempel otomatis lewat
    /// AppRoot.BakeButtonFeel dan per tombol resep lewat UiFactory.PressFx.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonFX : MonoBehaviour, IPointerDownHandler, IPointerExitHandler, IPointerUpHandler
    {
        /// <summary>Jarak bayangan saat istirahat (utama -16, sekunder/ikon -12).</summary>
        public float ShadowDy = -12f;

        const float Durasi = 0.1f;

        Button _btn;
        RectTransform _rt;
        Shadow _shadow;
        CanvasGroup _group;
        float _restY;
        bool _restKnown;
        Coroutine _tween;
        bool _interactable = true;

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
            bool inter = _btn == null || _btn.interactable;
            if (inter == _interactable) return;
            _interactable = inter;
            if (_group != null) _group.alpha = inter ? 1f : 0.45f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            Mulai(true);
        }

        public void OnPointerUp(PointerEventData eventData) => Mulai(false);
        public void OnPointerExit(PointerEventData eventData) => Mulai(false);

        void Mulai(bool turun)
        {
            if (_rt == null) return;
            if (_shadow == null) _shadow = GetComponent<Shadow>();
            if (!_restKnown) { _restY = _rt.anchoredPosition.y; _restKnown = true; }
            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(Animasi(turun));
        }

        IEnumerator Animasi(bool turun)
        {
            // tombol tanpa bayangan: jarak 0 - 4 dijepit minimal 4 px
            float jarak = Mathf.Max(4f, Mathf.Abs(ShadowDy)) - 4f;
            float y0 = _rt.anchoredPosition.y;
            float y1 = turun ? _restY - jarak : _restY;
            float d0 = _shadow != null ? _shadow.effectDistance.y : 0f;
            float d1 = turun ? -4f : ShadowDy;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Clamp01(t + Time.unscaledDeltaTime / Durasi);
                var p = _rt.anchoredPosition;
                p.y = Mathf.Lerp(y0, y1, t);
                _rt.anchoredPosition = p;
                if (_shadow != null) _shadow.effectDistance = new Vector2(0f, Mathf.Lerp(d0, d1, t));
                yield return null;
            }
            _tween = null;
        }

        void OnDisable()
        {
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
