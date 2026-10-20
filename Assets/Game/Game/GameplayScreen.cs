using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>
    /// Play screen (scene component). Board on the left/top, control panel on the
    /// right/bottom (adapts to aspect). Static hierarchy and references live in the
    /// scene; only the board interior is built at runtime.
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
        float _lastAspect = -1f;

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
            Layout();
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

        // ----------------------------- layout -----------------------------------

        void Update()
        {
            if (_timerRun && TimeLabel != null)
            {
                _elapsed += Time.unscaledDeltaTime;
                TimeLabel.text = $"{(int)_elapsed / 60}:{(int)_elapsed % 60:D2}";
            }
            float aspect = Screen.width / (float)Screen.height;
            if (!Mathf.Approximately(aspect, _lastAspect)) Layout();
        }

        /// <summary>
        /// Landscape: ~360-unit panel right; portrait: ~300-unit panel bottom, board
        /// fills the rest. Re-anchors slots from the safe-area rect in canvas units.
        /// </summary>
        public void Layout()
        {
            _lastAspect = Screen.width / (float)Screen.height;
            var area = Panel.parent as RectTransform;   // Screens root == safe area
            if (area == null || BoardSlot == null) return;
            float uw = area.rect.width;
            float uh = area.rect.height;
            bool portrait = uh > uw;

            if (portrait)
            {
                float ph = Mathf.Min(300f, uh * 0.3f);
                Stretches(BoardSlot, new Vector2(0, ph), Vector2.zero);
                Panel.anchorMin = Vector2.zero;
                Panel.anchorMax = Vector2.one;
                Panel.offsetMin = Vector2.zero;
                Panel.offsetMax = new Vector2(0, -ph);
                PlaceHUDBottom(ph, uw);
            }
            else
            {
                float pw = Mathf.Min(360f, uw * 0.42f);
                Stretches(BoardSlot, Vector2.zero, new Vector2(-pw, 0));
                Panel.anchorMin = new Vector2(1, 0);
                Panel.anchorMax = new Vector2(1, 1);
                Panel.offsetMin = new Vector2(-pw, 0);
                Panel.offsetMax = Vector2.zero;
                PlaceHUDRight(pw);
            }
            Board.RebuildIfReady();
        }

        static void Stretches(RectTransform rt, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        void PlaceHUDBottom(float ph, float panelW)
        {
            Anchor(LevelLabel.rectTransform, 0.5f, 0.86f, panelW - 24f, 44f);
            Anchor(ProgressLabel.rectTransform, 0.30f, 0.86f, 140f, 44f);
            Anchor(TimeLabel.rectTransform, 0.70f, 0.86f, 140f, 44f);

            float bw = Mathf.Min(panelW * 0.46f, 180f), bh = Mathf.Min(ph * 0.3f, 62f);
            AnchorBtn(UndoBtn, 0.26f, 0.52f, bw, bh);
            AnchorBtn(ResetBtn, 0.74f, 0.52f, bw, bh);
            AnchorBtn(HintBtn, 0.26f, 0.18f, bw, bh);
            AnchorBtn(BackBtn, 0.74f, 0.18f, bw, bh);
        }

        void PlaceHUDRight(float pw)
        {
            Anchor(LevelLabel.rectTransform, 0.5f, 0.92f, pw - 24f, 54f);
            Anchor(ProgressLabel.rectTransform, 0.5f, 0.80f, pw - 24f, 40f);
            Anchor(TimeLabel.rectTransform, 0.5f, 0.70f, pw - 24f, 36f);

            float bw = pw - 36f, bh = 62f;
            AnchorBtn(UndoBtn, 0.5f, 0.52f, bw, bh);
            AnchorBtn(ResetBtn, 0.5f, 0.38f, bw, bh);
            AnchorBtn(HintBtn, 0.5f, 0.24f, bw, bh);
            AnchorBtn(BackBtn, 0.5f, 0.10f, bw, bh);
        }

        static void Anchor(RectTransform rt, float ax, float ay, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        static void AnchorBtn(Button b, float ax, float ay, float w, float h) =>
            Anchor(b.GetComponent<RectTransform>(), ax, ay, w, h);
    }
}
