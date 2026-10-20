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

        void Awake()
        {
            UndoBtn.onClick.AddListener(DoUndo);
            ResetBtn.onClick.AddListener(DoReset);
            HintBtn.onClick.AddListener(DoHint);
            BackBtn.onClick.AddListener(DoBack);
            WinNextBtn.onClick.AddListener(NextLevel);
            WinMenuBtn.onClick.AddListener(GoLevelSelect);

            ThemeApplier.Subscribe(ThemeApplier.Role.Text, c =>
            {
                LevelLabel.color = c;
                ProgressLabel.color = c;
                TimeLabel.color = c;
                WinTitle.color = c;
                WinDetail.color = c;
            }, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _ => Board.ApplyTheme(), false);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _ => Board.ApplyTheme(), false);
            ThemeApplier.Subscribe(ThemeApplier.Role.Rect, _ => Board.ApplyTheme(), false);
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

        void OnRectPlaced(int ci, RectI r) => AudioService.Play("place");
        void OnRectRemoved(int ci, RectI r) => AudioService.Play("tap");
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
            WinDetail.text = Stars(best)
                + $"\nWaktu {TimeSpan.FromSeconds(_elapsed):mm\\:ss}"
                + (wasUnlocking ? "\nLevel baru terbuka!" : "");
            WinNextBtn.gameObject.SetActive(_levelId < SaveStore.LevelCount);
            WinOverlay.SetActive(true);
            LevelFinished?.Invoke(_levelId);
            if (wasUnlocking) AudioService.Play("unlock");
        }

        static string Stars(int n)
        {
            var s = "";
            for (int i = 0; i < 3; i++) s += i < n ? "\u2605" : "\u2606";
            return s;
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

        public void DoUndo() { _session.Undo(); Board.ShowPlaced(); UpdateProgress(); }
        public void DoReset() { _session.Reset(); Board.ShowPlaced(); UpdateProgress(); }

        public void DoHint()
        {
            if (_session == null || _session.IsSolved) return;
            _session.Hint();
            Board.ShowPlaced();
            UpdateProgress();
        }

        public void DoBack()
        {
            _timerRun = false;
            AppRoot.Instance.OpenHome();
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
