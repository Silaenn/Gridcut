using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GridCut.Game
{
    /// <summary>
    /// Main menu screen (scene component; hierarchy and references are wired in the
    /// scene). Vertical layout per spec bag. 5: logo, primary Continue, secondary
    /// Levels/Tema row; Tema opens the swatch popup, the top-right icon pair covers
    /// mute and Pengaturan (sound + reset).
    /// </summary>
    public class HomeScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public Button ContinueBtn, SelectBtn, SoundBtn, ResetBtn;   // ResetBtn = tombol Settings (gear)
        public Image ContinueIcon;         // ikon play 56×56 di dalam ContinueBtn
        public Button ThemeBtn;            // tombol sekunder baris bawah, membuka kartu tema
        public Button[] ThemeSwatches;     // kartu tematik Theme0..4 (spec: 5 kartu horizontal; diadaptasi 332×465)
        public GameObject ConfirmDialog;
        public Button ConfirmYes, ConfirmNo;

        [Header("Scene-baked popups (dibake di Main.unity, bukan runtime)")]
        public GameObject ThemePopup;      // Dim + Card + 5 swatch + Tutup
        public GameObject SettingsPopup;   // Dim + Card + Suara / Reset Progres / Tutup
        public Button ThemeCloseBtn, SettingsSoundBtn, SettingsResetBtn, SettingsCloseBtn;
        public Image MuteIcon, MuteIconOff, GearIcon, ThemeCard, SettingsCard;   // sprite ikon Kenney di Assets/Game/UI
        public TMP_Text ThemeTitle, SettingsTitle;

        Action<Color> _textTint, _popupTint, _primaryTint, _frameTint;
        RectTransform _logoRow;             // huruf logo GRIDCUT (dibake runtime)
        Image[] _logoTiles;                 // [i] = ubin huruf ke-i
        Action<Color>[] _logoTints;         // langganan warna keping per huruf (Rect i%6)

        void Awake()
        {
            AutoWire();

            ContinueBtn.onClick.AddListener(StartContinue);
            SelectBtn.onClick.AddListener(OpenLevelSelect);
            SoundBtn.onClick.AddListener(ToggleSound);
            ResetBtn.onClick.AddListener(OpenSettings);          // Pengaturan hub; reset progres live there
            if (ThemeBtn != null) ThemeBtn.onClick.AddListener(ToggleThemePopup);
            if (ThemeCloseBtn != null) ThemeCloseBtn.onClick.AddListener(ToggleThemePopup);
            if (SettingsSoundBtn != null) SettingsSoundBtn.onClick.AddListener(ToggleSound);
            if (SettingsResetBtn != null) SettingsResetBtn.onClick.AddListener(OnSettingsReset);
            if (SettingsCloseBtn != null) SettingsCloseBtn.onClick.AddListener(CloseSettings);
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
                Tint("SelectBtn", SelectBtn, c);
                Tint("ThemeBtn", ThemeBtn, c);
                if (MuteIcon != null) MuteIcon.color = c;
                if (MuteIconOff != null) MuteIconOff.color = c;
                if (GearIcon != null) GearIcon.color = c;
                if (ThemeTitle != null) ThemeTitle.color = c;
                if (SettingsTitle != null) SettingsTitle.color = c;
                // dialog reset: label mengikuti pola menu (teks gelap pada tombol aksen)
                Tint("ConfirmYes", ConfirmYes, c);
                Tint("ConfirmNo", ConfirmNo, c);
            };
            _popupTint = c =>
            {
                if (ThemeCard != null) ThemeCard.color = c;
                if (SettingsCard != null) SettingsCard.color = c;
            };
            _primaryTint = _ =>
            {
                UiFactory.PrimaryTint(ContinueBtn, -16f);
                // ikon play mengikuti kontras label (bag. 4: teks auto-kontras di Utama)
                if (ContinueIcon != null)
                    ContinueIcon.color = ThemeApplier.ContrastTextOn(
                        ThemeApplier.ColorOf(ThemeApplier.Role.Primary));
            };
            _frameTint = _ =>
            {
                UiFactory.FrameTint(SelectBtn, false);   // sekunder (Levels)
                UiFactory.FrameTint(ThemeBtn, false);    // sekunder (Tema)
                UiFactory.FrameTint(SoundBtn, true);     // ikon bulat
                UiFactory.FrameTint(ResetBtn, true);     // gear = pengaturan
            };
            // keadaan tombol (spec): tekan-turun + alpha nonaktif
            UiFactory.PressFx(ContinueBtn, -16f);
            UiFactory.PressFx(SelectBtn, -12f);
            UiFactory.PressFx(ThemeBtn, -12f);
            UiFactory.PressFx(SoundBtn, -12f);
            UiFactory.PressFx(ResetBtn, -12f);           // gear → pengaturan
        }

        void OnEnable()
        {
            RefreshLabels();
            RefreshThemeTint();
            if (_logoRow == null) BuildLogo();   // sprite Rounded belum siap saat Awake → coba lagi
            // applyImmediately: re-tint from the current theme on every appearance
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Primary, _primaryTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _popupTint, true);
            if (_logoTints != null)
                for (int i = 0; i < _logoTints.Length; i++)
                    ThemeApplier.Subscribe(ThemeApplier.Role.Rect, _logoTints[i], true, i % 6);
        }

        void OnDisable()
        {
            ThemeApplier.Unsubscribe(_textTint);
            ThemeApplier.Unsubscribe(_popupTint);
            ThemeApplier.Unsubscribe(_primaryTint);
            ThemeApplier.Unsubscribe(_frameTint);
            if (_logoTints != null)
                foreach (var cb in _logoTints) ThemeApplier.Unsubscribe(cb);
            // never revive an overlay when Home reappears
            if (ThemePopup != null) ThemePopup.SetActive(false);
            if (SettingsPopup != null) SettingsPopup.SetActive(false);
            // ConfirmReset kini di root Overlay (spec) — tidak ikut mati bersama Home
            if (ConfirmDialog != null && ConfirmDialog.activeSelf) ConfirmDialog.SetActive(false);
        }

        /// <summary>Recovers scene refs by child name when serialized wiring fails.</summary>
        void AutoWire()
        {
            if (ContinueBtn == null) ContinueBtn = UiFactory.Wire<Button>(this, null, "ContinueBtn");
            if (ContinueIcon == null) ContinueIcon = UiFactory.Wire<Image>(this, ContinueIcon, "ContinueBtn/Icon");
            if (SelectBtn == null) SelectBtn = UiFactory.Wire<Button>(this, null, "SelectBtn");
            if (ThemeBtn == null) ThemeBtn = UiFactory.Wire<Button>(this, null, "ThemeBtn");
            if (SoundBtn == null) SoundBtn = UiFactory.Wire<Button>(this, null, "SoundBtn");
            if (ResetBtn == null) ResetBtn = UiFactory.Wire<Button>(this, null, "ResetBtn");
            if (ThemePopup == null) ThemePopup = UiFactory.WireGo(this, ThemePopup, "ThemePopup");
            if (SettingsPopup == null) SettingsPopup = UiFactory.WireGo(this, SettingsPopup, "SettingsPopup");
            if (ThemeCloseBtn == null) ThemeCloseBtn = UiFactory.Wire<Button>(this, ThemeCloseBtn, "ThemePopup/CloseBtn");
            if (SettingsSoundBtn == null) SettingsSoundBtn = UiFactory.Wire<Button>(this, SettingsSoundBtn, "SettingsPopup/SetSoundBtn");
            if (SettingsResetBtn == null) SettingsResetBtn = UiFactory.Wire<Button>(this, SettingsResetBtn, "SettingsPopup/SetResetBtn");
            if (SettingsCloseBtn == null) SettingsCloseBtn = UiFactory.Wire<Button>(this, SettingsCloseBtn, "SettingsPopup/SetCloseBtn");
            if (MuteIcon == null) MuteIcon = UiFactory.Wire<Image>(this, MuteIcon, "SoundBtn/Icon");
            if (MuteIconOff == null) MuteIconOff = UiFactory.Wire<Image>(this, MuteIconOff, "SoundBtn/IconOff");
            if (GearIcon == null) GearIcon = UiFactory.Wire<Image>(this, GearIcon, "ResetBtn/Icon");
            if (ThemeCard == null) ThemeCard = UiFactory.Wire<Image>(this, ThemeCard, "ThemePopup/Card");
            if (SettingsCard == null) SettingsCard = UiFactory.Wire<Image>(this, SettingsCard, "SettingsPopup/Card");
            if (ThemeTitle == null) ThemeTitle = UiFactory.Wire<TMP_Text>(this, ThemeTitle, "ThemePopup/Card/CardTitle");
            if (SettingsTitle == null) SettingsTitle = UiFactory.Wire<TMP_Text>(this, SettingsTitle, "SettingsPopup/Card/CardTitle");
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
            if (cont != null)
            {
                int level = SaveStore.UnlockedLevel;
                if (level <= 1) cont.text = "Play";
                else cont.text = $"Lanjutkan \u00b7 Level {level}";
            }
        }

        public void ToggleSound()
        {
            SaveStore.SetSound(!SaveStore.SoundOn);
            RefreshLabels();
            RefreshMuteIcon();
            RefreshSettingsLabels();
            AudioService.Play("tap");
        }

        // ------------------ top-right icon pair (spec bag. 5) ------------------
        // GO "Icon" sudah dibake di scene lengkap dengan sprite PNG; kode hanya
        // menukar sprite Mute saat state suara berubah.

        void RefreshMuteIcon()
        {
            // dua Image scene-baked (speak-on speak-off); kode hanya memilih yang tampil
            if (MuteIcon != null) MuteIcon.enabled = SaveStore.SoundOn;
            if (MuteIconOff != null) MuteIconOff.enabled = !SaveStore.SoundOn;
        }

        // ------------------------------ theme popup ------------------------------

        /// <summary>Tema: home button membuka/menutup dialog berisi kelima swatch.</summary>
        void ToggleThemePopup()
        {
            if (ThemePopup == null) return;
            bool show = !ThemePopup.activeSelf;
            ThemePopup.SetActive(show);
            if (show) RefreshThemeTint();   // status/nama/kotak warna segar tiap dibuka
            AudioService.Play("tap");
        }

        // --------------------------- settings popup ---------------------------

        void OpenSettings()
        {
            if (SettingsPopup == null) return;
            RefreshSettingsLabels();
            SettingsPopup.SetActive(true);
            AudioService.Play("tap");
        }

        void CloseSettings()
        {
            SettingsPopup.SetActive(false);
            AudioService.Play("tap");
        }

        /// <summary>Pengaturan → Reset Progres: tutup popup lalu tampilkan dialog konfirmasi.</summary>
        void OnSettingsReset()
        {
            SettingsPopup.SetActive(false);
            ShowResetConfirm();
        }

        void RefreshSettingsLabels()
        {
            var lab = SettingsSoundBtn != null ? LabelOf(SettingsSoundBtn) : null;
            if (lab != null) lab.text = SaveStore.SoundOn ? "Suara: nyala" : "Suara: mati";
        }

        public void StartContinue() => AppRoot.Instance.StartLevel(SaveStore.UnlockedLevel);

        public void OpenLevelSelect() => AppRoot.Instance.OpenLevelSelect();

        public void ShowResetConfirm() => ConfirmDialog.SetActive(true);
        public void HideResetConfirm() => ConfirmDialog.SetActive(false);

        public void ConfirmReset()
        {
            SaveStore.ResetProgress();
            // a theme that is now locked falls back to the default Paper theme
            // (strict: tema terbuka hanya bila level N sudah SELESAI → UnlockedLevel > N)
            if (SaveStore.ThemeIndex > 0 && SaveStore.UnlockedLevel <= ThemeLibrary.All[SaveStore.ThemeIndex].UnlockLevel)
                ThemeApplier.Apply(0);
            HideResetConfirm();
            RefreshLabels();
            RefreshThemeTint();
            AudioService.Play("tap");
        }

        public void SelectTheme(int index)
        {
            var th = ThemeLibrary.All[index];
            bool unlocked = SaveStore.UnlockedLevel > th.UnlockLevel || th.UnlockLevel <= 1;
            var btn = index < ThemeSwatches.Length ? ThemeSwatches[index] : null;
            if (!unlocked)
            {
                // spec: tema terkunci tidak bisa dipilih — tap hanya menggetarkan kartu
                AudioService.Play("invalid");
                if (btn != null) StartCoroutine(ShakeCard(btn.GetComponent<RectTransform>()));
                return;
            }
            ThemeApplier.Apply(index);
            RefreshThemeTint();
            AudioService.Play("unlock");
            if (ThemePopup != null && ThemePopup.activeSelf) ThemePopup.SetActive(false);
        }

        // pola getar level select: ±7 px, 6 langkah, 35 ms
        IEnumerator ShakeCard(RectTransform rt)
        {
            const float amp = 7f, step = 0.035f;
            var basePos = rt != null ? rt.anchoredPosition : Vector2.zero;
            for (int k = 0; k < 6; k++)
            {
                if (rt != null) rt.anchoredPosition = basePos + new Vector2((k % 2 == 0 ? -1f : 1f) * amp, 0f);
                yield return new WaitForSecondsRealtime(step);
            }
            if (rt != null) rt.anchoredPosition = basePos;
        }

        // ------------------------------ huruf logo (spec) ------------------------------

        /// <summary>Logo GRIDCUT: tujuh ubin 120×144 (radius 28 → ×2,0) warna keping
        /// ke-i, bayangan (0,-10) warna tepi keping, huruf TMP Bold 88 auto-kontras,
        /// rotasi bergantian −3/+3°, jarak antar huruf 16. Seluruh huruf di-bake di
        /// scene (MenuGroup/LogoLetters); kode hanya menyambung warna tema.</summary>
        void BuildLogo()
        {
            var host = transform.Find("MenuGroup");
            var rowT = host != null ? host.Find("LogoLetters") : null;
            if (rowT == null)
            {
                Debug.LogWarning("[HomeScreen] 'MenuGroup/LogoLetters' belum dibake di scene");
                return;
            }

            _logoRow = (RectTransform)rowT;
            _logoTiles = new Image[7];
            _logoTints = new Action<Color>[7];
            for (int i = 0; i < 7; i++)
            {
                var t = _logoRow.Find($"letter{i}");
                _logoTiles[i] = t != null ? t.GetComponent<Image>() : null;
                int li = i;
                _logoTints[i] = _ => LogoTint(li);
            }
        }

        /// <summary>Warna ubin huruf ke-i mengikuti tema: warna keping i (Rect i%6),
        /// bayangan = Tepi keping i (tabel bag. 4), huruf auto-kontras di atasnya.</summary>
        void LogoTint(int i)
        {
            if (_logoTiles == null || i >= _logoTiles.Length || _logoTiles[i] == null) return;
            var c = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, i % 6);
            _logoTiles[i].color = c;
            UiFactory.ApplyShadow(_logoTiles[i], new Vector2(0f, -10f),
                ThemeApplier.ColorOf(ThemeApplier.Role.RectEdge, i % 6));
            var glyph = _logoTiles[i].GetComponentInChildren<TMP_Text>(true);
            if (glyph != null) glyph.color = ThemeApplier.ContrastTextOn(c);
        }

        public void Show()
        {
            RefreshLabels();
            RefreshMuteIcon();      // sound may have been toggled on another screen
            RefreshThemeTint();
        }

        void RefreshThemeTint()
        {
            for (int i = 0; i < ThemeSwatches.Length && i < ThemeLibrary.All.Length; i++)
            {
                var card = ThemeSwatches[i];
                if (card == null)
                {
                    Debug.LogWarning($"[HomeScreen] ThemeSwatches[{i}] belum ter-wire di scene");
                    continue;
                }
                var th = ThemeLibrary.All[i];
                bool unlocked = SaveStore.UnlockedLevel > th.UnlockLevel || th.UnlockLevel <= 1;
                // 5 kotak warna pratinjau per kartu
                for (int s = 0; s < 5; s++)
                {
                    var swT = card.transform.Find("Swatch" + s);
                    var img = swT != null ? swT.GetComponent<Image>() : null;
                    if (img != null)
                    {
                        var c = ThemeApplier.Parse(th.Rects[Mathf.Min(s, th.Rects.Length - 1)]);
                        c.a = unlocked ? 1f : 0.35f;
                        img.color = c;
                    }
                }
                var nameT = card.transform.Find("Name");
                var lab = nameT != null ? nameT.GetComponent<TMP_Text>() : null;
                if (lab != null)
                {
                    lab.text = th.Name;
                    var c = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
                    c.a = unlocked ? 1f : 0.4f;
                    lab.color = c;
                }
                // status: Dipakai / Pilih / Buka di level N
                var stT = card.transform.Find("Status");
                var st = stT != null ? stT.GetComponent<TMP_Text>() : null;
                if (st != null)
                {
                    st.text = SaveStore.ThemeIndex == i ? "Dipakai"
                        : unlocked ? "Pilih" : $"Selesaikan level {th.UnlockLevel}";
                    var c = ThemeApplier.ColorOf(SaveStore.ThemeIndex == i || unlocked
                        ? ThemeApplier.Role.Accent : ThemeApplier.Role.Text);
                    c.a = unlocked || SaveStore.ThemeIndex == i ? 1f : 0.5f;
                    st.color = c;
                }
                // kartu tetap interaktif agar tap kartu terkunci memicu getar (spec)
            }
        }
    }
}
