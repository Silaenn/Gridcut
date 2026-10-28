using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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
        public Image WinDim;
        public CanvasGroup WinDimGroup;
        public Button WinDimBtn;
        public RectTransform WinCard;
        public CanvasGroup WinCardGroup;
        public TMP_Text WinTitle, WinDetail, WinReason;
        public Button WinNextBtn, WinMenuBtn, WinRepeatBtn;
        public RectTransform WinStarsRow;
        public Image[] WinStars = new Image[3];
        public TMP_Text WinNextLabel;
        public Image WinNextArrow;
        public GameObject WinNewBest;
        public TMP_Text WinNewBestLabel;
        public GameObject WinBanner;
        public TMP_Text WinBannerLabel;
        public Image[] BannerSwatches;

        public event Action<int, int> LevelCompleted;

        PuzzleSession _session;
        int _levelId;
        bool _suppressRemoveFx;

        Action<Color> _textTint, _boardTint, _accentTint, _frameTint, _primaryTint, _badgeTint, _mutedTint;
        Image _badgeBack, _badgeDisc, _muteOn, _muteOff, _lamp;
        TMP_Text _badgeCount;
        Coroutine _bannerCo;

        // --- win overlay state (bagian 14) ---
        bool _winOpen, _winAnimating;
        int _winStarCount;
        float _winIdle;
        Coroutine _winCo;
        ThemeLibrary.Theme _winTheme;
        CanvasGroup _titleGroup, _subGroup, _reasonGroup, _menuGroup, _nextGroup, _repeatGroup, _bestGroup, _bannerGroup;
        Vector2 _titleRest, _subRest, _reasonRest, _bestRest, _menuRest, _nextRest, _repeatRest, _bannerRest;

        void Awake()
        {
            AutoWire();
            WireBadge();
            InitWinLayout();

            Add(BackBtn, DoBack);
            Add(ResetBtn, DoReset);
            Add(MuteBtn, ToggleMute);
            Add(HintBtn, DoHint);

            Add(WinNextBtn, NextLevel);
            Add(WinMenuBtn, GoHome);
            Add(WinRepeatBtn, Replay);
            Add(WinDimBtn, SkipWinAnimation);

            _textTint = c =>
            {
                if (LevelTitle != null) LevelTitle.color = c;
                if (WinTitle != null) WinTitle.color = c;
                if (WinBannerLabel != null) WinBannerLabel.color = c;
            };
            _mutedTint = c =>
            {
                if (WinDetail != null) WinDetail.color = c;
                if (WinReason != null) WinReason.color = c;
            };
            _boardTint = _ => { if (Board != null) Board.ApplyTheme(); };
            _accentTint = c =>
            {
                if (_badgeDisc != null) _badgeDisc.color = c;
                if (_badgeCount != null) _badgeCount.color = ThemeApplier.ContrastTextOn(c);
                if (WinNewBest != null)
                {
                    var img = WinNewBest.GetComponent<Image>();
                    if (img != null) img.color = c;
                    var sh = WinNewBest.GetComponent<Shadow>();
                    if (sh != null) sh.effectColor = ThemeApplier.ColorOf(ThemeApplier.Role.ShadowAccent);
                    if (WinNewBestLabel != null) WinNewBestLabel.color = ThemeApplier.ContrastTextOn(c);
                }
            };
            _badgeTint = c => { if (_badgeBack != null) _badgeBack.color = c; };
            _frameTint = _ =>
            {
                Frame(BackBtn); Frame(ResetBtn); Frame(MuteBtn);
                Frame(WinMenuBtn); Frame(WinRepeatBtn);
                if (WinCard != null) UiFactory.CardTint(WinCard, -20f);
                if (WinBanner != null) UiFactory.CardTint(WinBanner.transform, -12f);
                UpdateWinStarColors();
            };
            _primaryTint = _ =>
            {
                if (HintBtn != null) UiFactory.PrimaryTint(HintBtn, -16f);
                if (WinNextBtn != null) UiFactory.PrimaryTint(WinNextBtn, -16f);
                if (WinNextArrow != null)
                    WinNextArrow.color = ThemeApplier.ContrastTextOn(ThemeApplier.ColorOf(ThemeApplier.Role.Primary));
                if (_lamp != null)
                {
                    var c = ThemeApplier.ContrastTextOn(ThemeApplier.ColorOf(ThemeApplier.Role.Primary));
                    c.a = 1f;
                    _lamp.color = c;
                }
                UpdateWinStarColors();
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

        static void RebaseButton(Button b)
        {
            if (b == null) return;
            var fx = b.GetComponent<ButtonFX>();
            if (fx != null) fx.RebaseRest();
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
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _badgeTint, true);
            if (Board != null)
            {
                Board.DragStarted -= OnDragStarted;
                Board.DragCellChanged -= OnDragCellChanged;
                Board.DragStarted += OnDragStarted;
                Board.DragCellChanged += OnDragCellChanged;
            }
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
            if (Board != null)
            {
                Board.DragStarted -= OnDragStarted;
                Board.DragCellChanged -= OnDragCellChanged;
                Board.ClearPreview();
            }
            CloseWin();
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
            if (WinOverlay == null)
            {
                var wo = transform.root.Find("Overlay/WinOverlay");
                if (wo != null) WinOverlay = wo.gameObject;
            }
            if (WinDim == null && WinOverlay != null)
            {
                var d = WinOverlay.transform.Find("Dim");
                if (d != null)
                {
                    WinDim = d.GetComponent<Image>();
                    WinDimGroup = d.GetComponent<CanvasGroup>();
                    WinDimBtn = d.GetComponent<Button>();
                }
            }
            if (WinCard == null && WinOverlay != null) WinCard = WinOverlay.transform.Find("Card") as RectTransform;
            if (WinCardGroup == null && WinCard != null) WinCardGroup = WinCard.GetComponent<CanvasGroup>();
            if (WinTitle == null) WinTitle = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/Title");
            if (WinDetail == null) WinDetail = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/Subtitle");
            if (WinReason == null) WinReason = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/Reason");
            if (WinNextBtn == null) WinNextBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/Buttons/NextBtn");
            if (WinMenuBtn == null) WinMenuBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/Buttons/MenuBtn");
            if (WinRepeatBtn == null) WinRepeatBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/Buttons/ReplayBtn");
            if (WinStarsRow == null) WinStarsRow = UiFactory.Wire<RectTransform>(this, null, "WinOverlay/Card/Stars");
            if (WinStars == null || WinStars.Length != 3)
            {
                var list = new List<Image>();
                if (WinStarsRow != null)
                    for (int i = 0; i < WinStarsRow.childCount; i++)
                    {
                        var im = WinStarsRow.GetChild(i).GetComponent<Image>();
                        if (im != null) list.Add(im);
                    }
                WinStars = list.ToArray();
            }
            if (WinNextLabel == null) WinNextLabel = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/Buttons/NextBtn/Label");
            if (WinNextArrow == null) WinNextArrow = UiFactory.Wire<Image>(this, null, "WinOverlay/Card/Buttons/NextBtn/Arrow");
            if (WinNewBest == null && WinOverlay != null)
            {
                var nb = WinOverlay.transform.Find("Card/NewBest");
                if (nb != null) WinNewBest = nb.gameObject;
            }
            if (WinNewBestLabel == null) WinNewBestLabel = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/NewBest/Label");
            if (WinBanner == null && WinOverlay != null)
            {
                var bn = WinOverlay.transform.Find("WinBanner");
                if (bn != null) WinBanner = bn.gameObject;
            }
            if (WinBannerLabel == null) WinBannerLabel = UiFactory.Wire<TMP_Text>(this, null, "WinBanner/Content/Label");
            if (BannerSwatches == null || BannerSwatches.Length != 3)
            {
                var sw = new List<Image>();
                for (int i = 0; i < 3; i++)
                {
                    var tr = WinBanner != null ? WinBanner.transform.Find("Content/Swatches/Swatch" + i) : null;
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

        void InitWinLayout()
        {
            if (WinDim != null && WinDimGroup == null) WinDimGroup = EnsureGroup(WinDim.gameObject);
            if (WinCard != null && WinCardGroup == null) WinCardGroup = EnsureGroup(WinCard.gameObject);
            _titleGroup = EnsureGroup(WinTitle != null ? WinTitle.gameObject : null);
            _subGroup = EnsureGroup(WinDetail != null ? WinDetail.gameObject : null);
            _reasonGroup = EnsureGroup(WinReason != null ? WinReason.gameObject : null);
            _menuGroup = EnsureGroup(WinMenuBtn != null ? WinMenuBtn.gameObject : null);
            _nextGroup = EnsureGroup(WinNextBtn != null ? WinNextBtn.gameObject : null);
            _repeatGroup = EnsureGroup(WinRepeatBtn != null ? WinRepeatBtn.gameObject : null);
            _bestGroup = EnsureGroup(WinNewBest);
            _bannerGroup = EnsureGroup(WinBanner);

            _titleRest = PosOf(WinTitle != null ? WinTitle.transform : null);
            _subRest = PosOf(WinDetail != null ? WinDetail.transform : null);
            _reasonRest = PosOf(WinReason != null ? WinReason.transform : null);
            _bestRest = PosOf(WinNewBest != null ? WinNewBest.transform : null);
            _menuRest = PosOf(WinMenuBtn != null ? WinMenuBtn.transform : null);
            _nextRest = PosOf(WinNextBtn != null ? WinNextBtn.transform : null);
            _repeatRest = PosOf(WinRepeatBtn != null ? WinRepeatBtn.transform : null);
            _bannerRest = PosOf(WinBanner != null ? WinBanner.transform : null);

            if (WinNewBest != null) WinNewBest.SetActive(false);
            if (WinBanner != null) WinBanner.SetActive(false);
            if (WinOverlay != null) WinOverlay.SetActive(false);
        }

        static CanvasGroup EnsureGroup(GameObject go)
        {
            if (go == null) return null;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        static Vector2 PosOf(Transform t) => t is RectTransform rt ? rt.anchoredPosition : Vector2.zero;

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
            if (Board != null) { Board.PlaceGate = null; Board.TapGate = null; }
            if (ResetBtn != null) ResetBtn.interactable = true;
            LevelTitle.text = $"Level {_levelId}";
            UpdateHintLabel();
            RefreshMuteIcons();

            if (WinOverlay != null) CloseWin();
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
            RebaseButton(HintBtn);
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
            Board.PlayPlaceFx(ci, r);
            Board.PulseClue(ci);
            AudioService.Play("place");
            SavePlay();
        }

        void OnRectRemoved(int ci, RectI r)
        {
            Board.ShowPlaced();
            if (!_suppressRemoveFx) Board.PlayRemoveFx(ci, r);
            AudioService.Play("remove");
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
            UpdateHintLabel();
            AudioService.Play("win");
            if (Board != null)
            {
                Board.PlaceGate = _ => false;
                Board.TapGate = (_, __) => false;
            }
            if (ResetBtn != null) ResetBtn.interactable = false;

            int stars = _session.HintCount == 0 ? 3 : _session.HintCount <= 1 ? 2 : 1;
            int prevBest = SaveStore.BestStars(_levelId);
            bool wasCompleted = prevBest > 0;
            int unlockedBefore = SaveStore.UnlockedLevel;
            SaveStore.RecordWin(_levelId, stars);
            SaveStore.ClearPlayState();
            bool unlocking = _levelId >= unlockedBefore && _levelId < SaveStore.LevelCount;
            var theme = unlocking ? ThemeForUnlockedLevel(_levelId) : null;

            _winStarCount = stars;
            _winTheme = theme;
            LevelCompleted?.Invoke(_levelId, stars);

            if (_winCo != null) StopCoroutine(_winCo);
            _winCo = StartCoroutine(WinSequence(prevBest, wasCompleted, theme));
        }

        static ThemeLibrary.Theme ThemeForUnlockedLevel(int levelId)
        {
            if (levelId <= 1) return null;   // Paper adalah tema awal, jangan diumumkan lagi
            foreach (var t in ThemeLibrary.All)
                if (t.UnlockLevel == levelId) return t;
            return null;
        }

        IEnumerator WinSequence(int prevBest, bool wasCompleted, ThemeLibrary.Theme theme)
        {
            Board.ClearPreview();
            if (!SaveStore.ReduceMotion)
            {
                Board.Celebrate();
                yield return new WaitForSecondsRealtime(0.8f);
            }

            PrepareWinContent(prevBest, wasCompleted, theme);
            WinOverlay.SetActive(true);
            _winOpen = true;
            _winAnimating = true;
            _winIdle = 0f;
            ResetWinVisuals();

            if (SaveStore.ReduceMotion)
            {
                FinalizeWinState();
                _winCo = null;
                yield break;
            }

            yield return FadeGroup(WinDimGroup, 0f, 1f, 0.12f);
            yield return PanelOpen(0.18f);

            StartCoroutine(MoveFadeGroup(_titleGroup, (RectTransform)WinTitle.transform,
                _titleRest + new Vector2(0f, 24f), _titleRest, 0.16f));
            StartCoroutine(MoveFadeGroup(_subGroup, (RectTransform)WinDetail.transform,
                _subRest + new Vector2(0f, 24f), _subRest, 0.16f));
            yield return new WaitForSecondsRealtime(0.16f);

            for (int i = 0; i < 3 && WinStars != null && i < WinStars.Length; i++)
            {
                if (WinStars[i] == null) continue;
                var srt = (RectTransform)WinStars[i].transform;
                srt.localScale = Vector3.zero;
                AudioService.Play("star" + (i + 1));
                yield return PopStar(srt, 0.20f);
                StartCoroutine(StarBurst(srt));
                yield return new WaitForSecondsRealtime(0.10f);
            }

            if (theme != null && WinBanner != null)
            {
                if (_bannerCo != null) StopCoroutine(_bannerCo);
                _bannerCo = StartCoroutine(BannerShow());
            }

            if (WinNewBest != null && WinNewBest.activeSelf) StartCoroutine(PopBest());
            StartCoroutine(MoveFadeGroup(_reasonGroup, (RectTransform)WinReason.transform,
                _reasonRest - new Vector2(0f, 24f), _reasonRest, 0.15f));
            yield return new WaitForSecondsRealtime(0.18f);

            yield return RiseButton(_menuGroup, WinMenuBtn != null ? (RectTransform)WinMenuBtn.transform : null, _menuRest);
            yield return new WaitForSecondsRealtime(0.06f);
            yield return RiseButton(_nextGroup, WinNextBtn != null ? (RectTransform)WinNextBtn.transform : null, _nextRest);
            yield return new WaitForSecondsRealtime(0.06f);
            yield return RiseButton(_repeatGroup, WinRepeatBtn != null ? (RectTransform)WinRepeatBtn.transform : null, _repeatRest);

            RebaseButton(WinMenuBtn); RebaseButton(WinNextBtn); RebaseButton(WinRepeatBtn);
            EnableWinButtons(true);
            _winAnimating = false;
            _winIdle = 0f;
            _winCo = null;
        }

        void PrepareWinContent(int prevBest, bool wasCompleted, ThemeLibrary.Theme theme)
        {
            bool last = _levelId >= SaveStore.LevelCount;
            if (WinTitle != null)
                WinTitle.text = last ? "All Levels Complete!" : "Level Complete!";
            if (WinDetail != null)
                WinDetail.text = last ? $"You solved all {SaveStore.LevelCount} levels" : $"Level {_levelId}";

            int hints = _session != null ? _session.HintCount : 0;
            if (WinReason != null)
                WinReason.text = hints <= 0 ? "No hints used"
                    : hints == 1 ? "1 hint used" : $"{hints} hints used";

            UpdateWinStarColors();

            bool newBest = wasCompleted && _winStarCount > prevBest;
            if (WinNewBest != null) WinNewBest.SetActive(newBest);
            if (WinNewBestLabel != null) WinNewBestLabel.text = "New best!";

            if (WinNextLabel != null) WinNextLabel.text = last ? "Menu" : "Next Level";
            if (WinNextArrow != null) WinNextArrow.gameObject.SetActive(false);
            if (WinMenuBtn != null) WinMenuBtn.gameObject.SetActive(!last);

            if (WinBanner != null && theme != null)
            {
                WinBannerLabel.text = "New theme: " + theme.Name;
                if (BannerSwatches != null)
                    for (int i = 0; i < BannerSwatches.Length && i < theme.Rects.Length; i++)
                        if (BannerSwatches[i] != null
                            && ColorUtility.TryParseHtmlString(theme.Rects[i], out var c))
                            BannerSwatches[i].color = c;
            }
            else if (WinBanner != null) WinBanner.SetActive(false);
        }

        void UpdateWinStarColors()
        {
            if (WinStars == null) return;
            var on = ThemeApplier.ColorOf(ThemeApplier.Role.Primary);
            var off = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            for (int i = 0; i < WinStars.Length; i++)
            {
                var img = WinStars[i];
                if (img == null) continue;
                bool full = i < _winStarCount;
                img.sprite = UiFactory.StarSprite(full);
                img.color = full ? on : off;
                var sh = img.GetComponent<Shadow>();
                if (sh != null)
                {
                    sh.enabled = full;
                    if (full)
                    {
                        sh.effectColor = on;
                        sh.effectDistance = new Vector2(0f, -8f);
                        sh.useGraphicAlpha = false;
                    }
                }
            }
        }

        void ResetWinVisuals()
        {
            if (WinDimGroup != null) WinDimGroup.alpha = 0f;
            if (WinCardGroup != null) WinCardGroup.alpha = 0f;
            if (WinCard != null) WinCard.localScale = Vector3.one * 0.9f;

            SetGroup(_titleGroup, 0f);
            SetGroup(_subGroup, 0f);
            SetGroup(_reasonGroup, 0f);
            SetGroup(_menuGroup, 0f);
            SetGroup(_nextGroup, 0f);
            SetGroup(_repeatGroup, 0f);
            SetGroup(_bestGroup, 0f);
            SetGroup(_bannerGroup, 0f);

            if (WinTitle != null) ((RectTransform)WinTitle.transform).anchoredPosition = _titleRest + new Vector2(0f, 24f);
            if (WinDetail != null) ((RectTransform)WinDetail.transform).anchoredPosition = _subRest + new Vector2(0f, 24f);
            if (WinReason != null) ((RectTransform)WinReason.transform).anchoredPosition = _reasonRest - new Vector2(0f, 24f);
            if (WinMenuBtn != null) ((RectTransform)WinMenuBtn.transform).anchoredPosition = _menuRest - new Vector2(0f, 24f);
            if (WinNextBtn != null) ((RectTransform)WinNextBtn.transform).anchoredPosition = _nextRest - new Vector2(0f, 24f);
            if (WinRepeatBtn != null) ((RectTransform)WinRepeatBtn.transform).anchoredPosition = _repeatRest - new Vector2(0f, 24f);
            if (WinNewBest != null)
            {
                WinNewBest.transform.localScale = Vector3.one * 0.8f;
                ((RectTransform)WinNewBest.transform).anchoredPosition = _bestRest - new Vector2(0f, 24f);
            }

            if (WinStars != null)
                for (int i = 0; i < WinStars.Length; i++)
                    if (WinStars[i] != null) WinStars[i].transform.localScale = Vector3.zero;

            if (WinBanner != null) WinBanner.SetActive(false);
            if (WinNextBtn != null) WinNextBtn.transform.localScale = Vector3.one;
            RebaseButton(WinMenuBtn); RebaseButton(WinNextBtn); RebaseButton(WinRepeatBtn);
            EnableWinButtons(false);
        }

        void FinalizeWinState()
        {
            if (WinDimGroup != null) WinDimGroup.alpha = 1f;
            if (WinCardGroup != null) WinCardGroup.alpha = 1f;
            if (WinCard != null) WinCard.localScale = Vector3.one;

            SetGroup(_titleGroup, 1f);
            SetGroup(_subGroup, 1f);
            SetGroup(_reasonGroup, 1f);
            SetGroup(_menuGroup, 1f);
            SetGroup(_nextGroup, 1f);
            SetGroup(_repeatGroup, 1f);
            SetGroup(_bestGroup, 1f);
            SetGroup(_bannerGroup, 1f);

            if (WinTitle != null) ((RectTransform)WinTitle.transform).anchoredPosition = _titleRest;
            if (WinDetail != null) ((RectTransform)WinDetail.transform).anchoredPosition = _subRest;
            if (WinReason != null) ((RectTransform)WinReason.transform).anchoredPosition = _reasonRest;
            if (WinMenuBtn != null) ((RectTransform)WinMenuBtn.transform).anchoredPosition = _menuRest;
            if (WinNextBtn != null) ((RectTransform)WinNextBtn.transform).anchoredPosition = _nextRest;
            if (WinRepeatBtn != null) ((RectTransform)WinRepeatBtn.transform).anchoredPosition = _repeatRest;
            if (WinNewBest != null)
            {
                WinNewBest.transform.localScale = Vector3.one;
                ((RectTransform)WinNewBest.transform).anchoredPosition = _bestRest;
            }
            if (WinStars != null)
                for (int i = 0; i < WinStars.Length; i++)
                    if (WinStars[i] != null) WinStars[i].transform.localScale = Vector3.one;

            if (_winTheme != null && WinBanner != null)
            {
                bool wasActive = WinBanner.activeSelf;
                if (_bannerCo != null) { StopCoroutine(_bannerCo); _bannerCo = null; }
                WinBanner.SetActive(true);
                if (_bannerGroup != null) _bannerGroup.alpha = 1f;
                if (!wasActive) AudioService.Play("theme_unlock");
                ((RectTransform)WinBanner.transform).anchoredPosition = _bannerRest;
                _bannerCo = StartCoroutine(BannerHideAfter(2.2f));
            }
            else if (WinBanner != null) WinBanner.SetActive(false);

            RebaseButton(WinMenuBtn); RebaseButton(WinNextBtn); RebaseButton(WinRepeatBtn);
            EnableWinButtons(true);
            if (WinNextBtn != null) WinNextBtn.transform.localScale = Vector3.one;
            _winAnimating = false;
            _winIdle = 0f;
            _winCo = null;
        }

        void SkipWinAnimation()
        {
            if (!_winOpen || !_winAnimating) return;
            if (_winCo != null) { StopCoroutine(_winCo); _winCo = null; }
            FinalizeWinState();
        }

        void EnableWinButtons(bool on)
        {
            SetWinButton(WinNextBtn, on);
            SetWinButton(WinMenuBtn, on);
            SetWinButton(WinRepeatBtn, on);
        }

        static void SetWinButton(Button b, bool on)
        {
            if (b == null) return;
            b.interactable = on;
            var cg = b.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = on;   // tombol tidak bisa diklik selama animasi
        }

        static void SetGroup(CanvasGroup g, float a)
        {
            if (g != null) g.alpha = a;
        }

        static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float dur)
        {
            if (g == null) yield break;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            g.alpha = to;
        }

        IEnumerator PanelOpen(float dur)
        {
            var rt = WinCard;
            var cg = WinCardGroup;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                if (rt != null) rt.localScale = Vector3.one * (0.9f + 0.1f * EaseOutBack(k));
                if (cg != null) cg.alpha = k;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
            if (cg != null) cg.alpha = 1f;
        }

        static IEnumerator MoveFadeGroup(CanvasGroup g, RectTransform rt, Vector2 from, Vector2 to, float dur)
        {
            if (g == null || rt == null) yield break;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                g.alpha = k;
                rt.anchoredPosition = Vector2.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }
            g.alpha = 1f;
            rt.anchoredPosition = to;
        }

        static IEnumerator RiseButton(CanvasGroup g, RectTransform rt, Vector2 rest)
        {
            if (g == null || rt == null) yield break;
            var from = rest - new Vector2(0f, 24f);
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.15f);
                g.alpha = k;
                rt.anchoredPosition = Vector2.Lerp(from, rest, k * k * (3f - 2f * k));
                yield return null;
            }
            g.alpha = 1f;
            rt.anchoredPosition = rest;
        }

        static IEnumerator PopStar(RectTransform rt, float dur)
        {
            rt.localScale = Vector3.zero;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                float s = k < 0.5f ? Mathf.Lerp(0f, 1.15f, k / 0.5f)
                                   : Mathf.Lerp(1.15f, 1f, (k - 0.5f) / 0.5f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        IEnumerator PopBest()
        {
            AudioService.Play("newbest");
            var rt = (RectTransform)WinNewBest.transform;
            var g = _bestGroup;
            var from = _bestRest - new Vector2(0f, 24f);
            for (float t = 0f; t < 0.20f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.20f);
                float s = k < 0.5f ? Mathf.Lerp(0.8f, 1.1f, k / 0.5f)
                                   : Mathf.Lerp(1.1f, 1f, (k - 0.5f) / 0.5f);
                rt.localScale = new Vector3(s, s, 1f);
                rt.anchoredPosition = Vector2.Lerp(from, _bestRest, k * k * (3f - 2f * k));
                if (g != null) g.alpha = k;
                yield return null;
            }
            rt.localScale = Vector3.one;
            rt.anchoredPosition = _bestRest;
            if (g != null) g.alpha = 1f;
        }

        IEnumerator StarBurst(RectTransform star)
        {
            if (SaveStore.ReduceMotion) yield break;
            var parent = star.parent;
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("burst", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(20f, 20f);
                rt.anchoredPosition = star.anchoredPosition;
                var im = go.GetComponent<Image>();
                im.sprite = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
                im.type = Image.Type.Sliced;
                im.pixelsPerUnitMultiplier = 56f / 6f;
                im.color = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, i % 6);
                im.raycastTarget = false;
                var cg = go.AddComponent<CanvasGroup>();
                Vector2 dir = new Vector2(Mathf.Cos(i * Mathf.PI / 3f), Mathf.Sin(i * Mathf.PI / 3f));
                Vector2 start = rt.anchoredPosition;
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.Clamp01(t / 0.35f);
                    rt.anchoredPosition = start + dir * (55f * (k * k * (3f - 2f * k)));
                    float s = 1f - 0.5f * k;
                    rt.localScale = new Vector3(s, s, 1f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, 90f * k * (i % 2 == 0 ? 1f : -1f));
                    cg.alpha = 1f - k;
                    yield return null;
                }
                UnityEngine.Object.Destroy(go);
            }
        }

        IEnumerator BannerShow()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (!_winOpen || WinBanner == null) { _bannerCo = null; yield break; }
            WinBanner.SetActive(true);
            if (_bannerGroup != null) _bannerGroup.alpha = 1f;
            AudioService.Play("theme_unlock");
            var rt = (RectTransform)WinBanner.transform;
            var above = _bannerRest + new Vector2(0f, 136f);
            rt.anchoredPosition = above;
            yield return Slide(rt, above, _bannerRest, 0.20f);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Slide(rt, _bannerRest, above, 0.20f);
            WinBanner.SetActive(false);
            _bannerCo = null;
        }

        IEnumerator BannerHideAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (!_winOpen || WinBanner == null) { _bannerCo = null; yield break; }
            var rt = (RectTransform)WinBanner.transform;
            var above = _bannerRest + new Vector2(0f, 136f);
            yield return Slide(rt, _bannerRest, above, 0.20f);
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

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        void CloseWin()
        {
            if (_winCo != null) { StopCoroutine(_winCo); _winCo = null; }
            if (_bannerCo != null) { StopCoroutine(_bannerCo); _bannerCo = null; }
            _winOpen = false;
            _winAnimating = false;
            EnableWinButtons(false);
            if (WinNextBtn != null) WinNextBtn.transform.localScale = Vector3.one;
            if (WinCard != null) WinCard.localScale = Vector3.one;
            if (WinBanner != null) WinBanner.SetActive(false);
            if (WinOverlay != null) WinOverlay.SetActive(false);
            _winTheme = null;
        }

        public void NextLevel()
        {
            if (_levelId >= SaveStore.LevelCount) { GoHome(); return; }
            AudioService.Play("tap");
            CloseWin();
            Show(Mathf.Min(_levelId + 1, SaveStore.LevelCount));
        }

        public void Replay()
        {
            CloseWin();
            Show(_levelId);
            AudioService.Play("tap");
        }

        public void GoHome()
        {
            AudioService.Play("tap");
            CloseWin();
            AppRoot.Instance.OpenHome();
        }

        public void GoLevelSelect()
        {
            CloseWin();
            AppRoot.Instance.OpenLevelSelect();
        }

        public void DoReset()
        {
            if (_session == null) return;
            Board.CancelHint();
            Board.ClearPreview();
            _suppressRemoveFx = true;
            _session.Reset();
            _suppressRemoveFx = false;
            Board.ShowPlaced();
            UpdateHintLabel();
            SavePlay();
            AudioService.Play("tap");
        }

        public void DoHint()
        {
            if (_session == null || _session.IsSolved || _session.HintsLeft <= 0) return;
            if (!_session.TryGetHintTarget(out int ci, out _)) return;
            AudioService.Play("hint");
            var session = _session;
            Board.HintPulse(ci, () => session.ApplyHint(ci));
        }

        public void DoBack()
        {
            AppRoot.Instance.NavBack();
        }

        void Update()
        {
            if (!_winOpen || _winAnimating) return;

            if (!SaveStore.ReduceMotion)
            {
                _winIdle += Time.unscaledDeltaTime;
                if (WinNextBtn != null)
                {
                    float s = 1f;
                    if (_winIdle > 2f)
                        s = 1f + 0.02f * (1f - Mathf.Cos((_winIdle - 2f) * Mathf.PI * 2f / 1.6f));
                    WinNextBtn.transform.localScale = new Vector3(s, s, 1f);
                }
            }

            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame
                || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame))
                NextLevel();
        }
    }
}