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
    /// Layar gameplay (spec bagian 7). Papan di kiri, kartu kontrol di kanan.
    /// Kartu normal: 384 lebar, 48 dari kanan, padding 34, jarak 24, isi:
    /// kembali 144, judul Bold 60 + sub Medium 32, Hint 312×168, baris Reset/Sound 144.
    /// Kartu ringkas: 220 lebar, 32 dari kanan, padding 26, jarak 16, hanya ikon
    /// + label "Lv N" Bold 44. Tanpa timer, progres, atau undo.
    /// </summary>
    public class GameplayScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public BoardView Board;
        public RectTransform BoardSlot;
        public RectTransform Panel;
        public RectTransform NormalRoot, CompactRoot;

        [Header("Kartu normal")]
        public Button BackBtn, HintBtn, ResetBtn, MuteBtn;
        public TMP_Text LevelTitle, LevelSub;

        [Header("Kartu ringkas")]
        public Button BackBtnC, HintBtnC, ResetBtnC, MuteBtnC;
        public TMP_Text LevelLabelC;

        [Header("Tutorial (level 1–3)")]
        public RectTransform TutorialPill;
        public TMP_Text TutorialText;

        [Header("Overlay menang")]
        public GameObject WinOverlay;
        public TMP_Text WinTitle, WinDetail;
        public Button WinNextBtn, WinMenuBtn, WinRepeatBtn;
        public RectTransform WinStarsRow;
        public TMP_Text WinFinishMsg;
        public GameObject WinBanner;
        public TMP_Text WinBannerLabel;
        public Image[] BannerSwatches;

        public event Action<int, int> LevelCompleted;

        PuzzleSession _session;
        int _levelId;
        float _elapsed;
        bool _timerRun;
        bool _isCompact;

        Action<Color> _textTint, _mutedTint, _boardTint, _accentTint, _frameTint, _primaryTint, _badgeTint;
        Image _badgeBack, _badgeDisc, _badgeBackC, _badgeDiscC;
        TMP_Text _badgeCount, _badgeCountC;
        Coroutine _bannerCo;

        const float NormalCardW = 384f;
        const float CompactCardW = 220f;
        const float NormalMargin = 48f;
        const float CompactMargin = 32f;
        const float NormalHeight = 716f;
        const float CompactHeight = 744f;

        void Awake()
        {
            AutoWire();
            WireBadges();

            Add(BackBtn, DoBack);
            Add(BackBtnC, DoBack);
            Add(HintBtn, DoHint);
            Add(HintBtnC, DoHint);
            Add(ResetBtn, DoReset);
            Add(ResetBtnC, DoReset);
            Add(MuteBtn, ToggleMute);
            Add(MuteBtnC, ToggleMute);

            Add(WinNextBtn, NextLevel);
            Add(WinMenuBtn, GoHome);
            Add(WinRepeatBtn, Replay);

            if (Board != null)
            {
                Board.DragStarted += OnDragStarted;
                Board.DragCellChanged += OnDragCellChanged;
            }

            _textTint = c =>
            {
                if (LevelTitle != null) LevelTitle.color = c;
                if (LevelLabelC != null) LevelLabelC.color = c;
                if (TutorialText != null) TutorialText.color = c;
                if (WinTitle != null) WinTitle.color = c;
                if (WinDetail != null) WinDetail.color = c;
                if (WinFinishMsg != null) WinFinishMsg.color = c;
                if (WinBannerLabel != null) WinBannerLabel.color = c;
            };
            _mutedTint = c => { if (LevelSub != null) LevelSub.color = c; };
            _boardTint = _ => { if (Board != null) Board.ApplyTheme(); };
            _accentTint = c =>
            {
                if (_badgeDisc != null) _badgeDisc.color = c;
                if (_badgeCount != null) _badgeCount.color = ThemeApplier.ContrastTextOn(c);
                if (_badgeDiscC != null) _badgeDiscC.color = c;
                if (_badgeCountC != null) _badgeCountC.color = ThemeApplier.ContrastTextOn(c);
            };
            _primaryTint = _ =>
            {
                Primary(HintBtn);
                Primary(HintBtnC);
            };
            _badgeTint = c =>
            {
                if (_badgeBack != null) _badgeBack.color = c;
                if (_badgeBackC != null) _badgeBackC.color = c;
            };
            _frameTint = _ =>
            {
                Frame(BackBtn); Frame(ResetBtn); Frame(MuteBtn);
                Frame(BackBtnC); Frame(ResetBtnC); Frame(MuteBtnC);
                if (Panel != null) UiFactory.CardTint(Panel);
                if (TutorialPill != null) UiFactory.CardTint(TutorialPill);
            };

            UiFactory.PressFx(HintBtn, -16f);
            UiFactory.PressFx(HintBtnC, -16f);
            UiFactory.PressFx(BackBtn, -12f);
            UiFactory.PressFx(BackBtnC, -12f);
            UiFactory.PressFx(ResetBtn, -12f);
            UiFactory.PressFx(ResetBtnC, -12f);
            UiFactory.PressFx(MuteBtn, -12f);
            UiFactory.PressFx(MuteBtnC, -12f);
        }

        static void Add(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        static void Frame(Button b) { if (b != null) UiFactory.FrameTint(b, true); }

        static void Primary(Button b)
        {
            if (b == null) return;
            UiFactory.PrimaryTint(b, -16f);
            var lamp = b.transform.Find("LampIcon");
            if (lamp != null)
            {
                var img = lamp.GetComponent<Image>();
                if (img != null)
                {
                    var c = ThemeApplier.ContrastTextOn(ThemeApplier.ColorOf(ThemeApplier.Role.Primary));
                    c.a = 1f;
                    img.color = c;
                }
            }
        }

        void OnEnable()
        {
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.TextMuted, _mutedTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Rect, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Accent, _accentTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Primary, _primaryTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _badgeTint, true);
        }

        void OnDisable()
        {
            ThemeApplier.Unsubscribe(_textTint);
            ThemeApplier.Unsubscribe(_mutedTint);
            ThemeApplier.Unsubscribe(_boardTint);
            ThemeApplier.Unsubscribe(_accentTint);
            ThemeApplier.Unsubscribe(_frameTint);
            ThemeApplier.Unsubscribe(_primaryTint);
            ThemeApplier.Unsubscribe(_badgeTint);
            if (Board != null) Board.ClearPreview();
            if (WinOverlay != null && WinOverlay.activeSelf) WinOverlay.SetActive(false);
        }

        void AutoWire()
        {
            if (Board == null) Board = UiFactory.Wire<BoardView>(this, null, "BoardSlot/Board");
            if (BoardSlot == null) BoardSlot = UiFactory.Wire<RectTransform>(this, null, "BoardSlot");
            if (Panel == null) Panel = UiFactory.Wire<RectTransform>(this, null, "Panel");
            if (NormalRoot == null) NormalRoot = UiFactory.Wire<RectTransform>(this, null, "Panel/NormalRoot");
            if (CompactRoot == null) CompactRoot = UiFactory.Wire<RectTransform>(this, null, "Panel/CompactRoot");

            BackBtn = UiFactory.Wire<Button>(this, BackBtn, "Panel/NormalRoot/BackSeat/BackBtn");
            LevelTitle = UiFactory.Wire<TMP_Text>(this, LevelTitle, "Panel/NormalRoot/TitleBlock/LevelTitle");
            LevelSub = UiFactory.Wire<TMP_Text>(this, LevelSub, "Panel/NormalRoot/TitleBlock/LevelSub");
            HintBtn = UiFactory.Wire<Button>(this, HintBtn, "Panel/NormalRoot/HintSeat/HintBtn");
            ResetBtn = UiFactory.Wire<Button>(this, ResetBtn, "Panel/NormalRoot/BottomRow/ResetSeat/ResetBtn");
            MuteBtn = UiFactory.Wire<Button>(this, MuteBtn, "Panel/NormalRoot/BottomRow/SoundSeat/MuteBtn");

            BackBtnC = UiFactory.Wire<Button>(this, BackBtnC, "Panel/CompactRoot/BackSeat/BackBtn");
            LevelLabelC = UiFactory.Wire<TMP_Text>(this, LevelLabelC, "Panel/CompactRoot/LevelLabel");
            HintBtnC = UiFactory.Wire<Button>(this, HintBtnC, "Panel/CompactRoot/HintSeat/HintBtn");
            ResetBtnC = UiFactory.Wire<Button>(this, ResetBtnC, "Panel/CompactRoot/ResetSeat/ResetBtn");
            MuteBtnC = UiFactory.Wire<Button>(this, MuteBtnC, "Panel/CompactRoot/SoundSeat/MuteBtn");

            TutorialPill = UiFactory.Wire<RectTransform>(this, TutorialPill, "BoardSlot/TutorialPill");
            TutorialText = UiFactory.Wire<TMP_Text>(this, TutorialText, "BoardSlot/TutorialPill/Text");

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
                    var tr = transform.Find("WinBanner/Swatch" + i);
                    if (tr != null)
                    {
                        var im = tr.GetComponent<Image>();
                        if (im != null) sw.Add(im);
                    }
                }
                if (sw.Count == 3) BannerSwatches = sw.ToArray();
            }
        }

        void WireBadges()
        {
            _badgeBack = _badgeDisc = null; _badgeCount = null; _badgeBackC = _badgeDiscC = null; _badgeCountC = null;
            WireBadge(HintBtn, out _badgeBack, out _badgeDisc, out _badgeCount);
            WireBadge(HintBtnC, out _badgeBackC, out _badgeDiscC, out _badgeCountC);
        }

        static void WireBadge(Button b, out Image back, out Image disc, out TMP_Text count)
        {
            back = disc = null; count = null;
            if (b == null) return;
            var badge = b.transform.Find("HintBadge");
            if (badge == null) return;
            back = badge.GetComponent<Image>();
            var d = badge.Find("Disc"); if (d != null) disc = d.GetComponent<Image>();
            var c = badge.Find("Count"); if (c != null) count = c.GetComponent<TMP_Text>();
        }

        // ------------------------------ alur -----------------------------------

        public void Show(int levelId)
        {
            _levelId = levelId;
            var level = LevelRepository.Get(levelId);
            if (level == null) return;

            ConfigurePanelMode(level.Cols, level.Rows);

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

            if (SaveStore.TryLoadPlayState(out int pLevel, out int pHints, out var pRects)
                && pLevel == levelId)
                _session.Restore(pHints, pRects);

            Board.SetLevel(_session);
            _elapsed = 0f;
            _timerRun = true;

            UpdateLevelLabels(level);
            UpdateHintLabels();
            RefreshSoundIcons();
            ShowTutorial(level);

            if (WinOverlay != null) WinOverlay.SetActive(false);
            if (_bannerCo != null) { StopCoroutine(_bannerCo); _bannerCo = null; }
            if (WinBanner != null) WinBanner.SetActive(false);

            if (_session.IsSolved) OnSolved(true);
        }

        void ConfigurePanelMode(int cols, int rows)
        {
            if (BoardSlot == null || Panel == null) return;
            var rt = (RectTransform)transform;
            float cw = rt.rect.width;
            float ch = rt.rect.height;

            bool compact = BoardView.IsCompactFor(cw, ch, cols, rows);
            _isCompact = compact;
            Board.SetCompact(compact);

            float areaW = cw - (compact ? 252f : 432f);
            BoardSlot.anchorMin = new Vector2(0f, 0f);
            BoardSlot.anchorMax = new Vector2(0f, 1f);
            BoardSlot.pivot = new Vector2(0f, 0.5f);
            BoardSlot.anchoredPosition = Vector2.zero;
            BoardSlot.sizeDelta = new Vector2(areaW, 0f);

            Panel.anchorMin = Panel.anchorMax = new Vector2(1f, 0.5f);
            Panel.pivot = new Vector2(1f, 0.5f);
            Panel.anchoredPosition = new Vector2(-(compact ? CompactMargin : NormalMargin), 0f);
            Panel.sizeDelta = new Vector2(compact ? CompactCardW : NormalCardW,
                compact ? CompactHeight : NormalHeight);

            if (NormalRoot != null) NormalRoot.gameObject.SetActive(!compact);
            if (CompactRoot != null) CompactRoot.gameObject.SetActive(compact);
        }

        void UpdateLevelLabels(LevelData level)
        {
            string sub = level.IsTutorial ? "Tutorial" : $"{level.Cols} x {level.Rows}";
            if (LevelTitle != null) LevelTitle.text = $"Level {_levelId}";
            if (LevelSub != null) LevelSub.text = sub;
            if (LevelLabelC != null) LevelLabelC.text = $"Lv {_levelId}";
        }

        void ShowTutorial(LevelData level)
        {
            if (TutorialPill == null) return;
            bool on = level.IsTutorial;
            TutorialPill.gameObject.SetActive(on);
            if (!on || TutorialText == null) return;

            TutorialText.text = TutorialCopy(level.Id);
            TutorialText.ForceMeshUpdate();
            float maxW = (BoardSlot != null ? BoardSlot.rect.width : 1488f) - 96f;
            float w = Mathf.Min(TutorialText.preferredWidth + 56f, maxW);
            TutorialPill.sizeDelta = new Vector2(Mathf.Max(120f, w), 96f);
        }

        static string TutorialCopy(int id) => id switch
        {
            1 => "Drag on the grid to cover the numbers",
            2 => "A new rectangle replaces the old one",
            _ => "Tap a rectangle to remove it",
        };

        public void ToggleMute()
        {
            SaveStore.SetSound(!SaveStore.SoundOn);
            RefreshSoundIcons();
            AudioService.Play("tap");
        }

        void RefreshSoundIcons()
        {
            SetSoundIcons(MuteBtn);
            SetSoundIcons(MuteBtnC);
        }

        static void SetSoundIcons(Button b)
        {
            if (b == null) return;
            var on = b.transform.Find("IconOn");
            var off = b.transform.Find("IconOff");
            bool sound = SaveStore.SoundOn;
            if (on != null) on.gameObject.SetActive(sound);
            if (off != null) off.gameObject.SetActive(!sound);
        }

        void OnRectPlaced(int ci, RectI r)
        {
            Board.ShowPlaced();
            AudioService.Play("place");
            SavePlay();
        }

        void OnRectRemoved(int ci, RectI r)
        {
            Board.ShowPlaced();
            AudioService.Play("tap");
            SavePlay();
        }

        void OnInvalid() => AudioService.Play("invalid");

        void OnHintUsed(int ci)
        {
            UpdateHintLabels();
            SavePlay();
        }

        void OnDragStarted()
        {
            if (_session != null && !_session.IsSolved) AudioService.Play("drag");
        }

        void OnDragCellChanged() { }

        void SavePlay()
        {
            if (_session == null || _session.IsSolved) return;
            SaveStore.SavePlayState(_levelId, _session.HintCount, _session.Placed);
        }

        void UpdateHintLabels()
        {
            if (_session == null) return;
            int left = _session.HintsLeft;
            if (HintBtn != null) HintBtn.interactable = left > 0;
            if (HintBtnC != null) HintBtnC.interactable = left > 0;
            if (_badgeCount != null) _badgeCount.text = left.ToString();
            if (_badgeCountC != null) _badgeCountC.text = left.ToString();
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
            int stars = _session.Level.IsTutorial ? 3
                : _session.HintCount == 0 ? 3 : _session.HintCount <= 1 ? 2 : 1;
            bool unlocking = _levelId >= SaveStore.UnlockedLevel && _levelId < SaveStore.LevelCount;
            SaveStore.RecordWin(_levelId, stars);
            SaveStore.ClearPlayState();
            int best = SaveStore.BestStars(_levelId);

            WinTitle.text = "Complete!";
            WinDetail.text = $"Time {TimeSpan.FromSeconds(_elapsed):mm\\:ss}"
                + (unlocking ? "\nNew level unlocked!" : "");
            UiFactory.SetStars(WinStarsRow, best,
                ThemeApplier.ColorOf(ThemeApplier.Role.Primary),
                ThemeApplier.ColorOf(ThemeApplier.Role.GridLine));
            bool last = _levelId >= SaveStore.LevelCount;
            WinNextBtn.gameObject.SetActive(!last);
            if (WinFinishMsg != null) WinFinishMsg.gameObject.SetActive(last);
            WinOverlay.SetActive(true);
            UiFactory.PrimaryTint(WinNextBtn, -16f);
            UiFactory.FrameTint(WinRepeatBtn, true);
            UiFactory.FrameTint(WinMenuBtn, true);
            LevelCompleted?.Invoke(_levelId, stars);
            if (unlocking) AudioService.Play("unlock");

            var th = ThemeForUnlockedLevel();
            if (th != null) StartBanner(th);
        }

        static ThemeLibrary.Theme ThemeForUnlockedLevel()
        {
            foreach (var t in ThemeLibrary.All)
                if (t.UnlockLevel + 1 == SaveStore.UnlockedLevel) return t;
            return null;
        }

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
            if (WinBannerLabel != null) WinBannerLabel.text = $"{th.Name} unlocked!";
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
            AudioService.Play("tap");
            var session = _session;
            Board.HintPulse(ci, () => session.ApplyHint(ci));
        }

        public void DoBack()
        {
            _timerRun = false;
            AppRoot.Instance.NavBack();
        }

        void Update()
        {
            if (_timerRun) _elapsed += Time.unscaledDeltaTime;
        }
    }
}