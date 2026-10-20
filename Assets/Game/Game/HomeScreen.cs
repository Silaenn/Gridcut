using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GridCut.Game
{
    /// <summary>
    /// Main menu screen (scene component; hierarchy and references are wired in the
    /// scene). Continue/level pick/sound/reset; theme swatch row selects one of the
    /// five themes, locked until the unlock level is reached.
    /// </summary>
    public class HomeScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public TMP_Text Subtitle;
        public Button ContinueBtn, SelectBtn, SoundBtn, ResetBtn;
        public Button[] ThemeSwatches;     // one per ThemeLibrary.All entry
        public GameObject ConfirmDialog;
        public Button ConfirmYes, ConfirmNo;

        void OnEnable()
        {
            RefreshLabels();
            RefreshThemeTint();
        }

        void Awake()
        {
            ContinueBtn.onClick.AddListener(StartContinue);
            SelectBtn.onClick.AddListener(OpenLevelSelect);
            SoundBtn.onClick.AddListener(ToggleSound);
            ResetBtn.onClick.AddListener(ShowResetConfirm);
            ConfirmYes.onClick.AddListener(ConfirmReset);
            ConfirmNo.onClick.AddListener(HideResetConfirm);
            for (int i = 0; i < ThemeSwatches.Length; i++)
            {
                int idx = i;
                ThemeSwatches[i].onClick.AddListener(() => SelectTheme(idx));
            }

            // theme colors follow the active theme at all times
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, c =>
            {
                if (Subtitle != null) Subtitle.color = c;
                LabelOf(ContinueBtn).color = c;
                LabelOf(SelectBtn).color = c;
                LabelOf(SoundBtn).color = c;
                LabelOf(ResetBtn).color = c;
            }, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.Accent, c =>
            {
                LabelOf(ConfirmYes).color = c;
            }, false);
        }

        static TMP_Text LabelOf(Button b) => b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;

        public void RefreshLabels()
        {
            LabelOf(ContinueBtn).text = $"Lanjutkan \u00b7 {SaveStore.UnlockedLevel}";
            LabelOf(SoundBtn).text = SaveStore.SoundOn ? "Suara: nyala" : "Suara: mati";
        }

        public void ToggleSound()
        {
            SaveStore.SetSound(!SaveStore.SoundOn);
            RefreshLabels();
            AudioService.Play("tap");
        }

        public void StartContinue() => AppRoot.Instance.StartLevel(SaveStore.UnlockedLevel);

        public void OpenLevelSelect() => AppRoot.Instance.OpenLevelSelect();

        public void ShowResetConfirm() => ConfirmDialog.SetActive(true);
        public void HideResetConfirm() => ConfirmDialog.SetActive(false);

        public void ConfirmReset()
        {
            SaveStore.ResetProgress();
            HideResetConfirm();
            RefreshLabels();
            RefreshThemeTint();
            AudioService.Play("tap");
        }

        public void SelectTheme(int index)
        {
            var th = ThemeLibrary.All[index];
            bool unlocked = SaveStore.UnlockedLevel >= th.UnlockLevel || th.UnlockLevel <= 1;
            if (!unlocked)
            {
                AudioService.Play("invalid");
                return;
            }
            ThemeApplier.Apply(index);
            RefreshThemeTint();
            AudioService.Play("unlock");
        }

        public void Show()
        {
            RefreshLabels();
            RefreshThemeTint();
        }

        void RefreshThemeTint()
        {
            for (int i = 0; i < ThemeSwatches.Length; i++)
            {
                var th = ThemeLibrary.All[i];
                bool unlocked = SaveStore.UnlockedLevel >= th.UnlockLevel || th.UnlockLevel <= 1;
                var img = ThemeSwatches[i].GetComponent<Image>();
                img.color = unlocked ? ThemeApplier.Parse(th.Rects[0])
                                     : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                LabelOf(ThemeSwatches[i]).text = unlocked ? th.Name : $"{th.Name} L{th.UnlockLevel}";
                ThemeSwatches[i].interactable = unlocked;
            }
        }
    }
}
