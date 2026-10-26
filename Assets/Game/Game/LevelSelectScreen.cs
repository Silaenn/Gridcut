using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace GridCut.Game
{
    /// <summary>
    /// Level select (scene component) — spec bagian 6. Kisi 5×3 (15 ubin) x 10 halaman,
    /// header dengan tombol kembali ikon, titik halaman, panah kiri/kanan, geser layar.
    /// Ubin sudah di-bake di scene; kelas ini hanya menyambung input dan mengecat state.
    /// </summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public TMP_Text SelTitle;           // header "Pilih Level"
        public Button PrevBtn, NextBtn, BackBtn;
        public Image PrevIcon, NextIcon;    // panah halaman Kenney
        public Image BackIcon;              // panah kiri di tombol kembali
        public RectTransform GridContent;   // GridLayoutGroup 5 kolom

        const int PerPage = 15;             // 5 kolom × 3 baris
        const float ArrowDim = 0.45f;       // alpha panah nonaktif (spec)
        const float SwipeMin = 100f;        // jarak geser minimum (px kanvas)
        const float FlipSeconds = 0.18f;    // transisi halaman (panah & geser sama)

        int _page;
        RectTransform _dots;
        Image[] _dotImgs;
        CanvasGroup _gridGroup, _prevGroup, _nextGroup;
        Transform _activeGlow;
        Coroutine _transition, _pulse;
        float _suppressClickUntil;

        Action<Color> _textTint, _frameTint, _dotsTint;

        static int PageCount => Mathf.Max(1, Mathf.CeilToInt(SaveStore.LevelCount / (float)PerPage));

        void Awake()
        {
            AutoWire();
            PrevBtn.onClick.AddListener(Prev);
            NextBtn.onClick.AddListener(Next);
            BackBtn.onClick.AddListener(Back);

            _textTint = c =>
            {
                if (SelTitle != null) SelTitle.color = c;
                if (PrevIcon != null) PrevIcon.color = c;
                if (NextIcon != null) NextIcon.color = c;
                if (BackIcon != null) BackIcon.color = c;
            };
            _frameTint = _ =>
            {
                UiFactory.FrameTint(PrevBtn, true);
                UiFactory.FrameTint(NextBtn, true);
                UiFactory.FrameTint(BackBtn, true);
            };
            _dotsTint = _ => RepaintDots();

            // keadaan tombol (spec): tekan turun; alpha nonaktif diatur lewat CanvasGroup
            UiFactory.PressFx(PrevBtn, -12f);
            UiFactory.PressFx(NextBtn, -12f);
            UiFactory.PressFx(BackBtn, -12f);

            // ubin bake scene: klik + geser disambungkan sekali di sini
            if (GridContent != null)
            {
                AttachSwipe(GridContent);
                for (int s = 0; s < GridContent.childCount; s++)
                {
                    var b = (GridContent.GetChild(s) as RectTransform)
                            ?.GetComponentInChildren<Button>(true);
                    if (b == null) continue;
                    int slot = s;
                    b.onClick.AddListener(() => OnTileTap(slot));
                    AttachSwipe(b.transform);
                }
            }
        }

        void OnEnable()
        {
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Accent, _dotsTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _dotsTint, true);

            if (_pulse == null) _pulse = StartCoroutine(PulseActive());
        }

        void OnDisable()
        {
            ThemeApplier.Unsubscribe(_textTint);
            ThemeApplier.Unsubscribe(_frameTint);
            ThemeApplier.Unsubscribe(_dotsTint);

            if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
            if (_transition != null) { StopCoroutine(_transition); _transition = null; }
        }

        /// <summary>Recovers scene refs by child name when serialized wiring fails.</summary>
        void AutoWire()
        {
            var t = transform;
            if (SelTitle == null) SelTitle = UiFactory.Wire<TMP_Text>(this, SelTitle, "SelTitle");
            if (PrevBtn == null) PrevBtn = UiFactory.Wire<Button>(this, null, "PrevBtn");
            if (NextBtn == null) NextBtn = UiFactory.Wire<Button>(this, null, "NextBtn");
            if (BackBtn == null) BackBtn = UiFactory.Wire<Button>(this, null, "BackBtn");
            if (PrevIcon == null) PrevIcon = UiFactory.Wire<Image>(this, null, "PrevBtn/Icon");
            if (NextIcon == null) NextIcon = UiFactory.Wire<Image>(this, null, "NextBtn/Icon");
            if (BackIcon == null) BackIcon = UiFactory.Wire<Image>(this, null, "BackBtn/Icon");
            if (GridContent == null)
            {
                var g = t.Find("Grid");
                if (g != null) GridContent = g.GetComponent<RectTransform>();
            }
            if (GridContent != null && _gridGroup == null)
                _gridGroup = GridContent.GetComponent<CanvasGroup>();
            if (PrevBtn != null && _prevGroup == null)
                _prevGroup = PrevBtn.GetComponent<CanvasGroup>();
            if (NextBtn != null && _nextGroup == null)
                _nextGroup = NextBtn.GetComponent<CanvasGroup>();

            if (_dots == null)
            {
                _dots = t.Find("PageDots") as RectTransform;
                if (_dots != null)
                {
                    _dotImgs = new Image[_dots.childCount];
                    for (int i = 0; i < _dotImgs.Length; i++)
                        _dotImgs[i] = _dots.GetChild(i).GetComponent<Image>();
                }
            }
        }

        // ------------------------------ navigasi halaman ------------------------------

        public void Prev() => GoToPage(-1);
        public void Next() => GoToPage(1);
        public void Back() => AppRoot.Instance.NavBack();

        /// <summary>Panah dan geser memakai transisi yang sama (spec).</summary>
        void GoToPage(int dir)
        {
            int target = Mathf.Clamp(_page + dir, 0, PageCount - 1);
            if (target == _page) return;
            AudioService.Play("tap");
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(PageFlip(target, dir));
        }

        IEnumerator PageFlip(int target, int dir)
        {
            _page = target;
            Populate();
            if (_gridGroup == null || GridContent == null) { _transition = null; yield break; }

            var rt = GridContent;
            float restY = rt.anchoredPosition.y;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Clamp01(t + Time.unscaledDeltaTime / FlipSeconds);
                float e = 1f - Mathf.Pow(1f - t, 3f);          // ease-out
                _gridGroup.alpha = e;
                rt.anchoredPosition = new Vector2(Mathf.Lerp(dir * 48f, 0f, e), restY);
                yield return null;
            }
            _gridGroup.alpha = 1f;
            rt.anchoredPosition = new Vector2(0f, restY);
            _transition = null;
        }

        /// <summary>Buka halaman yang memuat level aktif (spec).</summary>
        public void Show()
        {
            _page = Mathf.Clamp((SaveStore.UnlockedLevel - 1) / PerPage, 0, PageCount - 1);
            if (_transition != null) { StopCoroutine(_transition); _transition = null; }
            if (_gridGroup != null) _gridGroup.alpha = 1f;
            if (GridContent != null)
                GridContent.anchoredPosition = new Vector2(0f, GridContent.anchoredPosition.y);
            Populate();
        }

        void Populate()
        {
            if (GridContent == null) return;

            int start = _page * PerPage + 1;
            for (int s = 0; s < GridContent.childCount; s++)
            {
                var seat = (RectTransform)GridContent.GetChild(s);
                int id = start + s;
                bool show = id >= 1 && id <= SaveStore.LevelCount;
                seat.gameObject.SetActive(show);
                if (show) PaintTile(seat, id);
            }

            _activeGlow = null;
            int slot = SaveStore.UnlockedLevel - start;
            if (slot >= 0 && slot < GridContent.childCount)
            {
                var seat = GridContent.GetChild(slot);
                if (seat.gameObject.activeSelf) _activeGlow = seat.Find("Glow");
            }

            RepaintDots();
            UpdateArrows();
        }

        void UpdateArrows()
        {
            SetArrow(_prevGroup, _page > 0);
            SetArrow(_nextGroup, _page < PageCount - 1);
        }

        void SetArrow(CanvasGroup group, bool on)
        {
            if (group == null) return;
            group.alpha = on ? 1f : ArrowDim;
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        /// <summary>Titik halaman (spec): aktif Aksen, lainnya Tepi.</summary>
        void RepaintDots()
        {
            if (_dotImgs == null) return;
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            var tepi = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            for (int i = 0; i < _dotImgs.Length; i++)
                if (_dotImgs[i] != null) _dotImgs[i].color = i == _page ? accent : tepi;
        }

        void OnTileTap(int slotIdx)
        {
            if (Time.unscaledTime < _suppressClickUntil) return;
            int id = _page * PerPage + slotIdx + 1;
            if (id < 1 || id > SaveStore.LevelCount) return;
            var seat = (RectTransform)GridContent.GetChild(slotIdx);
            if (id > SaveStore.UnlockedLevel)
            {
                AudioService.Play("invalid");
                if (!SaveStore.ReduceMotion) StartCoroutine(Shake(seat));
                return;
            }
            AudioService.Play("tap");
            AppRoot.Instance.StartLevel(id);
        }

        // ------------------------------ geser & denyut ------------------------------

        void AttachSwipe(Transform t)
        {
            if (t == null || t.GetComponent<SwipeRelay>() != null) return;
            t.gameObject.AddComponent<SwipeRelay>().Owner = this;
        }

        public void OnSwipeEnd(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) < SwipeMin || Mathf.Abs(delta.x) <= Mathf.Abs(delta.y)) return;
            _suppressClickUntil = Time.unscaledTime + 0.25f;   // jangan buka ubin setelah geser
            GoToPage(delta.x < 0f ? 1 : -1);                   // geser kiri = halaman berikutnya
        }

        /// <summary>Spec bagian 8: ubin aktif berdenyut — skala 1,0→1,04→1,0
        /// (periode 1,4 dtk) dan alpha Glow 0,3→0,15. Kurangi gerakan = statis.</summary>
        IEnumerator PulseActive()
        {
            const float Period = 1.4f;
            while (true)
            {
                if (SaveStore.ReduceMotion)
                {
                    SetPulse(1f, 0.3f);
                    yield return null;
                    continue;
                }
                float p = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (Mathf.PI * 2f / Period));
                SetPulse(1f + 0.04f * p, Mathf.Lerp(0.3f, 0.15f, p));
                yield return null;
            }
        }

        void SetPulse(float scale, float alpha)
        {
            if (_activeGlow == null) return;
            var img = _activeGlow.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = alpha;
                img.color = c;
            }
            _activeGlow.localScale = new Vector3(scale, scale, 1f);
        }

        // ------------------------------ resep ubin level ------------------------------

        /// <summary>Warnai ubin bake sesuai state (spec bagian 3):
        /// Selesai = akar Tepi + Fill Permukaan + shadow Tepi, angka Teks, bintang
        /// Utama/Tepi. Aktif = akar Aksen + shadow bayangan Aksen + Glow, angka
        /// auto-kontras, bintang kosong Latar. Terkunci = warna Ubin terkunci, gembok.</summary>
        void PaintTile(RectTransform seat, int id)
        {
            var btnT = seat.Find("Btn");
            if (btnT == null) return;
            var rootImg = btnT.GetComponent<Image>();
            var shadow = btnT.GetComponent<Shadow>();
            var glow = seat.Find("Glow");
            var fill = btnT.Find("Fill");
            var fillImg = fill != null ? fill.GetComponent<Image>() : null;
            var content = btnT.Find("Content");
            var numT = content != null ? content.Find("num") : btnT.Find("num");
            var rowT = content != null ? content.Find("stars") : btnT.Find("stars");
            var num = numT != null ? numT.GetComponent<TMP_Text>() : null;
            var numGo = numT != null ? numT.gameObject : null;
            var rowGo = rowT != null ? rowT.gameObject : null;
            var lockT = btnT.Find("lock");
            var lockGo = lockT != null ? lockT.gameObject : null;
            var lockImg = lockT != null ? lockT.GetComponent<Image>() : null;

            bool locked = id > SaveStore.UnlockedLevel;
            bool active = id == SaveStore.UnlockedLevel;

            if (glow != null)
            {
                glow.gameObject.SetActive(active);
                if (active) glow.localScale = Vector3.one;
            }
            if (lockGo != null) lockGo.SetActive(locked);
            if (fill != null) fill.gameObject.SetActive(!locked && !active);
            if (content != null) content.gameObject.SetActive(!locked);
            if (numGo != null) numGo.SetActive(!locked);
            if (rowGo != null) rowGo.SetActive(!locked);
            if (shadow != null) shadow.enabled = !locked;
            if (lockImg != null) lockImg.color = ThemeApplier.ColorOf(ThemeApplier.Role.Lock);
            if (num != null) num.text = id.ToString();

            if (locked)
            {
                rootImg.color = ThemeApplier.ColorOf(ThemeApplier.Role.Locked);
                return;
            }

            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            rootImg.color = active ? accent : ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            if (shadow != null)
                shadow.effectColor = active ? ThemeApplier.ColorOf(ThemeApplier.Role.ShadowAccent)
                    : ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            if (fillImg != null) fillImg.color = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            if (num != null)
                num.color = active ? ThemeApplier.ContrastTextOn(accent)
                    : ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            if (rowGo != null)
                UiFactory.SetStars(rowGo.transform, active ? 0 : SaveStore.BestStars(id),
                    ThemeApplier.ColorOf(ThemeApplier.Role.Primary),
                    active ? ThemeApplier.ColorOf(ThemeApplier.Role.Background)
                           : ThemeApplier.ColorOf(ThemeApplier.Role.GridLine));
        }

        /// <summary>Spec bagian 8: tap level terkunci menggetarkan ubin ±8 px,
        /// tiga ayunan dalam 200 ms.</summary>
        IEnumerator Shake(RectTransform rt)
        {
            var base0 = rt.anchoredPosition;
            const float Wobble = 8f;
            const float Step = 0.2f / 6f;
            for (int i = 0; i < 6; i++)
            {
                rt.anchoredPosition = base0 + new Vector2(i % 2 == 0 ? Wobble : -Wobble, 0f);
                yield return new WaitForSecondsRealtime(Step);
            }
            rt.anchoredPosition = base0;
        }

        /// <summary>Meneruskan drag dari area kosong kisi / ubin ke layar.</summary>
        sealed class SwipeRelay : MonoBehaviour, IBeginDragHandler, IEndDragHandler
        {
            public LevelSelectScreen Owner;
            Vector2 _start;

            public void OnBeginDrag(PointerEventData eventData) => _start = eventData.position;

            public void OnEndDrag(PointerEventData eventData)
            {
                if (Owner != null) Owner.OnSwipeEnd(eventData.position - _start);
            }
        }
    }
}
