using System;
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
        public Button PrevBtn, NextBtn, BackBtn;
        public RectTransform GridContent;   // GridLayoutGroup

        int _page;
        const int PerPage = 45;
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
            PageLabel.text = $"Level {start}\u2013{end}";

            for (int id = start; id <= end; id++)
            {
                int levelId = id;
                bool unlocked = id <= SaveStore.UnlockedLevel;
                var b = UiFactory.Button($"L{id}", GridContent, id.ToString(), 38, null,
                    Vector2.zero, unlocked
                        ? ThemeApplier.ColorOf(ThemeApplier.Role.Accent)
                        : new Color(0.45f, 0.45f, 0.45f, 0.4f));
                var label = b.GetComponentInChildren<TMP_Text>(true);
                label.text = unlocked ? id.ToString() : "X";

                // stars as sprite images — LiberationSans SDF has no ★/☆ glyphs
                var row = UiFactory.StarRow(b.transform, "stars");
                row.anchorMin = new Vector2(0f, 0.02f);
                row.anchorMax = new Vector2(1f, 0.28f);
                row.offsetMin = Vector2.zero; row.offsetMax = Vector2.zero;
                UiFactory.SetStars(row, SaveStore.BestStars(id),
                    ThemeApplier.ColorOf(ThemeApplier.Role.Text), new Color(1f, 1f, 1f, 0.28f));

                if (unlocked)
                    b.onClick.AddListener(() =>
                    {
                        AudioService.Play("tap");
                        AppRoot.Instance.StartLevel(levelId);
                    });
            }
        }
    }
}
