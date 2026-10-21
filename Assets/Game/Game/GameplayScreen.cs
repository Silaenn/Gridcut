using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>
    /// Play screen (scene component). Board on the left, 360-unit control panel on
    /// the right — fixed landscape layout baked in the scene, per spec §4–5. Only
    /// the board interior is built at runtime.
    /// </summary>
    public class GameplayScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public BoardView Board;
        public RectTransform BoardSlot;      // stretches to the board area
        public RectTransform Panel;          // 360-unit side panel / bottom strip
        public TMP_Text LevelLabel, ProgressLabel, TimeLabel;
        public Button ResetBtn, HintBtn, BackBtn, MuteBtn;
        public GameObject WinOverlay;
        public TMP_Text WinTitle, WinDetail;
        public Button WinNextBtn, WinMenuBtn, WinRepeatBtn;
        public RectTransform WinStarsRow;              // 3 Image anak: Star0..2 (spec 160 px ×1.35)
        public TMP_Text WinFinishMsg;                  // pesan pengganti Berikutnya di level terakhir
        public GameObject WinBanner;                   // spanduk tema baru (spec 120 px ×1.35)
        public TMP_Text WinBannerLabel;
        public Image[] BannerSwatches;                 // 3 kotak pratinjau warna tema

        public event Action<int> LevelFinished;   // levelId, raised once on win

        PuzzleSession _session;
        int _levelId;
        float _elapsed;
        bool _timerRun;
        Action<Color> _textTint, _boardTint;
        Coroutine _bannerCo;
        GameObject _tutorial;
        bool _panelCompact;

        // spec panel widths: 360/220 reference ×1.35 (app-wide rebake)
        const float FullWidth = 486f;
        const float CompactWidth = 297f;

        void Awake()
        {
            AutoWire();
            ResetBtn.onClick.AddListener(DoReset);
            HintBtn.onClick.AddListener(DoHint);
            BackBtn.onClick.AddListener(DoBack);
            if (MuteBtn != null) MuteBtn.onClick.AddListener(ToggleMute);
            WinNextBtn.onClick.AddListener(NextLevel);
            WinMenuBtn.onClick.AddListener(GoHome);
            WinRepeatBtn.onClick.AddListener(Replay);

            _textTint = c =>
            {
                if (LevelLabel != null) LevelLabel.color = c;
                if (ProgressLabel != null) ProgressLabel.color = c;
                if (TimeLabel != null) TimeLabel.color = c;
                if (WinTitle != null) WinTitle.color = c;
                if (WinDetail != null) WinDetail.color = c;
                if (WinFinishMsg != null) WinFinishMsg.color = c;
                if (WinBannerLabel != null) WinBannerLabel.color = c;
            };
            // Rebuild() is a no-op while Session is unset, so applyImmediately is safe
            _boardTint = _ => Board.ApplyTheme();
        }

        void OnEnable()
        {
            // applyImmediately: re-tint from the current theme on every appearance
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Rect, _boardTint, true);
        }

        void OnDisable()
        {
            ThemeApplier.Unsubscribe(_textTint);
            // _boardTint is registered on three roles; Unsubscribe clears all of them
            ThemeApplier.Unsubscribe(_boardTint);
            // spec: gameplay disembunyikan membuang state input (drag yang berjalan)
            if (Board != null) Board.ClearPreview();
        }

        /// <summary>Recovers scene refs by child name when serialized wiring fails.</summary>
        void AutoWire()
        {
            var t = transform;
            if (Board == null) Board = UiFactory.Wire<BoardView>(this, null, "BoardSlot/Board");
            if (BoardSlot == null) BoardSlot = UiFactory.Wire<RectTransform>(this, null, "BoardSlot");
            if (Panel == null) Panel = UiFactory.Wire<RectTransform>(this, null, "Panel");
            if (LevelLabel == null) LevelLabel = UiFactory.Wire<TMP_Text>(this, null, "Panel/LevelLabel");
            if (ProgressLabel == null) ProgressLabel = UiFactory.Wire<TMP_Text>(this, null, "Panel/ProgressLabel");
            if (TimeLabel == null) TimeLabel = UiFactory.Wire<TMP_Text>(this, null, "Panel/TimeLabel");
            if (ResetBtn == null) ResetBtn = UiFactory.Wire<Button>(this, null, "Panel/ResetBtn");
            if (HintBtn == null) HintBtn = UiFactory.Wire<Button>(this, null, "Panel/HintBtn");
            if (BackBtn == null) BackBtn = UiFactory.Wire<Button>(this, null, "Panel/BackBtn");
            if (MuteBtn == null) MuteBtn = UiFactory.Wire<Button>(this, null, "Panel/MuteBtn");
            if (WinOverlay == null) WinOverlay = UiFactory.WireGo(this, null, "WinOverlay");
            if (WinTitle == null) WinTitle = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/WinTitle");
            if (WinDetail == null) WinDetail = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/WinDetail");
            if (WinNextBtn == null) WinNextBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/WinNextBtn");
            if (WinMenuBtn == null) WinMenuBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/WinMenuBtn");
            if (WinRepeatBtn == null) WinRepeatBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/WinRepeatBtn");
            if (WinStarsRow == null) WinStarsRow = UiFactory.Wire<RectTransform>(this, null, "WinOverlay/Card/WinStars");
            if (WinFinishMsg == null) WinFinishMsg = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/WinFinishMsg");
            if (WinBanner == null) WinBanner = UiFactory.WireGo(this, null, "WinBanner");
            if (WinBannerLabel == null) WinBannerLabel = UiFactory.Wire<TMP_Text>(this, null, "WinBanner/BannerLabel");
            if (BannerSwatches == null || BannerSwatches.Length != 3)
            {
                var sw = new List<Image>();
                for (int i = 0; i < 3; i++)
                {
                    var swTr = transform.Find("WinBanner/Swatch" + i);
                    if (swTr != null) { var im = swTr.GetComponent<Image>(); if (im != null) sw.Add(im); }
                }
                if (sw.Count == 3) BannerSwatches = sw.ToArray();
            }
        }

        // ------------------------------ flow -----------------------------------

        public void Show(int levelId)
        {
            _levelId = levelId;
            var level = LevelRepository.Get(levelId);
            if (level == null) return;

            ConfigurePanelMode(level.Cols, level.Rows);   // compact panel before the board sizes
            if (_session != null)
            {
                _session.RectPlaced -= OnRectPlaced;
                _session.Invalid -= OnInvalid;
                _session.RectRemoved -= OnRectRemoved;
                _session.SolvedChanged -= OnSolved;
                _session.HintUsed -= OnHintUsed;
            }
            _session = PuzzleSession.Make(level);
            _session.RectPlaced += OnRectPlaced;
            _session.Invalid += OnInvalid;
            _session.RectRemoved += OnRectRemoved;
            _session.SolvedChanged += OnSolved;
            _session.HintUsed += OnHintUsed;

            // spec: melanjutkan mainan yang tersimpan bila levelnya sama
            if (SaveStore.TryLoadPlayState(out int pLevel, out int pHints, out var pRects)
                && pLevel == levelId)
                _session.Restore(pHints, pRects);

            Board.SetLevel(_session);
            _elapsed = 0f;
            _timerRun = true;
            LevelLabel.text = $"Level {_levelId}" + (level.IsTutorial ? " \u00b7 Latihan" : "");
            TimeLabel.text = "0:00";
            UpdateProgress();
            UpdateHintLabel();
            WinOverlay.SetActive(false);
            if (_bannerCo != null) { StopCoroutine(_bannerCo); _bannerCo = null; }   // spanduk tema ikut reset
            if (WinBanner != null) WinBanner.SetActive(false);
            ShowTutorial(level.IsTutorial);   // spec: tutorial overlay above the grid (L1–3)

            // tepi: state tersimpan adalah papan SELESAI (tab ditutup sebelum
            // overlay sempat membuka) → jalankan alur menang normal, bukan macet
            if (_session.IsSolved) OnSolved(true);
        }

        // --------------------------- panel layout modes --------------------------

        /// <summary>
        /// Spec: if the FULL panel would render a cell under 32 real px, switch to the
        /// compact panel (icons only) before shrinking cells. Runs per level, before
        /// the board builds.
        /// </summary>
        void ConfigurePanelMode(int cols, int rows)
        {
            if (BoardSlot == null || Panel == null || cols <= 0 || rows <= 0) return;
            var s = BoardSlot.rect;
            float widen = FullWidth - CompactWidth;                    // slot gains this in compact
            float slotWFull = _panelCompact ? s.width - widen : s.width;
            float slotWComp = _panelCompact ? s.width : s.width + widen;
            float scale = Screen.height / 1080f;                       // canvas units → real px (match-height)
            float realFull = BoardView.EstimateCell(slotWFull, s.height, cols, rows) * scale;
            bool compact = realFull < 32f;
            if (_panelCompact == compact) return;
            _panelCompact = compact;
            Panel.sizeDelta = new Vector2(compact ? CompactWidth : FullWidth, 0f);
            BoardSlot.sizeDelta = new Vector2(-(compact ? CompactWidth : FullWidth), 0f);
            if (LevelLabel != null) LevelLabel.gameObject.SetActive(!compact);
            if (ProgressLabel != null) ProgressLabel.gameObject.SetActive(!compact);
            if (TimeLabel != null) TimeLabel.gameObject.SetActive(!compact);
            foreach (var b in new[] { BackBtn, HintBtn, ResetBtn, MuteBtn })
            {
                var t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
                if (t != null) t.fontSize = compact ? 26f : 38f;
            }
            UpdateHintLabel();
            RefreshMuteLabel();
        }

        public void ToggleMute()
        {
            SaveStore.SetSound(!SaveStore.SoundOn);
            RefreshMuteLabel();
            AudioService.Play("tap");
        }

        void RefreshMuteLabel()
        {
            if (MuteBtn == null) return;
            var t = MuteBtn.GetComponentInChildren<TMP_Text>(true);
            if (t == null) return;
            t.text = _panelCompact
                ? (SaveStore.SoundOn ? "Nyala" : "Mati")
                : (SaveStore.SoundOn ? "Suara: nyala" : "Suara: mati");
        }

        void OnRectPlaced(int ci, RectI r)
        {
            Board.ShowPlaced();
            UpdateProgress();
            AudioService.Play("place");
            SavePlay();   // spec: state level dimainkan disimpan otomatis tiap perubahan
        }

        void OnRectRemoved(int ci, RectI r)
        {
            Board.ShowPlaced();
            UpdateProgress();
            AudioService.Play("tap");
            SavePlay();
        }

        void OnInvalid() => AudioService.Play("invalid");

        void OnHintUsed(int ci)
        {
            UpdateHintLabel();
            SavePlay();   // hint terpakai ikut tersimpan
        }

        /// <summary>Simpan Rect + hint terpakai level ini. Dilewati saat solved —
        /// menang memanggil ClearPlayState (level tidak lagi "sedang dimainkan").</summary>
        void SavePlay()
        {
            if (_session == null || _session.IsSolved) return;
            SaveStore.SavePlayState(_levelId, _session.HintCount, _session.Placed);
        }

        /// <summary>Spec: Hint button carries the remaining count; disabled at 0/3.</summary>
        void UpdateHintLabel()
        {
            if (HintBtn == null || _session == null) return;
            var t = HintBtn.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = $"Hint {_session.HintsLeft}/{PuzzleSession.HintLimit}";
            HintBtn.interactable = _session.HintsLeft > 0;
        }

        void OnSolved(bool solved)
        {
            if (!solved) return;
            _timerRun = false;
            AudioService.Play("win");
            Invoke(nameof(OpenWinOverlay), 0.5f);
        }

        void OpenWinOverlay()
        {
            Board.ClearPreview();
            int stars = _session.HintCount == 0 ? 3 : _session.HintCount <= 1 ? 2 : 1;
            bool wasUnlocking = _levelId >= SaveStore.UnlockedLevel && _levelId < SaveStore.LevelCount;
            SaveStore.RecordWin(_levelId, stars);
            SaveStore.ClearPlayState();   // menang: tidak ada lagi mainan utk dilanjutkan
            int best = SaveStore.BestStars(_levelId);

            WinTitle.text = "Selesai!";   // spec: judul overlay tetap
            WinDetail.text = $"Waktu {TimeSpan.FromSeconds(_elapsed):mm\\:ss}"
                + (wasUnlocking ? "\nLevel baru terbuka!" : "");
            UiFactory.SetStars(WinStarsRow, best,
                ThemeApplier.ColorOf(ThemeApplier.Role.Accent),
                new Color(0.55f, 0.55f, 0.55f, 0.9f));
            bool last = _levelId >= SaveStore.LevelCount;
            WinNextBtn.gameObject.SetActive(!last);
            if (WinFinishMsg != null) WinFinishMsg.gameObject.SetActive(last);   // spec: pesan di level terakhir
            WinOverlay.SetActive(true);
            LevelFinished?.Invoke(_levelId);
            if (wasUnlocking) AudioService.Play("unlock");

            var th = ThemeForUnlockedLevel();
            if (th != null) StartBanner(th);
        }

        static ThemeLibrary.Theme ThemeForUnlockedLevel()
        {
            // spec literal: tema N terbuka SETELAH level N selesai →
            // UnlockedLevel (level berikutnya yang terbuka) == N + 1
            foreach (var t in ThemeLibrary.All)
                if (t.UnlockLevel + 1 == SaveStore.UnlockedLevel) return t;
            return null;
        }

        // spec: spanduk 120 px (×1.35) turun dari atas, hadir total ≈2,5 detik
        void StartBanner(ThemeLibrary.Theme th)
        {
            if (_bannerCo != null) StopCoroutine(_bannerCo);
            _bannerCo = StartCoroutine(BannerSlide(th));
        }

        IEnumerator BannerSlide(ThemeLibrary.Theme th)
        {
            var rt = (RectTransform)WinBanner.transform;
            var rest = new Vector2(0f, -12f);
            var above = new Vector2(0f, rest.y + rt.rect.height + 16f);
            if (WinBannerLabel != null) WinBannerLabel.text = $"{th.Name} terbuka!";
            if (BannerSwatches != null)
                for (int i = 0; i < BannerSwatches.Length; i++)
                    if (i < th.Rects.Length && BannerSwatches[i] != null
                        && ColorUtility.TryParseHtmlString(th.Rects[i], out var c))
                        BannerSwatches[i].color = c;
            rt.anchoredPosition = above;
            WinBanner.SetActive(true);
            yield return Slide(rt, above, rest, 0.25f);
            yield return new WaitForSecondsRealtime(1.85f);
            yield return Slide(rt, rest, above, 0.35f);
            WinBanner.SetActive(false);
            _bannerCo = null;
        }

        static IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float dur)
        {
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = Vector2.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            rt.anchoredPosition = to;
        }

        public void NextLevel()
        {
            WinOverlay.SetActive(false);
            Show(Mathf.Min(_levelId + 1, SaveStore.LevelCount));
        }

        // ------------------------------ tutorial --------------------------------

        /// <summary>Spec: tutorial overlay (levels 1–3) floats above the grid, not the panel.</summary>
        void ShowTutorial(bool tutorial)
        {
            if (!tutorial)
            {
                if (_tutorial != null) _tutorial.SetActive(false);
                return;
            }
            if (_tutorial == null) _tutorial = BuildTutorial();
            _tutorial.transform.Find("Card").Find("Body").GetComponent<TMP_Text>().text = TutorialText(_levelId);
            _tutorial.SetActive(true);
        }

        static string TutorialText(int levelId) => levelId switch
        {
            1 => "Tarik di atas kisi untuk meletakkan persegi panjang.\n\n"
               + "Angka di sel adalah luas persegi yang harus menutupinya.\n\n"
               + "Setiap angka hanya boleh ditutup oleh satu persegi.",
            2 => "Persegi baru yang menimpa persegi lama otomatis\nmenghapus yang lama.\n\n"
               + "Tutup seluruh papan tanpa sisa.\n\n"
               + "Salah menarik? Tap persegi untuk menghapusnya.",
            _ => "Buntu? Reset mengosongkan papan.\n\n"
               + "Hint meletakkan satu persegi yang benar,\ntetapi setiap Hint memangkas bintang.\n\n"
               + "Setelah level ini, kisi kian besar dan kian menantang.",
        };

        GameObject BuildTutorial()
        {
            var rootGo = new GameObject("Tutorial", typeof(RectTransform), typeof(CanvasGroup));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(BoardSlot, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            UiFactory.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.45f), true);   // blocks board input

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            var card = (RectTransform)cardGo.transform;
            card.SetParent(root, false);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(640f, 640f);
            cardGo.GetComponent<Image>().color = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);

            var title = UiFactory.Text(card, "Title", "Cara Bermain", 46,
                ThemeApplier.ColorOf(ThemeApplier.Role.Text), TMPro.TextAlignmentOptions.Center);
            var trt = title.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(560f, 64f);
            trt.anchoredPosition = new Vector2(0f, -40f);

            var body = UiFactory.Text(card, "Body", "", 34,
                ThemeApplier.ColorOf(ThemeApplier.Role.Text), TMPro.TextAlignmentOptions.TopLeft,
                new Vector2(520f, 320f));
            body.enableWordWrapping = true;
            var brt = body.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -120f);

            var btn = UiFactory.Button("MengertiBtn", card, "Mengerti", 40, () =>
                {
                    _tutorial.SetActive(false);
                    AudioService.Play("tap");
                },
                new Vector2(320f, 130f), ThemeApplier.ColorOf(ThemeApplier.Role.Accent));
            var art = btn.GetComponent<RectTransform>();
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0f);
            art.pivot = new Vector2(0.5f, 0f);
            art.anchoredPosition = new Vector2(0f, 36f);

            rootGo.SetActive(false);
            return rootGo;
        }

        public void Replay()
        {
            WinOverlay.SetActive(false);
            Show(_levelId);
            AudioService.Play("tap");
        }

        public void GoHome()
        {
            WinOverlay.SetActive(false);
            AppRoot.Instance.OpenHome();
        }

        public void GoLevelSelect()
        {
            WinOverlay.SetActive(false);
            AppRoot.Instance.OpenLevelSelect();
        }

        public void DoReset() => _session.Reset();


        public void DoHint()
        {
            if (_session == null || _session.IsSolved || _session.HintsLeft <= 0) return;
            if (!_session.TryGetHintTarget(out int ci, out _)) return;
            // spec hint bertingkat: kedipkan angka dulu, baru rect solusi ditempatkan
            var session = _session;   // pengaman bila level berganti saat animasi jalan
            Board.HintPulse(ci, () => session.ApplyHint(ci));
        }

        public void DoBack()
        {
            _timerRun = false;
            AppRoot.Instance.NavBack();
        }

        void UpdateProgress() => ProgressLabel.text = $"{_session.PlacedCount}/{_session.ClueCount}";

        void Update()
        {
            if (_timerRun && TimeLabel != null)
            {
                _elapsed += Time.unscaledDeltaTime;
                TimeLabel.text = $"{(int)_elapsed / 60}:{(int)_elapsed % 60:D2}";
            }
        }
    }
}
