using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridCut.Game
{
    /// <summary>
    /// Popup bersama (spec bagian 9): Backdrop penuh layar + Panel tengah.
    /// Buka: backdrop 150 ms, panel skala 0,9→1 + fade 220 ms (ease out back).
    /// Tutup: kebalikannya 150 ms. Kurangi gerakan: hanya fade.
    /// Popup teratas ditutup oleh Escape/Back; backdrop menutup Tema/Pengaturan,
    /// tetapi tidak menutup Konfirmasi. Semua memakai waktu unscaled.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PopupView : MonoBehaviour
    {
        public RectTransform Panel;
        public Image Backdrop;
        public CanvasGroup BackdropGroup;
        public CanvasGroup PanelGroup;
        [Range(0f, 1f)] public float BackdropAlpha = 0.55f;
        public bool CloseOnBackdrop = true;

        const float OpenBackdropDur = 0.15f;
        const float OpenPanelDur = 0.22f;
        const float CloseDur = 0.15f;
        const float ClosedScale = 0.9f;

        static readonly List<PopupView> Active = new List<PopupView>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => Active.Clear();

        Coroutine _co;
        bool _open;
        bool _counted;

        public bool IsOpen => _open;

        public static bool AnyOpen => Active.Count > 0;

        void Awake()
        {
            if (Panel == null)
            {
                Panel = transform.Find("Panel") as RectTransform;
                if (Panel == null) Panel = transform.Find("Card") as RectTransform;
            }
            if (Backdrop == null)
            {
                var b = transform.Find("Backdrop");
                if (b == null) b = transform.Find("Dim");
                if (b != null) Backdrop = b.GetComponent<Image>();
            }
            if (Backdrop != null)
            {
                Backdrop.raycastTarget = true;
                if (BackdropGroup == null) BackdropGroup = Backdrop.GetComponent<CanvasGroup>();
                if (BackdropGroup == null) BackdropGroup = Backdrop.gameObject.AddComponent<CanvasGroup>();
            }
            if (Panel != null && PanelGroup == null)
            {
                PanelGroup = Panel.GetComponent<CanvasGroup>();
                if (PanelGroup == null) PanelGroup = Panel.gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Show()
        {
            if (_open) return;
            if (_co != null) { StopCoroutine(_co); _co = null; }

            _open = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (Backdrop != null)
            {
                Backdrop.color = new Color32(0x1B, 0x1B, 0x24, 0xFF);
                Backdrop.raycastTarget = true;
            }
            if (BackdropGroup != null) BackdropGroup.alpha = 0f;
            if (PanelGroup != null)
            {
                PanelGroup.alpha = 0f;
                PanelGroup.interactable = true;
                PanelGroup.blocksRaycasts = true;
            }
            if (Panel != null) Panel.localScale = Vector3.one;

            if (Active.Contains(this)) Active.Remove(this);
            Active.Add(this);
            if (!_counted)
            {
                _counted = true;
                if (AppRoot.Instance != null) AppRoot.Instance.PopupOpened();
            }

            _co = StartCoroutine(OpenCo());
        }

        public void Hide()
        {
            if (!_open) return;
            _open = false;
            ClearSelection();
            Unregister();
            if (_co != null) { StopCoroutine(_co); _co = null; }
            _co = StartCoroutine(CloseCo());
        }

        /// <summary>Tutup tanpa animasi (dipakai saat layar induk disembunyikan).</summary>
        public void HideImmediate()
        {
            _open = false;
            Unregister();
            if (_co != null) { StopCoroutine(_co); _co = null; }
            if (PanelGroup != null) PanelGroup.alpha = 0f;
            if (BackdropGroup != null) BackdropGroup.alpha = 0f;
            if (Panel != null) Panel.localScale = Vector3.one * ClosedScale;
            gameObject.SetActive(false);
        }

        void Unregister()
        {
            if (Active.Contains(this)) Active.Remove(this);
            if (_counted)
            {
                _counted = false;
                if (AppRoot.Instance != null) AppRoot.Instance.PopupClosed();
            }
        }

        void ClearSelection()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var sel = es.currentSelectedGameObject;
            if (sel != null && sel.transform.IsChildOf(transform))
                es.SetSelectedGameObject(null);
        }

        public void OnBackdropClick()
        {
            if (CloseOnBackdrop) Hide();
        }

        /// <summary>Escape/Back: tutup popup paling atas; true bila ada yang ditutup.</summary>
        public static bool CloseTop()
        {
            if (Active.Count == 0) return false;
            Active[Active.Count - 1].Hide();
            return true;
        }

        IEnumerator OpenCo()
        {
            bool reduce = SaveStore.ReduceMotion;
            float bt = 0f, pt = 0f;
            float bFrom = BackdropGroup != null ? BackdropGroup.alpha : 0f;
            if (Panel != null && !reduce) Panel.localScale = Vector3.one * ClosedScale;

            while (pt < OpenPanelDur)
            {
                float dt = Time.unscaledDeltaTime;
                bt += dt;
                pt += dt;

                float bk = Mathf.Clamp01(bt / OpenBackdropDur);
                if (BackdropGroup != null)
                    BackdropGroup.alpha = Mathf.Lerp(bFrom, BackdropAlpha, bk * bk * (3f - 2f * bk));

                float pk = Mathf.Clamp01(pt / OpenPanelDur);
                if (PanelGroup != null) PanelGroup.alpha = pk;
                if (Panel != null && !reduce)
                {
                    float s = Mathf.Lerp(ClosedScale, 1f, EaseOutBack(pk));
                    Panel.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }

            if (BackdropGroup != null) BackdropGroup.alpha = BackdropAlpha;
            if (PanelGroup != null) PanelGroup.alpha = 1f;
            if (Panel != null) Panel.localScale = Vector3.one;
            _co = null;
        }

        IEnumerator CloseCo()
        {
            bool reduce = SaveStore.ReduceMotion;
            float t = 0f;
            float b0 = BackdropGroup != null ? BackdropGroup.alpha : 0f;
            float p0 = PanelGroup != null ? PanelGroup.alpha : 1f;
            float s0 = Panel != null ? Panel.localScale.x : 1f;

            while (t < CloseDur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / CloseDur);
                if (BackdropGroup != null) BackdropGroup.alpha = Mathf.Lerp(b0, 0f, k);
                if (PanelGroup != null) PanelGroup.alpha = Mathf.Lerp(p0, 0f, k);
                if (Panel != null && !reduce)
                {
                    float s = Mathf.Lerp(s0, ClosedScale, k);
                    Panel.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }

            if (PanelGroup != null) PanelGroup.alpha = 0f;
            if (BackdropGroup != null) BackdropGroup.alpha = 0f;
            if (Panel != null) Panel.localScale = Vector3.one * ClosedScale;
            _co = null;
            gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (_open)
            {
                _open = false;
                Unregister();
            }
            if (_co != null) { StopCoroutine(_co); _co = null; }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}