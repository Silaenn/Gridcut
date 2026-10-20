using System;
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
        public Button UndoBtn, ResetBtn, HintBtn, BackBtn;
        public GameObject WinOverlay;
        public TMP_Text WinTitle, WinDetail;
        public Button WinNextBtn, WinMenuBtn;

        public event Action<int> LevelFinished;   // levelId, raised once on win

        PuzzleSession _session;
        int _levelId;
        float _elapsed;
        bool _timerRun;
        Action<Color> _textTint, _boardTint;
        RectTransform _winStars;

        void Awake()
        {
            AutoWire();
            UndoBtn.onClick.AddListener(DoUndo);
            ResetBtn.onClick.AddListener(DoReset);
            HintBtn.onClick.AddListener(DoHint);
            BackBtn.onClick.AddListener(DoBack);
            WinNextBtn.onClick.AddListener(NextLevel);
            WinMenuBtn.onClick.AddListener(GoLevelSelect);

            _textTint = c =>
            {
                if (LevelLabel != null) LevelLabel.color = c;
                if (ProgressLabel != null) ProgressLabel.color = c;
                if (TimeLabel != null) TimeLabel.color = c;
                if (WinTitle != null) WinTitle.color = c;
                if (WinDetail != null) WinDetail.color = c;
            };
            // Rebuild() is a no-op while Session is unset, so applyImmediately is safe
            _boardTint = _ => Board.ApplyTheme();

            // win stars as sprites above the bottom-aligned detail text
            // (LiberationSans SDF has no ★/☆ glyphs)
            WinDetail.alignment = TextAlignmentOptions.Bottom;
            _winStars = UiFactory.StarRow(WinDetail.transform, "WinStars");
            _winStars.anchorMin = _winStars.anchorMax = new Vector2(0.5f, 1f);
            _winStars.pivot = new Vector2(0.5f, 1f);
            _winStars.sizeDelta = new Vector2(340f, 40f);
            _winStars.anchoredPosition = new Vector2(0f, -2f);
            _winStars.gameObject.SetActive(false);
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
            if (UndoBtn == null) UndoBtn = UiFactory.Wire<Button>(this, null, "Panel/UndoBtn");
            if (ResetBtn == null) ResetBtn = UiFactory.Wire<Button>(this, null, "Panel/ResetBtn");
            if (HintBtn == null) HintBtn = UiFactory.Wire<Button>(this, null, "Panel/HintBtn");
            if (BackBtn == null) BackBtn = UiFactory.Wire<Button>(this, null, "Panel/BackBtn");
            if (WinOverlay == null) WinOverlay = UiFactory.WireGo(this, null, "WinOverlay");
            if (WinTitle == null) WinTitle = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/WinTitle");
            if (WinDetail == null) WinDetail = UiFactory.Wire<TMP_Text>(this, null, "WinOverlay/Card/WinDetail");
            if (WinNextBtn == null) WinNextBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/WinNextBtn");
            if (WinMenuBtn == null) WinMenuBtn = UiFactory.Wire<Button>(this, null, "WinOverlay/Card/WinMenuBtn");
        }

        // ------------------------------ flow -----------------------------------

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
            }
            _session = PuzzleSession.Make(level);
            _session.RectPlaced += OnRectPlaced;
            _session.Invalid += OnInvalid;
            _session.RectRemoved += OnRectRemoved;
            _session.SolvedChanged += OnSolved;

            Board.SetLevel(_session);
            _elapsed = 0f;
            _timerRun = true;
            LevelLabel.text = $"Level {_levelId}" + (level.IsTutorial ? " \u00b7 Latihan" : "");
            TimeLabel.text = "0:00";
            UpdateProgress();
            WinOverlay.SetActive(false);
        }

        void OnRectPlaced(int ci, RectI r)
        {
            Board.ShowPlaced();
            UpdateProgress();
            AudioService.Play("place");
        }

        void OnRectRemoved(int ci, RectI r)
        {
            Board.ShowPlaced();
            UpdateProgress();
            AudioService.Play("tap");
        }

        void OnInvalid() => AudioService.Play("invalid");

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
            int best = SaveStore.BestStars(_levelId);

            WinTitle.text = best == 3 ? "Sempurna!" : "Selesai!";
            WinDetail.text = $"Waktu {TimeSpan.FromSeconds(_elapsed):mm\\:ss}"
                + (wasUnlocking ? "\nLevel baru terbuka!" : "");
            _winStars.gameObject.SetActive(true);
            UiFactory.SetStars(_winStars, best,
                ThemeApplier.ColorOf(ThemeApplier.Role.Accent),
                new Color(0.5f, 0.5f, 0.5f, 0.35f));
            WinNextBtn.gameObject.SetActive(_levelId < SaveStore.LevelCount);
            WinOverlay.SetActive(true);
            LevelFinished?.Invoke(_levelId);
            if (wasUnlocking) AudioService.Play("unlock");
        }

        public void NextLevel()
        {
            WinOverlay.SetActive(false);
            Show(Mathf.Min(_levelId + 1, SaveStore.LevelCount));
        }

        public void GoLevelSelect()
        {
            WinOverlay.SetActive(false);
            AppRoot.Instance.OpenLevelSelect();
        }

        public void DoUndo() => _session.Undo();
        public void DoReset() => _session.Reset();

        public void DoHint()
        {
            if (_session == null || _session.IsSolved) return;
            _session.Hint();
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
