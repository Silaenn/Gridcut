using System;
using System.Collections.Generic;
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
        public Button ContinueBtn, SelectBtn, SoundBtn, ResetBtn, ThemesHeader;
        public Button[] ThemeSwatches;     // one per ThemeLibrary.All entry
        public GameObject ConfirmDialog;
        public Button ConfirmYes, ConfirmNo;

        Action<Color> _textTint, _accentTint;

        void Awake()
        {
            AutoWire();

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

            // theme colors follow the active theme at all times (null-safe: a mis-wired
            // scene logs a warning instead of throwing inside the theme callback)
            _textTint = c =>
            {
                if (Subtitle != null) Subtitle.color = c;
                Tint("ContinueBtn", ContinueBtn, c);
                Tint("SelectBtn", SelectBtn, c);
                Tint("SoundBtn", SoundBtn, c);
                Tint("ResetBtn", ResetBtn, c);
            };
            _accentTint = c => Tint("ConfirmYes", ConfirmYes, c);
        }

        void OnEnable()
        {
            RefreshLabels();
            RefreshThemeTint();
            // applyImmediately: re-tint from the current theme on every appearance
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Accent, _accentTint, true);
        }

        void OnDisable()
        {
            ThemeApplier.Unsubscribe(_textTint);
            ThemeApplier.Unsubscribe(_accentTint);
        }

        /// <summary>Recovers scene refs by child name when serialized wiring fails.</summary>
        void AutoWire()
        {
            var t = transform;
            if (Subtitle == null)
            {
                var sub = t.Find("Sub");
                if (sub != null) Subtitle = sub.GetComponentInChildren<TMP_Text>(true);
            }
            if (ContinueBtn == null) ContinueBtn = UiFactory.Wire<Button>(this, null, "ContinueBtn");
            if (SelectBtn == null) SelectBtn = UiFactory.Wire<Button>(this, null, "SelectBtn");
            if (SoundBtn == null) SoundBtn = UiFactory.Wire<Button>(this, null, "SoundBtn");
            if (ResetBtn == null) ResetBtn = UiFactory.Wire<Button>(this, null, "ResetBtn");
            if (ConfirmDialog == null) ConfirmDialog = UiFactory.WireGo(this, null, "ConfirmReset");
            if (ConfirmYes == null) ConfirmYes = UiFactory.Wire<Button>(this, null, "ConfirmReset/Yes");
            if (ConfirmNo == null) ConfirmNo = UiFactory.Wire<Button>(this, null, "ConfirmReset/No");
            if (ThemeSwatches == null || ThemeSwatches.Length == 0)
            {
                var list = new List<Button>();
                for (int i = 0; i < ThemeLibrary.All.Length; i++)
                {
                    var b = UiFactory.Wire<Button>(this, null, $"Theme{i}");
                    if (b != null) list.Add(b);
                }
                ThemeSwatches = list.ToArray();
            }
        }

        static TMP_Text LabelOf(Button b) =>
            b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;

        static void Tint(string name, Button b, Color c)
        {
            if (b == null)
            {
                Debug.LogWarning($"[HomeScreen] Referensi Button '{name}' belum ter-wire di scene");
                return;
            }
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t == null)
            {
                Debug.LogWarning($"[HomeScreen] Tidak ada TMP label di bawah '{b.name}'");
                return;
            }
            t.color = c;
        }

        public void RefreshLabels()
        {
            var cont = LabelOf(ContinueBtn);
            if (cont != null) cont.text = $"Lanjutkan \u00b7 {SaveStore.UnlockedLevel}";
            var snd = LabelOf(SoundBtn);
            if (snd != null) snd.text = SaveStore.SoundOn ? "Suara: nyala" : "Suara: mati";
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
            // a theme that is now locked falls back to the default Paper theme
            if (SaveStore.ThemeIndex > 0 && SaveStore.UnlockedLevel < ThemeLibrary.All[SaveStore.ThemeIndex].UnlockLevel)
                ThemeApplier.Apply(0);
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
                var sw = ThemeSwatches[i];
                if (sw == null)
                {
                    Debug.LogWarning($"[HomeScreen] ThemeSwatches[{i}] belum ter-wire di scene");
                    continue;
                }
                var th = ThemeLibrary.All[i];
                bool unlocked = SaveStore.UnlockedLevel >= th.UnlockLevel || th.UnlockLevel <= 1;
                var img = sw.GetComponent<Image>();
                var themeColor = ThemeApplier.Parse(th.Rects[0]);
                if (img != null)
                {
                    themeColor.a = unlocked ? 1f : 0.4f;
                    img.color = themeColor;
                }
                var lab = LabelOf(sw);
                if (lab != null) lab.text = unlocked ? th.Name : $"{th.Name} L{th.UnlockLevel}";
                sw.interactable = unlocked;
            }
        }
    }
}
