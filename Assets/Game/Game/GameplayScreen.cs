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
    /// Layar gameplay (spec bagian 7 revisi): grid di tengah, tanpa panel samping.
    /// Kontrol atas: Back kiri, judul Level N tengah, Reset+Sound kanan.
    /// Tombol Hint 380×128 di bawah grid. Tidak ada timer/progres/undo.
    /// </summary>
    public class GameplayScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public BoardView Board;
        public RectTransform BoardSlot;
        public Button BackBtn, ResetBtn, MuteBtn, HintBtn;
        public TMP_Text LevelTitle;

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

        Action<Color> _textTint, _boardTint, _accentTint, _frameTint, _primaryTint, _badgeTint;
        Image _badgeBack, _badgeDisc, _muteOn, _muteOff, _lamp;
        TMP_Text _badgeCount;
        Coroutine _bannerCo;

        void Awake()
        {
            AutoWire();
            WireBadge();

            Add(BackBtn, DoBack);
            Add(ResetBtn, DoReset);
            Add(MuteBtn, ToggleMute);
            Add(HintBtn, DoHint);

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
                if (WinTitle != null) WinTitle.color = c;
                if (WinDetail != null) WinDetail.color = c;
                if (WinFinishMsg != null) WinFinishMsg.color = c;
                if (WinBannerLabel != null) WinBannerLabel.color = c;
            };
            _boardTint = _ => { if (Board != null) Board.ApplyTheme(); };
            _accentTint = c =>
            {
                if (_badgeDisc != null) _badgeDisc.color = c;
                if (_badgeCount != null) _badgeCount.color = ThemeApplier.ContrastTextOn(c);
            };
            _badgeTint = c => { if (_badgeBack != null) _badgeBack.color = c; };
            _frameTint = _ =>
            {
                Frame(BackBtn); Frame(ResetBtn); Frame(MuteBtn);
            };
            _primaryTint = _ =>
            {
                if (HintBtn != null) UiFactory.PrimaryTint(HintBtn, -16f);
                if (_lamp != null)
                {
                    var c = ThemeApplier.ContrastTextOn(ThemeApplier.ColorOf(ThemeApplier.Role.Primary));
                    c.a = 1f;
                    _lamp.color = c;
                }
            };

            UiFactory.PressFx(HintBtn, -16f);
            UiFactory.PressFx(BackBtn, -12f);
            UiFactory.PressFx(ResetBtn, -12f);
            UiFactory.PressFx(MuteBtn, -12f);
        }

        static void Add(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        static void Frame(Button b) { if (b != null) UiFactory.FrameTint(b, true); }

        void OnEnable()
        {
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _textTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Rect, _boardTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Accent, _accentTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.Primary, _primaryTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _frameTint, true);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _badgeTint, true);
        }

        void OnDisable()
        {
            ThemeApplier.Unsubscribe(_textTint);
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
            if (BackBtn == null) BackBtn = UiFactory.Wire<Button>(this, null, "BackBtn");
            if (LevelTitle == null) LevelTitle = UiFactory.Wire<TMP_Text>(this, null, "LevelTitle");
            if (ResetBtn == null) ResetBtn = UiFactory.Wire<Button>(this, null, "ResetBtn");
            if (MuteBtn == null) MuteBtn = UiFactory.Wire<Button>(this, null, "MuteBtn");
            if (HintBtn == null) HintBtn = UiFactory.Wire<Button>(this, null, "HintBtn");

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

        void WireBadge()
        {
            var badge = HintBtn != null ? HintBtn.transform.Find("HintBadge") : null;
            if (badge != null)
            {
                var disc = badge.Find("Disc");
                var cnt = badge.Find("Count");
                _badgeBack = badge.GetComponent<Image>();
                _badgeDisc = disc != null ? disc.GetComponent<Image>() : null;
                _badgeCount = cnt != null ? cnt.GetComponent<TMP_Text>() : null;
            }
            _lamp = HintBtn != null && HintBtn.transform.Find("LampIcon") != null
                ? HintBtn.transform.Find("LampIcon").GetComponent<Image>() : null;
            if (MuteBtn != null)
            {
                var on = MuteBtn.transform.Find("IconOn");
                var off = MuteBtn.transform.Find("IconOff");
                _muteOn = on != null ? on.GetComponent<Image>() : null;
                _muteOff = off != null ? off.GetComponent<Image>() : null;
            }
        }

        // ------------------------------ alur -----------------------------------

        public void Show(int levelId)
        {
            _levelId = levelId;
            var level = LevelRepository.Get(levelId);
            if (level == null) return;

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

            LevelTitle.text = $"Level {_levelId}";
            UpdateHintLabel();
            RefreshMuteIcons();

            if (WinOverlay != null) WinOverlay.SetActive(false);
            if (_bannerCo != null) { StopCoroutine(_bannerCo); _bannerCo = null; }
            if (WinBanner != null) WinBanner.SetActive(false);

            PositionHint();
            if (_session.IsSolved) OnSolved(true);
        }

        void PositionHint()
        {
            if (HintBtn == null || Board == null) return;
            var rt = (RectTransform)HintBtn.transform;
            float canvasH = ((RectTransform)transform).rect.height;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            // Slot Hint 128 px tetap mempertahankan papan di posisi spek; tombol
            // yang lebih kecil (108) digeser ke atas agar jarak papan tetap 44.
            float hintTopCanvas = Board.HintCenterY + 64f;
            rt.anchoredPosition = new Vector2(0f, hintTopCanvas + canvasH * 0.5f - rt.rect.height);
        }

        public void ToggleMute()
        {
            SaveStore.SetSound(!SaveStore.SoundOn);
            RefreshMuteIcons();
            AudioService.RefreshMusicVolume();
            AudioService.Play("tap");
        }

        void RefreshMuteIcons()
        {
            bool on = SaveStore.SoundOn;
            if (_muteOn != null) _muteOn.gameObject.SetActive(on);
            if (_muteOff != null) _muteOff.gameObject.SetActive(!on);
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
            UpdateHintLabel();
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

        void UpdateHintLabel()
        {
            if (_session == null) return;
            int left = _session.HintsLeft;
            bool active = left > 0 && !_session.IsSolved;
            if (HintBtn != null) HintBtn.interactable = active;
            if (_badgeCount != null) _badgeCount.text = left.ToString();
            var lbl = HintBtn != null ? HintBtn.transform.Find("Label") : null;
            var t = lbl != null ? lbl.GetComponent<TMP_Text>() : null;
            if (t != null) t.text = "Hint";
        }

        void OnSolved(bool solved)
        {
            if (!solved) return;
            _timerRun = false;
            UpdateHintLabel();   // tombol Hint nonaktif saat level selesai
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