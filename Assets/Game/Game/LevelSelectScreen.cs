using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GridCut.Game
{
    /// <summary>
    /// Level select (scene component). Paged 45-button grid populated at runtime from
    /// SaveStore progress; static chrome (page label, arrows, back) is wired in scene.
    /// </summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public TMP_Text PageLabel;
        public TMP_Text SelTitle;           // header "Pilih Level"
        public Button PrevBtn, NextBtn, BackBtn;
        public RectTransform GridContent;   // GridLayoutGroup

        int _page;
        const int PerPage = 15;             // spec: kisi 5 x 3
        Action<Color> _textTint;

        void Awake()
        {
            AutoWire();
            PrevBtn.onClick.AddListener(Prev);
            NextBtn.onClick.AddListener(Next);
            BackBtn.onClick.AddListener(Back);
            _textTint = c =>
            {
                if (PageLabel != null) PageLabel.color = c;
                if (SelTitle != null) SelTitle.color = c;
            };
        }

        void OnEnable()
        {
            // applyImmediately re-tints PageLabel from the current theme on appearance
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
        }

        void OnDisable() => ThemeApplier.Unsubscribe(_textTint);

        /// <summary>Recovers scene refs by child name when serialized wiring fails.</summary>
        void AutoWire()
        {
            var t = transform;
            if (PageLabel == null)
            {
                var p = t.Find("PageLabel");
                if (p != null) PageLabel = p.GetComponentInChildren<TMP_Text>(true);
            }
            if (SelTitle == null) SelTitle = UiFactory.Wire<TMP_Text>(this, SelTitle, "SelTitle");
            if (PrevBtn == null) PrevBtn = UiFactory.Wire<Button>(this, null, "PrevBtn");
            if (NextBtn == null) NextBtn = UiFactory.Wire<Button>(this, null, "NextBtn");
            if (BackBtn == null) BackBtn = UiFactory.Wire<Button>(this, null, "BackBtn");
            if (GridContent == null)
            {
                var g = t.Find("Grid");
                if (g != null) GridContent = g.GetComponent<RectTransform>();
            }
        }

        public void Prev() { _page = Mathf.Max(0, _page - 1); Populate(); AudioService.Play("tap"); }
        public void Next()
        {
            int pages = Mathf.CeilToInt(SaveStore.LevelCount / (float)PerPage);
            _page = Mathf.Min(pages - 1, _page + 1);
            Populate();
            AudioService.Play("tap");
        }
        public void Back() => AppRoot.Instance.NavBack();

        public void Show()
        {
            _page = (SaveStore.UnlockedLevel - 1) / PerPage;
            Populate();
        }

        void Populate()
        {
            for (int i = GridContent.childCount - 1; i >= 0; i--)
                Destroy(GridContent.GetChild(i).gameObject);

            int start = _page * PerPage + 1;
            int end = Mathf.Min(start + PerPage - 1, SaveStore.LevelCount);
            int pages = Mathf.CeilToInt(SaveStore.LevelCount / (float)PerPage);
            PageLabel.text = $"{_page + 1} / {pages}";

            for (int id = start; id <= end; id++)
            {
                int levelId = id;
                bool unlocked = id <= SaveStore.UnlockedLevel;
                var b = UiFactory.Button($"L{id}", GridContent, id.ToString(), 97f, null,
                    new Vector2(265f, 265f), unlocked
                        ? ThemeApplier.ColorOf(ThemeApplier.Role.Accent)
                        : new Color(0.45f, 0.45f, 0.45f, 0.4f));

                // stars as sprite images — LiberationSans SDF has no ★/☆ glyphs
                var row = UiFactory.StarRow(b.transform, "stars");
                row.anchorMin = new Vector2(0f, 0.02f);
                row.anchorMax = new Vector2(1f, 0.28f);
                row.offsetMin = Vector2.zero; row.offsetMax = Vector2.zero;
                // sprite bintang Kenney putih-alpha: penuh = putih, kosong = diredam warna
                UiFactory.SetStars(row, SaveStore.BestStars(id), Color.white, new Color(1f, 1f, 1f, 0.24f));

                if (unlocked)
                {
                    b.onClick.AddListener(() =>
                    {
                        AudioService.Play("tap");
                        AppRoot.Instance.StartLevel(levelId);
                    });
                    continue;
                }

                // terkunci: nomor disembunyikan, ikon gembok di tengah (spec: redup + padlock)
                var label = b.GetComponentInChildren<TMP_Text>(true);
                label.gameObject.SetActive(false);
                var lockImg = UiFactory.Image(b.transform, "lock", new Color(0.85f, 0.85f, 0.85f, 0.9f));
                lockImg.sprite = AppRoot.Instance.Padlock;   // PNG asset, di-wire di AppRoot scene
                lockImg.preserveAspect = true;
                var lrt = (RectTransform)lockImg.transform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(64f, 64f);   // spec: gembok tampil 64x64 di ubin
                lrt.anchoredPosition = new Vector2(0f, 22f);

                var cellRt = (RectTransform)b.transform;
                int levelRef = id;
                b.onClick.AddListener(() =>
                {
                    AudioService.Play("invalid");
                    StartCoroutine(Shake(cellRt));
                });
            }
        }

        /// <summary>Spec bagian 8: tap level terkunci menggetarkan tombol.</summary>
        IEnumerator Shake(RectTransform rt)
        {
            var base0 = rt.anchoredPosition;
            const float Wobble = 7f, Step = 0.035f;
            for (int i = 0; i < 6; i++)
            {
                rt.anchoredPosition = base0 + new Vector2(i % 2 == 0 ? Wobble : -Wobble, 0f);
                yield return new WaitForSeconds(Step);
            }
            rt.anchoredPosition = base0;
        }
    }
}
