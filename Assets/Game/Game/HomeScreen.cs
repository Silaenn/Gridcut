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
        public Image ContinueIcon;         // ikon play 80×80 di dalam ContinueBtn
        public Button ThemeBtn;            // tombol sekunder baris bawah, membuka kartu tema
        public Button[] ThemeSwatches;     // kartu tematik Theme0..4 (spec: 5 kartu horizontal; diadaptasi 332×465)
        public GameObject ConfirmDialog;
        public Button ConfirmYes, ConfirmNo;

        [Header("Scene-baked popups (dibake di Main.unity, bukan runtime)")]
        public GameObject ThemePopup;      // Overlay: Backdrop + Panel
        public GameObject SettingsPopup;   // Overlay: Backdrop + Panel
        public Button ThemeCloseBtn, SettingsResetBtn, SettingsCloseBtn, ReplayTutorialBtn;
        public Slider MusicSlider, SfxSlider;
        public Toggle ReduceMotionToggle;
        public RectTransform ReduceMotionKnob;
        public Image ReduceMotionTrack;
        public TMP_Text MusicLabel, SfxLabel, MotionLabel;
        public Image MuteIcon, MuteIconOff, GearIcon, ThemeCard, SettingsCard;
        public Image ConfirmCard;
        public TMP_Text ThemeTitle, SettingsTitle, ConfirmQuestion;
        public Image ConfirmWarning, ConfirmMarkBar, ConfirmMarkDot;
        public GameObject ResetToast;
        public Image ThemeNewDot;          // titik Aksen di tombol Themes (opsional bag. 10)

        Action<Color> _textTint, _popupTint, _primaryTint, _frameTint, _accentTint;
        RectTransform _logoRow;             // huruf logo GRIDCUT (dibake runtime)
        Image[] _logoTiles;                 // [i] = ubin huruf ke-i
        Action<Color>[] _logoTints;         // langganan warna keping per huruf (Rect i%6)

        // spec bagian 8: intro logo + tombol hanya sekali per sesi
        static bool _introPlayed;
        Coroutine _introCo;
        Coroutine _lockedHint;
        Coroutine _toastCo;
        bool _themeRepaintPending;
        RectTransform[] _introLogos;
        Vector2[] _introLogoBase;
        Button[] _introBtns;
        CanvasGroup[] _introBtnGroups;
        float[] _introBtnBaseY;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetIntroSession() => _introPlayed = false;

        void Awake()
        {
            AutoWire();

            ContinueBtn.onClick.AddListener(StartContinue);
            SelectBtn.onClick.AddListener(OpenLevelSelect);
            SoundBtn.onClick.AddListener(ToggleSound);
            ResetBtn.onClick.AddListener(OpenSettings);          // Pengaturan hub; reset progres live there
            if (ThemeBtn != null) ThemeBtn.onClick.AddListener(ToggleThemePopup);
            if (ThemeCloseBtn != null) ThemeCloseBtn.onClick.AddListener(ToggleThemePopup);
            if (SettingsResetBtn != null) SettingsResetBtn.onClick.AddListener(OnSettingsReset);
            if (SettingsCloseBtn != null) SettingsCloseBtn.onClick.AddListener(CloseSettings);
            if (ReplayTutorialBtn != null) ReplayTutorialBtn.onClick.AddListener(ReplayTutorial);
            if (MusicSlider != null)
            {
                MusicSlider.wholeNumbers = false;
                MusicSlider.minValue = 0f;
                MusicSlider.maxValue = 1f;
                MusicSlider.onValueChanged.AddListener(v => AudioService.SetMusicPreview(v));
                AddSliderCommit(MusicSlider, v =>
                {
                    SaveStore.SetMusicVolume(v);
                    AudioService.SetMusicPreview(v);
                });
            }
            if (SfxSlider != null)
            {
                SfxSlider.wholeNumbers = false;
                SfxSlider.minValue = 0f;
                SfxSlider.maxValue = 1f;
                SfxSlider.onValueChanged.AddListener(v => AudioService.SetSfxPreview(v));
                AddSliderCommit(SfxSlider, v =>
                {
                    SaveStore.SetSfxVolume(v);
                    AudioService.SetSfxPreview(v);
                    AudioService.Play("tap");   // contoh level SFX setelah dilepas
                });
            }
            if (ReduceMotionToggle != null)
                ReduceMotionToggle.onValueChanged.AddListener(OnReduceMotionChanged);
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
                if (ConfirmQuestion != null) ConfirmQuestion.color = c;
                if (MusicLabel != null) MusicLabel.color = c;
                if (SfxLabel != null) SfxLabel.color = c;
                if (MotionLabel != null) MotionLabel.color = c;
                Tint("ReplayTutorialBtn", ReplayTutorialBtn, c);
                Tint("SettingsResetBtn", SettingsResetBtn, c);
                // label dialog konfirmasi diatur StyleConfirm (auto-kontras)
            };
            _popupTint = _ =>
            {
                // spec bagian 9: akar Tepi radius 56 + Fill Permukaan radius 50 + shadow (0,-20)
                if (ThemeCard != null) UiFactory.CardTint(ThemeCard, -20f);
                if (SettingsCard != null) UiFactory.CardTint(SettingsCard, -20f);
                if (ConfirmCard != null) UiFactory.CardTint(ConfirmCard, -20f);
            };
            _primaryTint = _ =>
            {
                UiFactory.PrimaryTint(ContinueBtn, -16f);
                // ikon play mengikuti kontras label (bag. 4: teks auto-kontras di Utama)
                if (ContinueIcon != null)
                    ContinueIcon.color = ThemeApplier.ContrastTextOn(
                        ThemeApplier.ColorOf(ThemeApplier.Role.Primary));
                StyleSettings();
            };
            _frameTint = _ =>
            {
                UiFactory.FrameTint(SelectBtn, false);   // sekunder (Levels)
                UiFactory.FrameTint(ThemeBtn, false);    // sekunder (Tema)
                UiFactory.FrameTint(SoundBtn, true);     // ikon bulat
                UiFactory.FrameTint(ResetBtn, true);     // gear = pengaturan
                UiFactory.FrameTint(ThemeCloseBtn, true);
                UiFactory.FrameTint(SettingsCloseBtn, true);
                StyleSettings();
                StyleConfirm();
            };
            _accentTint = c =>
            {
                if (ThemeNewDot != null) ThemeNewDot.color = c;
                StyleSettings();
                StyleConfirm();
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
            // intro dimulai dari Show() setelah SaveStore.Load (opsi Kurangi gerakan terbaca)
            // applyImmediately: re-tint from the current theme on every appearance
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Primary, _primaryTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _popupTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Accent, _accentTint, true);
            if (_logoTints != null)
                for (int i = 0; i < _logoTints.Length; i++)
                    ThemeApplier.Subscribe(ThemeApplier.Role.Rect, _logoTints[i], true, i % 6);
        }

        void OnDisable()
        {
            if (_introCo != null) { StopCoroutine(_introCo); _introCo = null; }
            if (_lockedHint != null) { StopCoroutine(_lockedHint); _lockedHint = null; }
            if (_knobCo != null) { StopCoroutine(_knobCo); _knobCo = null; }
            if (_toastCo != null) { StopCoroutine(_toastCo); _toastCo = null; }
            if (ResetToast != null) ResetToast.SetActive(false);
            SetIntroFinal();   // jangan tinggalkan alpha/posisi di tengah animasi
            ThemeApplier.Unsubscribe(_textTint);
            ThemeApplier.Unsubscribe(_popupTint);
            ThemeApplier.Unsubscribe(_primaryTint);
            ThemeApplier.Unsubscribe(_frameTint);
            ThemeApplier.Unsubscribe(_accentTint);
            if (_logoTints != null)
                foreach (var cb in _logoTints) ThemeApplier.Unsubscribe(cb);
            // never revive an overlay when Home reappears
            HidePopupNow(ThemePopup);
            HidePopupNow(SettingsPopup);
            HidePopupNow(ConfirmDialog);
        }

        /// <summary>Repaint kartu tema selama transisi 300 ms agar cangkang, pil,
        /// dan pratinjau ikut berubah mulus saat tema diganti dari popup.</summary>
        void Update()
        {
            if (ThemePopup != null && ThemePopup.activeSelf && ThemeApplier.IsTransitioning)
                RefreshThemeTint();
            if (_themeRepaintPending && !ThemeApplier.IsTransitioning)
            {
                _themeRepaintPending = false;
                RefreshThemeTint();
            }
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
            if (ThemePopup == null) ThemePopup = RootGo("Overlay/ThemePopup");
            if (SettingsPopup == null) SettingsPopup = RootGo("Overlay/SettingsPopup");
            if (ThemeNewDot == null && ThemeBtn != null)
            {
                var d = ThemeBtn.transform.Find("ThemeNewDot");
                if (d != null) ThemeNewDot = d.GetComponent<Image>();
            }
            if (ThemeCloseBtn == null) ThemeCloseBtn = WireRoot<Button>("Overlay/ThemePopup/Card/CloseBtn");
            if (SettingsResetBtn == null) SettingsResetBtn = WireRoot<Button>("Overlay/SettingsPopup/Card/BottomRow/ResetSeat/SetResetBtn");
            if (SettingsCloseBtn == null) SettingsCloseBtn = WireRoot<Button>("Overlay/SettingsPopup/Card/SetCloseBtn");
            if (ReplayTutorialBtn == null) ReplayTutorialBtn = WireRoot<Button>("Overlay/SettingsPopup/Card/BottomRow/ReplaySeat/ReplayBtn");
            if (MusicSlider == null) MusicSlider = WireRoot<Slider>("Overlay/SettingsPopup/Card/Rows/MusicRow/MusicSlider");
            if (SfxSlider == null) SfxSlider = WireRoot<Slider>("Overlay/SettingsPopup/Card/Rows/SfxRow/SfxSlider");
            if (ReduceMotionToggle == null) ReduceMotionToggle = WireRoot<Toggle>("Overlay/SettingsPopup/Card/Rows/MotionRow");
            if (ReduceMotionKnob == null) ReduceMotionKnob = WireRoot<RectTransform>("Overlay/SettingsPopup/Card/Rows/MotionRow/Track/Knob");
            if (ReduceMotionTrack == null) ReduceMotionTrack = WireRoot<Image>("Overlay/SettingsPopup/Card/Rows/MotionRow/Track");
            if (MusicLabel == null) MusicLabel = WireRoot<TMP_Text>("Overlay/SettingsPopup/Card/Rows/MusicRow/Label");
            if (SfxLabel == null) SfxLabel = WireRoot<TMP_Text>("Overlay/SettingsPopup/Card/Rows/SfxRow/Label");
            if (MotionLabel == null) MotionLabel = WireRoot<TMP_Text>("Overlay/SettingsPopup/Card/Rows/MotionRow/Label");
            if (MuteIcon == null) MuteIcon = UiFactory.Wire<Image>(this, MuteIcon, "SoundBtn/Icon");
            if (MuteIconOff == null) MuteIconOff = UiFactory.Wire<Image>(this, MuteIconOff, "SoundBtn/IconOff");
            if (GearIcon == null) GearIcon = UiFactory.Wire<Image>(this, GearIcon, "ResetBtn/Icon");
            if (ThemeCard == null) ThemeCard = WireRoot<Image>("Overlay/ThemePopup/Card");
            if (SettingsCard == null) SettingsCard = WireRoot<Image>("Overlay/SettingsPopup/Card");
            if (ConfirmDialog == null) ConfirmDialog = UiFactory.WireGo(this, null, "ConfirmReset");
            if (ConfirmDialog == null) ConfirmDialog = RootGo("Overlay/ConfirmReset");
            if (ConfirmCard == null) ConfirmCard = WireRoot<Image>("Overlay/ConfirmReset/Card");
            if (ConfirmCard == null && ConfirmDialog != null)
            {
                var c = ConfirmDialog.transform.Find("Card");
                if (c != null) ConfirmCard = c.GetComponent<Image>();
            }
            if (ConfirmQuestion == null) ConfirmQuestion = WireRoot<TMP_Text>("Overlay/ConfirmReset/Card/Question");
            if (ConfirmWarning == null) ConfirmWarning = WireRoot<Image>("Overlay/ConfirmReset/Card/WarningIcon");
            if (ConfirmMarkBar == null) ConfirmMarkBar = WireRoot<Image>("Overlay/ConfirmReset/Card/WarningIcon/Mark/Bar");
            if (ConfirmMarkDot == null) ConfirmMarkDot = WireRoot<Image>("Overlay/ConfirmReset/Card/WarningIcon/Mark/Dot");
            if (ResetToast == null) ResetToast = RootGo("Overlay/ResetToast");
            if (ConfirmQuestion == null && ConfirmDialog != null)
            {
                var q = ConfirmDialog.transform.Find("Card/Question");
                if (q != null) ConfirmQuestion = q.GetComponent<TMP_Text>();
            }
            if (ThemeTitle == null) ThemeTitle = WireRoot<TMP_Text>("Overlay/ThemePopup/Card/CardTitle");
            if (SettingsTitle == null) SettingsTitle = WireRoot<TMP_Text>("Overlay/SettingsPopup/Card/CardTitle");
            if (ConfirmDialog == null) ConfirmDialog = UiFactory.WireGo(this, null, "ConfirmReset");
            if (ConfirmYes == null) ConfirmYes = WireRoot<Button>("Overlay/ConfirmReset/Card/Yes");
            if (ConfirmNo == null) ConfirmNo = WireRoot<Button>("Overlay/ConfirmReset/Card/No");
            if (ThemeSwatches == null || ThemeSwatches.Length == 0)
            {
                var list = new List<Button>();
                for (int i = 0; i < ThemeLibrary.All.Length; i++)
                {
                    var b = WireRoot<Button>($"Overlay/ThemePopup/Card/CardsRow/Theme{i}");
                    if (b != null) list.Add(b);
                }
                ThemeSwatches = list.ToArray();
            }
        }

        static TMP_Text LabelOf(Button b) =>
            b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;

        GameObject RootGo(string path)
        {
            var t = transform.root.Find(path);
            return t != null ? t.gameObject : null;
        }

        T WireRoot<T>(string path) where T : Component
        {
            var t = transform.root.Find(path);
            return t != null ? t.GetComponentInChildren<T>(true) : null;
        }

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
                else cont.text = $"Continue \u00b7 Level {level}";
                cont.ForceMeshUpdate();
            }
            FitContinueWidth(cont);
        }

        /// <summary>Lebar tombol Play/Continue mengikuti isi: ikon + teks + padding,
        /// supaya "Continue · Level 150" tidak terpotong.</summary>
        void FitContinueWidth(TMP_Text label)
        {
            if (ContinueBtn == null) return;
            var btn = (RectTransform)ContinueBtn.transform;
            var seat = btn.parent as RectTransform;
            var icon = btn.Find("Icon") as RectTransform;
            var hlg = btn.GetComponent<HorizontalLayoutGroup>();
            float iconW = 80f;
            if (icon != null)
            {
                var le = icon.GetComponent<LayoutElement>();
                iconW = le != null ? le.preferredWidth : icon.rect.width;
            }
            float spacing = hlg != null ? hlg.spacing : 20f;
            float textW = label != null ? Mathf.Max(0f, label.preferredWidth) : 0f;
            float width = Mathf.Clamp(iconW + spacing + textW + 112f, 400f, 800f);
            btn.sizeDelta = new Vector2(width, btn.sizeDelta.y);
            if (seat != null) seat.sizeDelta = new Vector2(width, seat.sizeDelta.y);
        }

        public void ToggleSound()
        {
            SaveStore.SetSound(!SaveStore.SoundOn);
            RefreshLabels();
            RefreshMuteIcon();
            RefreshSettingsLabels();
            AudioService.RefreshMusicVolume();
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

        /// <summary>Tema: home button membuka/menutup popup berisi kelima swatch.</summary>
        void ToggleThemePopup()
        {
            var p = PopupOf(ThemePopup);
            if (p == null) return;
            if (p.IsOpen)
            {
                p.Hide();
            }
            else
            {
                p.Show();
                RefreshThemeTint();           // setelah Show: CanvasGroup kartu sudah Awake
                SaveStore.MarkThemesSeen();   // titik tema baru hilang setelah popup dibuka
                RefreshThemeDot();
            }
            AudioService.Play("tap");
        }

        // --------------------------- settings popup ---------------------------

        void OpenSettings()
        {
            var p = PopupOf(SettingsPopup);
            if (p == null) return;
            RefreshSettingsLabels();
            p.Show();
            AudioService.Play("tap");
        }

        void CloseSettings()
        {
            // jaring pengaman bila popup ditutup saat slider masih di-drag
            if (MusicSlider != null) SaveStore.SetMusicVolume(MusicSlider.value);
            if (SfxSlider != null) SaveStore.SetSfxVolume(SfxSlider.value);
            var p = PopupOf(SettingsPopup);
            if (p != null) p.Hide();
            AudioService.Play("tap");
        }

        /// <summary>Replay tutorial: tutup Pengaturan lalu buka Level 1 (tutorial).
        /// Tidak menyentuh progres/bintang yang sudah ada.</summary>
        void ReplayTutorial()
        {
            var p = PopupOf(SettingsPopup);
            if (p != null) p.HideImmediate();
            AudioService.Play("tap");
            AppRoot.Instance.StartTutorial();
        }

        /// <summary>Pengaturan → Reset Progress: Pengaturan tetap terbuka di belakang,
        /// Konfirmasi muncul sebagai popup di atasnya (dua backdrop bertumpuk).</summary>
        void OnSettingsReset()
        {
            ShowResetConfirm();
        }

        static PopupView PopupOf(GameObject go) =>
            go != null ? go.GetComponent<PopupView>() : null;

        void HidePopupNow(GameObject go)
        {
            var p = PopupOf(go);
            if (p != null) p.HideImmediate();
            else if (go != null) go.SetActive(false);
        }

        void RefreshSettingsLabels()
        {
            if (MusicSlider != null) MusicSlider.SetValueWithoutNotify(SaveStore.MusicVolume);
            if (SfxSlider != null) SfxSlider.SetValueWithoutNotify(SaveStore.SfxVolume);
            if (ReduceMotionToggle != null)
                ReduceMotionToggle.SetIsOnWithoutNotify(SaveStore.ReduceMotion);
            SetKnob(SaveStore.ReduceMotion, true);
            AudioService.SetMusicPreview(SaveStore.MusicVolume);
            AudioService.SetSfxPreview(SaveStore.SfxVolume);
            StyleSettings();
        }

        void OnReduceMotionChanged(bool on)
        {
            SaveStore.SetReduceMotion(on);
            SetKnob(on, false);
            if (on && _introCo != null)
            {
                StopCoroutine(_introCo);
                _introCo = null;
                SetIntroFinal();
            }
            StyleSettings();
            AudioService.Play("tap");
        }

        void SetKnob(bool on, bool instant)
        {
            if (ReduceMotionKnob == null) return;
            float target = on ? 30f : -30f;   // track 136, kenop 60, margin 8
            if (_knobCo != null) { StopCoroutine(_knobCo); _knobCo = null; }
            if (instant)
            {
                var p = ReduceMotionKnob.anchoredPosition;
                p.x = target;
                ReduceMotionKnob.anchoredPosition = p;
                return;
            }
            _knobCo = StartCoroutine(KnobCo(target));
        }

        Coroutine _knobCo;

        IEnumerator KnobCo(float target)
        {
            float from = ReduceMotionKnob.anchoredPosition.x;
            const float dur = 0.15f;
            for (float t = 0f; t < dur;)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                var p = ReduceMotionKnob.anchoredPosition;
                p.x = Mathf.Lerp(from, target, k * k * (3f - 2f * k));
                ReduceMotionKnob.anchoredPosition = p;
                yield return null;
            }
            var q = ReduceMotionKnob.anchoredPosition;
            q.x = target;
            ReduceMotionKnob.anchoredPosition = q;
            _knobCo = null;
        }

        static void AddSliderCommit(Slider s, System.Action<float> commit)
        {
            var c = s.GetComponent<SliderCommit>();
            if (c == null) c = s.gameObject.AddComponent<SliderCommit>();
            c.Slider = s;
            c.Commit = commit;
        }

        /// <summary>Slider/toggle + tombol bawah mengikuti peran warna tema.</summary>
        void StyleSettings()
        {
            var edge = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            var surface = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            var primary = ThemeApplier.ColorOf(ThemeApplier.Role.Primary);
            var shadowPrimary = ThemeApplier.ColorOf(ThemeApplier.Role.ShadowPrimary);
            var locked = ThemeApplier.ColorOf(ThemeApplier.Role.Lock);

            StyleSlider(MusicSlider, edge, primary, surface, shadowPrimary);
            StyleSlider(SfxSlider, edge, primary, surface, shadowPrimary);

            if (ReduceMotionTrack != null)
                ReduceMotionTrack.color = SaveStore.ReduceMotion ? primary : edge;
            if (ReduceMotionKnob != null)
            {
                var img = ReduceMotionKnob.GetComponent<Image>();
                if (img != null) img.color = surface;
                var sh = ReduceMotionKnob.GetComponent<Shadow>();
                if (sh != null)
                {
                    sh.effectColor = locked;
                    sh.effectDistance = new Vector2(0f, -4f);
                }
            }

            UiFactory.FrameTint(ReplayTutorialBtn, false);
            UiFactory.FrameTint(SettingsResetBtn, true);   // ikon sampah ikut warna Teks
            if (SettingsResetBtn != null)
            {
                var trash = SettingsResetBtn.transform.Find("Icon");
                if (trash != null)
                    foreach (var im in trash.GetComponentsInChildren<Image>(true))
                        im.color = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            }
        }

        static void StyleSlider(Slider s, Color edge, Color primary, Color surface, Color shadowPrimary)
        {
            if (s == null) return;
            var track = s.transform.Find("Track");
            var trackImg = track != null ? track.GetComponent<Image>() : null;
            if (trackImg != null) trackImg.color = edge;
            if (s.fillRect != null)
            {
                var fill = s.fillRect.GetComponent<Image>();
                if (fill != null) fill.color = primary;
            }
            if (s.handleRect != null)
            {
                var handle = s.handleRect.GetComponent<Image>();
                if (handle != null) handle.color = shadowPrimary;
                var inner = s.handleRect.Find("Fill");
                var innerImg = inner != null ? inner.GetComponent<Image>() : null;
                if (innerImg != null) innerImg.color = surface;
            }
        }

        /// <summary>Konfirmasi: ikon peringatan Aksen; Reset = Aksen (label auto-kontras),
        /// Cancel = sekunder. Label dialog diatur di sini (bukan _textTint).</summary>
        void StyleConfirm()
        {
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            var contrast = ThemeApplier.ContrastTextOn(accent);
            if (ConfirmWarning != null)
            {
                ConfirmWarning.color = accent;
                UiFactory.ApplyShadow(ConfirmWarning, new Vector2(0f, -12f),
                    ThemeApplier.ColorOf(ThemeApplier.Role.ShadowAccent));
            }
            if (ConfirmMarkBar != null) ConfirmMarkBar.color = contrast;
            if (ConfirmMarkDot != null) ConfirmMarkDot.color = contrast;

            if (ConfirmYes != null)
            {
                var img = ConfirmYes.GetComponent<Image>();
                if (img != null) img.color = accent;
                UiFactory.ApplyShadow(ConfirmYes, new Vector2(0f, -16f),
                    ThemeApplier.ColorOf(ThemeApplier.Role.ShadowAccent));
                var fill = ConfirmYes.transform.Find("Fill");
                if (fill != null) fill.gameObject.SetActive(false);
                var lab = LabelOf(ConfirmYes);
                if (lab != null) lab.color = contrast;
            }
            if (ConfirmNo != null)
            {
                UiFactory.FrameTint(ConfirmNo, false);
                var fill = ConfirmNo.transform.Find("Fill");
                if (fill != null) fill.gameObject.SetActive(true);
                var lab = LabelOf(ConfirmNo);
                if (lab != null) lab.color = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            }
        }

        // ------------------------ intro menu (spec bagian 8) ------------------------

        void PlayIntro()
        {
            if (_logoRow == null) return;

            // Posisi huruf di-bake eksplisit di scene (tanpa HorizontalLayoutGroup)
            // supaya animasi tidak bergantung pada layout pass pertama.
            int n = _logoRow.childCount;
            _introLogos = new RectTransform[n];
            _introLogoBase = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                _introLogos[i] = (RectTransform)_logoRow.GetChild(i);
                _introLogoBase[i] = _introLogos[i].anchoredPosition;
            }

            _introBtns = new[] { ContinueBtn, SelectBtn, ThemeBtn };
            int m = _introBtns.Length;
            _introBtnGroups = new CanvasGroup[m];
            _introBtnBaseY = new float[m];
            for (int i = 0; i < m; i++)
            {
                var b = _introBtns[i];
                if (b == null) continue;
                var rt = (RectTransform)b.transform;
                _introBtnBaseY[i] = rt.anchoredPosition.y;
                var fx = b.GetComponent<ButtonFX>();
                if (fx != null) fx.RebaseRest();   // posisi istirahat = sebelum intro
                var g = b.GetComponent<CanvasGroup>();
                if (g == null) g = b.gameObject.AddComponent<CanvasGroup>();
                _introBtnGroups[i] = g;
            }

            if (_introPlayed || SaveStore.ReduceMotion)
            {
                _introPlayed = true;
                SetIntroFinal();
                return;
            }
            _introPlayed = true;

            if (_introCo != null) StopCoroutine(_introCo);
            _introCo = StartCoroutine(IntroCo());
        }

        IEnumerator IntroCo()
        {
            const float LogoDur = 0.35f;
            const float LogoStagger = 0.06f;
            const float LogoTotal = LogoDur + LogoStagger * 6f;
            const float BtnDur = 0.25f;
            const float BtnStagger = 0.08f;
            const float BtnTotal = BtnDur + BtnStagger * 2f;

            for (int i = 0; i < _introLogos.Length; i++)
            {
                var rt = _introLogos[i];
                if (rt == null) continue;
                rt.anchoredPosition = _introLogoBase[i] + new Vector2(0f, 40f);
                rt.localScale = new Vector3(0f, 0f, 1f);
            }
            for (int i = 0; i < _introBtns.Length; i++)
            {
                var rt = _introBtns[i] != null ? (RectTransform)_introBtns[i].transform : null;
                if (rt == null) continue;
                var p = rt.anchoredPosition;
                p.y = _introBtnBaseY[i] - 40f;
                rt.anchoredPosition = p;
                if (_introBtnGroups[i] != null) _introBtnGroups[i].alpha = 0f;
            }

            float elapsed = 0f;
            while (elapsed < LogoTotal)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < _introLogos.Length; i++)
                {
                    var rt = _introLogos[i];
                    if (rt == null) continue;
                    float local = elapsed - i * LogoStagger;
                    float k = Mathf.Clamp01(local / LogoDur);
                    rt.anchoredPosition = _introLogoBase[i] + new Vector2(0f, 40f * (1f - EaseOutBack(k)));
                    float s = k < 0.7f
                        ? Mathf.Lerp(0f, 1.15f, EaseOutCubic(k / 0.7f))
                        : Mathf.Lerp(1.15f, 1f, EaseOutCubic((k - 0.7f) / 0.3f));
                    rt.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }

            float bt = 0f;
            while (bt < BtnTotal)
            {
                bt += Time.unscaledDeltaTime;
                for (int i = 0; i < _introBtns.Length; i++)
                {
                    var b = _introBtns[i];
                    if (b == null) continue;
                    var rt = (RectTransform)b.transform;
                    float local = bt - i * BtnStagger;
                    float k = Mathf.Clamp01(local / BtnDur);
                    var p = rt.anchoredPosition;
                    p.y = Mathf.Lerp(_introBtnBaseY[i] - 40f, _introBtnBaseY[i], EaseOutCubic(k));
                    rt.anchoredPosition = p;
                    if (_introBtnGroups[i] != null) _introBtnGroups[i].alpha = k;
                }
                yield return null;
            }

            SetIntroFinal();
            _introCo = null;
        }

        void SetIntroFinal()
        {
            if (_introLogos != null && _introLogoBase != null)
                for (int i = 0; i < _introLogos.Length; i++)
                {
                    var rt = _introLogos[i];
                    if (rt == null) continue;
                    rt.anchoredPosition = _introLogoBase[i];
                    rt.localScale = Vector3.one;
                }
            if (_introBtns != null && _introBtnBaseY != null)
                for (int i = 0; i < _introBtns.Length; i++)
                {
                    var b = _introBtns[i];
                    if (b == null) continue;
                    var rt = (RectTransform)b.transform;
                    var p = rt.anchoredPosition;
                    p.y = _introBtnBaseY[i];
                    rt.anchoredPosition = p;
                    if (_introBtnGroups[i] != null) _introBtnGroups[i].alpha = 1f;
                    var fx = b.GetComponent<ButtonFX>();
                    if (fx != null) fx.RebaseRest();
                }
        }

        static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = t - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        public void StartContinue() => AppRoot.Instance.StartContinue();

        public void OpenLevelSelect() => AppRoot.Instance.OpenLevelSelect();

        public void ShowResetConfirm()
        {
            var p = PopupOf(ConfirmDialog);
            if (p == null) return;
            if (ConfirmYes != null) ConfirmYes.interactable = true;
            if (ConfirmNo != null) ConfirmNo.interactable = true;
            p.Show();
            StartCoroutine(SelectCancelNextFrame());
        }

        IEnumerator SelectCancelNextFrame()
        {
            yield return null;
            if (ConfirmNo != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(ConfirmNo.gameObject);
        }

        public void HideResetConfirm()
        {
            PopupOf(ConfirmDialog)?.Hide();
            ClearConfirmSelection();
        }

        void ClearConfirmSelection()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            var sel = es.currentSelectedGameObject;
            if (sel != null && ConfirmDialog != null && sel.transform.IsChildOf(ConfirmDialog.transform))
                es.SetSelectedGameObject(null);
        }

        public void ConfirmReset()
        {
            // cegah tekan ganda seketika
            if (ConfirmYes != null) ConfirmYes.interactable = false;
            if (ConfirmNo != null) ConfirmNo.interactable = false;

            SaveStore.ResetProgress();   // level, bintang, state main, tema terbuka, tanda tutorial
            if (SaveStore.ThemeIndex != 0)
            {
                ThemeApplier.Apply(0);   // kembali ke Paper; audio/motion tidak disentuh
                _themeRepaintPending = true;
            }

            HidePopupNow(ConfirmDialog);
            HidePopupNow(SettingsPopup);   // kedua popup tertutup

            RefreshLabels();               // tombol utama kembali "Play"
            RefreshThemeTint();
            RefreshThemeDot();
            RefreshSettingsLabels();

            if (ConfirmYes != null) ConfirmYes.interactable = true;
            if (ConfirmNo != null) ConfirmNo.interactable = true;
            ClearConfirmSelection();
            ShowResetToast();
            AudioService.Play("tap");
        }

        void ShowResetToast()
        {
            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ResetToastCo());
        }

        IEnumerator ResetToastCo()
        {
            if (ResetToast == null) { _toastCo = null; yield break; }
            ResetToast.SetActive(true);
            var cg = ResetToast.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.5f);
            ResetToast.SetActive(false);
            _toastCo = null;
        }

        public void SelectTheme(int index)
        {
            if (_lockedHint != null) { StopCoroutine(_lockedHint); _lockedHint = null; }
            var th = ThemeLibrary.All[index];
            bool unlocked = SaveStore.UnlockedLevel > th.UnlockLevel || th.UnlockLevel <= 1;
            var btn = index < ThemeSwatches.Length ? ThemeSwatches[index] : null;
            if (!unlocked)
            {
                AudioService.Play("invalid");
                if (btn != null)
                {
                    if (!SaveStore.ReduceMotion)
                        StartCoroutine(ShakeCard(btn.GetComponent<RectTransform>()));
                    int more = Mathf.Max(1, th.UnlockLevel - SaveStore.UnlockedLevel + 1);
                    _lockedHint = StartCoroutine(LockedPillHint(index, more));
                }
                return;
            }
            // spec bagian 10: tema langsung diterapkan, popup tetap terbuka
            ThemeApplier.Apply(index);
            RefreshThemeTint();
            _themeRepaintPending = true;
            AudioService.Play("tap");
        }

        /// <summary>Opsional spek: pill terkunci sesaat menampilkan sisa level.</summary>
        IEnumerator LockedPillHint(int index, int more)
        {
            var card = index < ThemeSwatches.Length ? ThemeSwatches[index] : null;
            var pillT = card != null ? card.transform.Find("StatusPill") : null;
            var labT = pillT != null ? pillT.Find("Label") : null;
            var lab = labT != null ? labT.GetComponent<TMP_Text>() : null;
            if (lab != null)
            {
                lab.text = $"{more} more levels";
                var prt = pillT as RectTransform;
                if (prt != null)
                    prt.sizeDelta = new Vector2(
                        Mathf.Max(120f, lab.GetPreferredValues(lab.text, 0f, 0f).x + 56f), 68f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            _lockedHint = null;
            RefreshThemeTint();
        }

        // getar kartu terkunci: ±8 px, tiga ayunan dalam 200 ms (spec bagian 8/10)
        IEnumerator ShakeCard(RectTransform rt)
        {
            const float amp = 8f;
            const float step = 0.2f / 6f;
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
            RefreshThemeDot();
            PlayIntro();            // spec bagian 8: hanya pembukaan pertama per sesi
        }

        void RefreshThemeDot()
        {
            if (ThemeNewDot != null) ThemeNewDot.gameObject.SetActive(SaveStore.HasUnseenTheme());
        }

        void RefreshThemeTint()
        {
            var edge = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            var surface = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            var primary = ThemeApplier.ColorOf(ThemeApplier.Role.Primary);
            var text = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            var muted = ThemeApplier.ColorOf(ThemeApplier.Role.TextMuted);
            var lockedFill = ThemeApplier.ColorOf(ThemeApplier.Role.Locked);

            for (int i = 0; i < ThemeSwatches.Length && i < ThemeLibrary.All.Length; i++)
            {
                var card = ThemeSwatches[i];
                if (card == null)
                {
                    Debug.LogWarning($"[HomeScreen] ThemeSwatches[{i}] belum ter-wire di scene");
                    continue;
                }
                var th = ThemeLibrary.All[i];
                bool inUse = SaveStore.ThemeIndex == i;
                bool unlocked = SaveStore.UnlockedLevel > th.UnlockLevel || th.UnlockLevel <= 1;
                bool locked = !unlocked;

                var rootImg = card.GetComponent<Image>();
                var shadow = card.GetComponent<Shadow>();
                var group = card.GetComponent<CanvasGroup>();
                if (rootImg != null) rootImg.color = inUse ? accent : edge;
                if (shadow != null)
                {
                    shadow.effectColor = inUse
                        ? ThemeApplier.ColorOf(ThemeApplier.Role.ShadowAccent) : edge;
                    shadow.effectDistance = new Vector2(0f, -10f);
                }
                if (group != null) group.alpha = locked ? 0.75f : 1f;

                var fillT = card.transform.Find("Fill");
                var fillImg = fillT != null ? fillT.GetComponent<Image>() : null;
                if (fillImg != null) fillImg.color = surface;

                var previewT = card.transform.Find("Fill/Content/Preview");
                var preview = previewT != null ? previewT.GetComponent<Image>() : null;
                if (preview != null) preview.color = ThemeApplier.Parse(th.Background);

                for (int p = 0; p < 6; p++)
                {
                    var cellT = previewT != null ? previewT.Find("Piece" + p) : null;
                    var cell = cellT != null ? cellT.GetComponent<Image>() : null;
                    if (cell == null) continue;
                    var c = ThemeApplier.Parse(th.Rects[Mathf.Min(p, th.Rects.Length - 1)]);
                    c.a = locked ? 0.45f : 1f;
                    cell.color = c;
                }

                var lockT = previewT != null ? previewT.Find("Lock") : null;
                if (lockT != null)
                {
                    lockT.gameObject.SetActive(locked);
                    var lockImg = lockT.GetComponent<Image>();
                    if (lockImg != null)
                    {
                        // kontras terhadap latar pratinjau tema itu sendiri
                        lockImg.color = ThemeApplier.ContrastTextOn(
                            ThemeApplier.Parse(th.Background));
                    }
                }

                var nameT = card.transform.Find("Fill/Content/Name");
                var nameLab = nameT != null ? nameT.GetComponent<TMP_Text>() : null;
                if (nameLab != null)
                {
                    nameLab.text = th.Name;
                    nameLab.color = text;
                }

                var pillT = card.transform.Find("StatusPill");
                var pillImg = pillT != null ? pillT.GetComponent<Image>() : null;
                var pillLabT = pillT != null ? pillT.Find("Label") : null;
                var pillLab = pillLabT != null ? pillLabT.GetComponent<TMP_Text>() : null;
                string status;
                Color pillColor, labelColor;
                if (inUse)
                {
                    status = "In use";
                    pillColor = accent;
                    labelColor = ThemeApplier.ContrastTextOn(accent);
                }
                else if (unlocked)
                {
                    status = "Select";
                    pillColor = primary;
                    labelColor = ThemeApplier.ContrastTextOn(primary);
                }
                else
                {
                    status = $"Level {th.UnlockLevel}";
                    pillColor = lockedFill;
                    labelColor = muted;
                }
                if (pillImg != null) pillImg.color = pillColor;
                if (pillLab != null)
                {
                    pillLab.text = status;
                    pillLab.color = labelColor;
                    float w = Mathf.Max(120f, pillLab.GetPreferredValues(status, 0f, 0f).x + 56f);
                    var prt = pillT as RectTransform;
                    if (prt != null) prt.sizeDelta = new Vector2(w, 68f);
                }
                // kartu tetap interaktif agar tap kartu terkunci memicu getar (spec)
            }
        }
    }
}
