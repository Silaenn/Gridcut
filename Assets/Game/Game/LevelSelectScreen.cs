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

        void Awake()
        {
            PrevBtn.onClick.AddListener(Prev);
            NextBtn.onClick.AddListener(Next);
            BackBtn.onClick.AddListener(Back);
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, c => PageLabel.color = c, false);
        }

        public void Prev() { _page = Mathf.Max(0, _page - 1); Populate(); AudioService.Play("tap"); }
        public void Next()
        {
            int pages = Mathf.CeilToInt(SaveStore.LevelCount / (float)PerPage);
            _page = Mathf.Min(pages - 1, _page + 1);
            Populate();
            AudioService.Play("tap");
        }
        public void Back() => AppRoot.Instance.OpenHome();

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
                var b = UiFactory.Button($"L{id}", GridContent, id.ToString(), 28, null,
                    Vector2.zero, unlocked
                        ? ThemeApplier.ColorOf(ThemeApplier.Role.Accent)
                        : new Color(0.45f, 0.45f, 0.45f, 0.55f));
                var label = b.GetComponentInChildren<TMP_Text>(true);
                label.text = unlocked ? id.ToString() : "\u2716";

                var stars = UiFactory.Text(b.transform, "stars", StarString(id), 15,
                    ThemeApplier.ColorOf(ThemeApplier.Role.Text), TMPro.TextAlignmentOptions.Center,
                    new Vector2(90, 22));
                var srt = stars.rectTransform;
                srt.anchorMin = new Vector2(0, 0.02f);
                srt.anchorMax = new Vector2(1, 0.28f);
                srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

                if (unlocked)
                    b.onClick.AddListener(() =>
                    {
                        AudioService.Play("tap");
                        AppRoot.Instance.StartLevel(levelId);
                    });
            }
        }

        string StarString(int id)
        {
            int s = SaveStore.BestStars(id);
            string r = "";
            for (int i = 0; i < 3; i++) r += i < s ? "\u2605" : "\u2606";
            return r;
        }
    }
}
