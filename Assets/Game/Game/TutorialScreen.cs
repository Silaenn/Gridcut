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
    /// Mode tutorial 5 pelajaran (spec bagian 13). Memakai BoardView/PuzzleSession
    /// yang sama dengan gameplay, dengan gerbang input, demo, progress 5 kotak,
    /// Continue, dan layar penutup. Progres tutorial tidak disimpan; hanya tutorialDone.
    /// </summary>
    public class TutorialScreen : MonoBehaviour
    {
        [Header("Wired in scene")]
        public BoardView Board;
        public RectTransform BoardSlot;
        public Button BackBtn, ResetBtn, SkipBtn, ContinueBtn, HintBtn;
        public RectTransform InstructionPill;
        public TMP_Text InstructionText;
        public RectTransform DotsRow;
        public Image[] Dots = new Image[5];
        public GameObject ReadyOverlay;
        public Button ReadyPlay;
        public TMP_Text ReadyTitle;
        public TMP_Text[] ReadyRows = new TMP_Text[3];

        [Header("Sprites")]
        public Sprite CircleSprite, RingSprite, ArrowSprite;

        public event Action<int> TutorialStepCompleted;

        TutorialLesson[] _lessons;
        int _index;
        PuzzleSession _session;
        TutorialLesson Lesson => _lessons != null && _index < _lessons.Length ? _lessons[_index] : null;

        bool _stepDone, _tapDone, _hintPrompt;
        float _idle;
        Coroutine _demoCo, _continueCo, _popCo, _hintPulseCo, _riseCo;

        RectTransform _demoRoot;
        Image _targetRing, _ghostRing, _ghostFill, _halo, _staticArrow;
        RectTransform _ghostRt, _trailRoot;
        Vector2 _demoA, _demoB;
        bool _demoTap;
        readonly List<Image> _dashes = new List<Image>(24);

        Image _badgeBack, _badgeDisc;
        TMP_Text _badgeCount;
        Image _lamp;

        bool _repaintPending;
        Action<Color> _repaintCb;

        void Awake()
        {
            AutoWire();
            Add(BackBtn, GoBack);
            Add(ResetBtn, ResetLesson);
            Add(SkipBtn, Skip);
            Add(ContinueBtn, ContinuePressed);
            Add(HintBtn, DoHint);
            Add(ReadyPlay, ReadyPlayPressed);
            WireHintBadge();
            if (Board != null)
            {
                Board.DragStarted += OnDragStart;
                Board.Rejected += OnRejected;
            }
        }

        void Start()
        {
            if (SkipBtn != null)
            {
                var t = SkipBtn.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.text = "Skip";
            }
            if (ContinueBtn != null)
            {
                var t = ContinueBtn.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.text = "Continue";
            }
        }

        static void Add(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        void AutoWire()
        {
            if (Board == null) Board = UiFactory.Wire<BoardView>(this, null, "BoardSlot/Board");
            if (BoardSlot == null) BoardSlot = UiFactory.Wire<RectTransform>(this, null, "BoardSlot");
            if (BackBtn == null) BackBtn = UiFactory.Wire<Button>(this, null, "BackBtn");
            if (ResetBtn == null) ResetBtn = UiFactory.Wire<Button>(this, null, "ResetBtn");
            if (SkipBtn == null) SkipBtn = UiFactory.Wire<Button>(this, null, "SkipBtn");
            if (ContinueBtn == null) ContinueBtn = UiFactory.Wire<Button>(this, null, "ContinueBtn");
            if (HintBtn == null) HintBtn = UiFactory.Wire<Button>(this, null, "HintBtn");
            if (InstructionPill == null) InstructionPill = UiFactory.Wire<RectTransform>(this, null, "InstructionPill");
            if (InstructionText == null) InstructionText = UiFactory.Wire<TMP_Text>(this, null, "InstructionPill/Text");
            if (DotsRow == null) DotsRow = UiFactory.Wire<RectTransform>(this, null, "Dots");
            if (ReadyOverlay == null) ReadyOverlay = UiFactory.WireGo(this, null, "TutorialReady");
            if (ReadyOverlay == null)
            {
                var root = transform.root.Find("Overlay/TutorialReady");
                if (root != null) ReadyOverlay = root.gameObject;
            }
            if (ReadyPlay == null) ReadyPlay = UiFactory.Wire<Button>(this, null, "TutorialReady/Card/PlayBtn");
            if (ReadyTitle == null) ReadyTitle = UiFactory.Wire<TMP_Text>(this, null, "TutorialReady/Card/Title");
            if (Dots == null || Dots.Length < 5)
            {
                var list = new List<Image>();
                if (DotsRow != null)
                    for (int i = 0; i < DotsRow.childCount; i++)
                    {
                        var im = DotsRow.GetChild(i).GetComponent<Image>();
                        if (im != null) list.Add(im);
                    }
                Dots = list.ToArray();
            }
        }

        void WireHintBadge()
        {
            var badge = HintBtn != null ? HintBtn.transform.Find("HintBadge") : null;
            if (badge != null)
            {
                _badgeBack = badge.GetComponent<Image>();
                var d = badge.Find("Disc"); _badgeDisc = d != null ? d.GetComponent<Image>() : null;
                var c = badge.Find("Count"); _badgeCount = c != null ? c.GetComponent<TMP_Text>() : null;
            }
            _lamp = HintBtn != null && HintBtn.transform.Find("LampIcon") != null
                ? HintBtn.transform.Find("LampIcon").GetComponent<Image>() : null;
        }

        // ------------------------------ lifecycle ------------------------------

        public void Begin()
        {
            LoadLessons();
            if (_lessons == null || _lessons.Length == 0) return;
            SetLesson(0);
        }

        void LoadLessons()
        {
            if (_lessons != null && _lessons.Length > 0) return;
            var all = Resources.LoadAll<TutorialLesson>("Tutorial");
            Array.Sort(all, (a, b) => a.Index.CompareTo(b.Index));
            _lessons = all;
            if (_lessons == null || _lessons.Length == 0)
                Debug.LogError("[Tutorial] Aset Resources/Tutorial tidak ditemukan");
        }

        void SetLesson(int index)
        {
            if (_lessons == null || _lessons.Length == 0) return;
            StopFlows();
            _index = Mathf.Clamp(index, 0, _lessons.Length - 1);
            _stepDone = false;
            _tapDone = false;
            _hintPrompt = false;
            _idle = 0f;

            var lesson = Lesson;
            var level = lesson.BuildLevel();
            _session = PuzzleSession.Make(level);
            _session.Restore(0, lesson.BuildInitial());
            _session.RectPlaced += OnRectPlaced;
            _session.RectRemoved += OnRectRemoved;
            _session.HintUsed += OnHintUsed;

            Board.SetTutorialLayout(true);
            Board.SetLevel(_session);
            Board.PlaceGate = GatePlace;
            Board.TapGate = GateTap;

            SetInstruction(lesson.Instruction);
            SetBottomSlot(lesson.HintEnabled);
            UpdateDots();
            UpdateHint();
            if (ContinueBtn != null) ContinueBtn.gameObject.SetActive(false);
            if (HintBtn != null)
            {
                HintBtn.gameObject.SetActive(lesson.HintEnabled);
                HintBtn.transform.localScale = Vector3.one;
            }

            BuildDemoVisuals();
            if (lesson.HasDemo) _demoCo = StartCoroutine(DemoLoop());
        }

        void StopFlows()
        {
            if (_demoCo != null) { StopCoroutine(_demoCo); _demoCo = null; }
            if (_continueCo != null) { StopCoroutine(_continueCo); _continueCo = null; }
            if (_riseCo != null) { StopCoroutine(_riseCo); _riseCo = null; }
            if (_popCo != null) { StopCoroutine(_popCo); _popCo = null; }
            if (_hintPulseCo != null) { StopCoroutine(_hintPulseCo); _hintPulseCo = null; }
            if (_session != null)
            {
                _session.RectPlaced -= OnRectPlaced;
                _session.RectRemoved -= OnRectRemoved;
                _session.HintUsed -= OnHintUsed;
            }
        }

        void OnEnable()
        {
            if (_repaintCb == null) _repaintCb = _ => _repaintPending = true;
            ThemeApplier.Subscribe(ThemeApplier.Role.Text, _repaintCb, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.GridLine, _repaintCb, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.CellEmpty, _repaintCb, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.Primary, _repaintCb, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.Accent, _repaintCb, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.Rect, _repaintCb, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.RectEdge, _repaintCb, false);
            ThemeApplier.Subscribe(ThemeApplier.Role.Board, _repaintCb, false);
            _repaintPending = true;
        }

        void LateUpdate()
        {
            if (!_repaintPending) return;
            _repaintPending = false;
            RepaintTheme();
        }

        void RepaintTheme()
        {
            var text = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            var edge = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            var surface = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            var primary = ThemeApplier.ColorOf(ThemeApplier.Role.Primary);
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);

            UiFactory.FrameTint(BackBtn, true);
            UiFactory.FrameTint(ResetBtn, true);
            UiFactory.FrameTint(SkipBtn, false);
            if (SkipBtn != null)
            {
                var t = SkipBtn.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.color = text;
            }

            if (InstructionPill != null)
            {
                var img = InstructionPill.GetComponent<Image>();
                if (img != null) img.color = edge;
                UiFactory.ApplyShadow(InstructionPill, new Vector2(0f, -12f), edge);
                var fill = InstructionPill.Find("Fill");
                var fi = fill != null ? fill.GetComponent<Image>() : null;
                if (fi != null) fi.color = surface;
            }
            if (InstructionText != null) InstructionText.color = text;

            UiFactory.PrimaryTint(ContinueBtn, -16f);
            UiFactory.PrimaryTint(HintBtn, -16f);
            UiFactory.PrimaryTint(ReadyPlay, -16f);

            if (_badgeBack != null) _badgeBack.color = edge;
            if (_badgeDisc != null) _badgeDisc.color = accent;
            if (_badgeCount != null) _badgeCount.color = ThemeApplier.ContrastTextOn(accent);
            if (_lamp != null) _lamp.color = ThemeApplier.ContrastTextOn(primary);

            if (ReadyOverlay != null)
            {
                var card = ReadyOverlay.transform.Find("Card");
                if (card != null) UiFactory.CardTint(card, -20f);
                if (ReadyTitle != null) ReadyTitle.color = text;
                if (ReadyRows != null)
                    for (int i = 0; i < ReadyRows.Length; i++)
                        if (ReadyRows[i] != null) ReadyRows[i].color = text;
                RecolorReadyIcon(card, "Row0/Icon", text);
                RecolorReadyIcon(card, "Row1/Icon/Ring", text);
                RecolorReadyIcon(card, "Row1/Icon/Dot", text);
                RecolorReadyIcon(card, "Row2/Icon", primary);
            }

            UpdateDots();

            if (Board != null && Board.Session != null)
            {
                Board.ApplyTheme();   // membangun ulang papan + membersihkan lapisan Demo
                BuildDemoVisuals();
            }
        }

        static void RecolorReadyIcon(Transform card, string path, Color c)
        {
            if (card == null) return;
            var t = card.Find(path);
            var im = t != null ? t.GetComponent<Image>() : null;
            if (im != null) im.color = c;
        }

        void OnDisable()
        {
            StopFlows();
            if (_repaintCb != null) ThemeApplier.Unsubscribe(_repaintCb);
        }

        void Update()
        {
            if (Lesson == null || _stepDone) return;
            _idle += Time.unscaledDeltaTime;

            if (Lesson.HintEnabled && !_hintPrompt && _idle >= Lesson.HintDelay)
            {
                _hintPrompt = true;
                SetInstruction("Stuck? Tap Hint.");
                if (_hintPulseCo != null) StopCoroutine(_hintPulseCo);
                _hintPulseCo = StartCoroutine(HintPulse());
            }
        }

        // ------------------------------ input gate ------------------------------

        bool GatePlace(RectI rect)
        {
            if (_stepDone) return false;
            if (_index == 4) return true;             // lesson 5 bebas
            if (_index == 3 && !_tapDone) return false; // lesson 4: tap dulu
            var sols = Lesson.BuildSolution();
            for (int i = 0; i < sols.Length; i++)
                if (Same(sols[i], rect) && !IsPlaced(sols[i])) return true;
            return false;
        }

        bool GateTap(int col, int row)
        {
            if (_stepDone) return false;
            if (_index == 4) return true;
            if (_index == 3 && !_tapDone)
            {
                var init = Lesson.BuildInitial();
                for (int i = 0; i < init.Length; i++)
                    if (init[i].Contains(col, row)) return true;
            }
            return false;
        }

        static bool Same(in RectI a, in RectI b) =>
            a.X == b.X && a.Y == b.Y && a.W == b.W && a.H == b.H;

        bool IsPlaced(in RectI rect)
        {
            for (int i = 0; i < _session.Placed.Count; i++)
                if (Same(_session.Placed[i], rect)) return true;
            return false;
        }

        void OnRectPlaced(int owner, RectI rect)
        {
            if (_stepDone) return;
            AudioService.Play("place");
            if (Board != null) Board.ShowPlaced();
            if (_session.IsSolved) { CompleteStep(); return; }
            // target berikutnya: sorotan & demo pindah ke keping yang belum terpasang
            BuildDemoVisuals();
            _idle = 0f;
        }

        void OnRectRemoved(int owner, RectI rect)
        {
            AudioService.Play("tap");
            if (Board != null) Board.ShowPlaced();
            // Lesson 4: keping salah dihapus → lanjut menyelesaikan papan
            if (_index == 3 && !_tapDone)
            {
                _tapDone = true;
                SetInstruction(string.IsNullOrEmpty(Lesson.AfterRemoval)
                    ? "Now finish the puzzle." : Lesson.AfterRemoval);
                BuildDemoVisuals();
                _idle = 0f;
            }
        }

        void OnHintUsed(int clue) => UpdateHint();

        void OnDragStart()
        {
            AudioService.Play("drag");
            OnTouch();
        }

        void OnRejected()
        {
            AudioService.Play("invalid");
            OnTouch();
        }

        void OnTouch()
        {
            _idle = 0f;
            HideDemoVisuals();
            if (_hintPrompt)
            {
                _hintPrompt = false;
                SetInstruction(Lesson != null ? Lesson.Instruction : "");
                StopHintPulse();
            }
        }

        // ------------------------------ success / flow ------------------------------

        void CompleteStep()
        {
            _stepDone = true;
            Board.PlaceGate = _ => false;
            Board.TapGate = (_, __) => false;
            HideDemoVisuals();
            HideTarget();
            StopHintPulse();
            SetInstruction(Lesson.Success);
            AudioService.Play("step");
            if (_popCo != null) StopCoroutine(_popCo);
            _popCo = StartCoroutine(PopDot(_index));
            TutorialStepCompleted?.Invoke(_index + 1);

            if (HintBtn != null) HintBtn.gameObject.SetActive(false);
            if (_index == 4)
            {
                if (_continueCo != null) StopCoroutine(_continueCo);
                _continueCo = StartCoroutine(ReadySoon());
            }
            else
            {
                if (_continueCo != null) StopCoroutine(_continueCo);
                _continueCo = StartCoroutine(ShowContinueSoon());
            }
        }

        IEnumerator ShowContinueSoon()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            _continueCo = null;
            ShowContinue(true);
        }

        IEnumerator ReadySoon()
        {
            yield return new WaitForSecondsRealtime(0.7f);
            ShowReady();
            _continueCo = null;
        }

        void ContinuePressed()
        {
            if (_stepDone && _index >= 4) { ShowReady(); return; }
            if (_index + 1 < _lessons.Length) SetLesson(_index + 1);
        }

        void ShowReady()
        {
            SaveStore.SetTutorialDone(true);
            var p = ReadyOverlay != null ? ReadyOverlay.GetComponent<PopupView>() : null;
            if (p != null) p.Show();
            else if (ReadyOverlay != null) ReadyOverlay.SetActive(true);
            AudioService.Play("step");
        }

        void Skip()
        {
            SaveStore.SetTutorialDone(true);
            AudioService.Play("tap");
            AppRoot.Instance.StartLevel(1);
        }

        void GoBack()
        {
            AudioService.Play("tap");
            AppRoot.Instance.NavBack();
        }

        void ResetLesson()
        {
            if (Lesson == null) return;
            AudioService.Play("tap");
            SetLesson(_index);
        }

        void ReadyPlayPressed()
        {
            var p = ReadyOverlay != null ? ReadyOverlay.GetComponent<PopupView>() : null;
            if (p != null) p.Hide();
            else if (ReadyOverlay != null) ReadyOverlay.SetActive(false);
            AppRoot.Instance.StartLevel(1);
        }

        // ------------------------------ hint ----------------------------------

        void UpdateHint()
        {
            if (HintBtn == null || _session == null) return;
            int left = _session.HintsLeft;
            HintBtn.interactable = Lesson != null && Lesson.HintEnabled && left > 0 && !_stepDone;
            if (_badgeCount != null) _badgeCount.text = left.ToString();
            var lbl = HintBtn.transform.Find("Label");
            var t = lbl != null ? lbl.GetComponent<TMP_Text>() : null;
            if (t != null) t.text = "Hint";
        }

        void DoHint()
        {
            if (_index != 4 || _session == null || _session.IsSolved || _session.HintsLeft <= 0) return;
            if (!_session.TryGetHintTarget(out int ci, out _)) return;
            AudioService.Play("tap");
            _idle = 0f;
            var session = _session;
            Board.HintPulse(ci, () => session.ApplyHint(ci));
            if (_hintPrompt)
            {
                _hintPrompt = false;
                SetInstruction(Lesson.Instruction);
                StopHintPulse();
            }
        }

        IEnumerator HintPulse()
        {
            if (HintBtn == null) yield break;
            if (SaveStore.ReduceMotion) yield break;
            while (!_stepDone)
            {
                for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
                {
                    float s = 1f + 0.06f * Mathf.Sin((t / 1.2f) * Mathf.PI);
                    HintBtn.transform.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
                HintBtn.transform.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            HintBtn.transform.localScale = Vector3.one;
        }

        void StopHintPulse()
        {
            if (_hintPulseCo != null) { StopCoroutine(_hintPulseCo); _hintPulseCo = null; }
            if (HintBtn != null) HintBtn.transform.localScale = Vector3.one;
        }

        // ------------------------------ layout ------------------------------

        void SetInstruction(string text)
        {
            if (InstructionText == null || InstructionPill == null) return;
            // lebar dihitung dari teks tanpa batas kolom lama, supaya pil tidak
            // "terkunci" pada lebar kalimat sebelumnya saat teks berganti
            float pref = InstructionText.GetPreferredValues(text).x;
            InstructionText.text = text;
            float w = Mathf.Min(1120f, pref + 96f);
            InstructionPill.sizeDelta = new Vector2(Mathf.Max(360f, w), 144f);
            InstructionText.ForceMeshUpdate();
        }

        void SetBottomSlot(bool hint)
        {
            if (Board == null) return;
            float boardBottomScreen = 540f - (Board.BoardCenterY - Board.BoardHeight * 0.5f);
            // lesson 5: kotak progres 60 px di bawah Hint; rapatkan slot 20 px supaya
            // tombol + bayangannya tidak menimpa kotak (spesifikasi 964 bertabrakan
            // dengan slot Hint pada papan 4x4).
            float top = boardBottomScreen + (hint ? 24f : 44f);
            var cRt = ContinueBtn != null ? (RectTransform)ContinueBtn.transform : null;
            if (cRt != null)
            {
                cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 1f);
                cRt.pivot = new Vector2(0.5f, 1f);
                cRt.anchoredPosition = new Vector2(0f, -top);
            }
            var hRt = HintBtn != null ? (RectTransform)HintBtn.transform : null;
            if (hRt != null)
            {
                hRt.anchorMin = hRt.anchorMax = new Vector2(0.5f, 1f);
                hRt.pivot = new Vector2(0.5f, 1f);
                hRt.anchoredPosition = new Vector2(0f, -top);
                if (hint)
                {
                    // badge tetap di sudut kanan-atas, tetapi masuk sedikit supaya
                    // tidak menyentuh tepi bawah papan pada slot yang dirapatkan
                    var badge = hRt.Find("HintBadge") as RectTransform;
                    if (badge != null) badge.anchoredPosition = new Vector2(0f, -12f);
                }
            }
            if (DotsRow != null)
                DotsRow.anchoredPosition = new Vector2(0f, -(hint ? 1010f : 964f));
            ShowContinue(false);
        }

        void ShowContinue(bool show)
        {
            if (ContinueBtn == null) return;
            var rt = (RectTransform)ContinueBtn.transform;
            var cg = ContinueBtn.GetComponent<CanvasGroup>();
            if (!show)
            {
                ContinueBtn.gameObject.SetActive(false);
                if (cg != null) cg.alpha = 0f;
                return;
            }
            ContinueBtn.gameObject.SetActive(true);
            float y = rt.anchoredPosition.y;
            var rest = new Vector2(0f, y);
            if (SaveStore.ReduceMotion)
            {
                rt.anchoredPosition = rest;
                if (cg != null) cg.alpha = 1f;
                return;
            }
            var from = new Vector2(0f, y - 40f);
            rt.anchoredPosition = from;
            if (cg != null) cg.alpha = 0f;
            if (_riseCo != null) StopCoroutine(_riseCo);
            _riseCo = StartCoroutine(Rise(rt, cg, from, rest));
        }

        IEnumerator Rise(RectTransform rt, CanvasGroup cg, Vector2 from, Vector2 to)
        {
            const float dur = 0.25f;
            for (float t = 0f; t < dur;)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                rt.anchoredPosition = Vector2.Lerp(from, to, k * k * (3f - 2f * k));
                if (cg != null) cg.alpha = k;
                yield return null;
            }
            rt.anchoredPosition = to;
            if (cg != null) cg.alpha = 1f;
            _riseCo = null;
        }

        void UpdateDots()
        {
            if (Dots == null) return;
            for (int i = 0; i < Dots.Length && i < 5; i++)
            {
                var dot = Dots[i];
                if (dot == null) continue;
                bool done = i < _index;
                bool current = i == _index;
                dot.color = (done || current)
                    ? ThemeApplier.ColorOf(ThemeApplier.Role.Rect, i % 6)
                    : ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                dot.rectTransform.localScale = Vector3.one * (current ? 1.15f : 1f);
                var sh = dot.GetComponent<Shadow>();
                if (sh != null)
                {
                    sh.enabled = current;
                    sh.effectColor = ThemeApplier.ColorOf(ThemeApplier.Role.RectEdge, i % 6);
                    sh.effectDistance = new Vector2(0f, -8f);
                    sh.useGraphicAlpha = false;
                }
            }
        }

        IEnumerator PopDot(int idx)
        {
            if (Dots == null || idx >= Dots.Length || Dots[idx] == null) yield break;
            var rt = Dots[idx].rectTransform;
            if (SaveStore.ReduceMotion)
            {
                rt.localScale = Vector3.one * 1.15f;
                _popCo = null;
                yield break;
            }
            for (float t = 0f; t < 0.22f;)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.22f);
                float s = k < 0.5f
                    ? Mathf.Lerp(1f, 1.4f, k / 0.5f)
                    : Mathf.Lerp(1.4f, 1.15f, (k - 0.5f) / 0.5f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one * 1.15f;
            _popCo = null;
        }

        // ------------------------------ demo ------------------------------

        RectI NextTargetRect()
        {
            if (_index == 3 && !_tapDone)
            {
                var init = Lesson.BuildInitial();
                return init.Length > 0 ? init[0] : default;
            }
            var sols = Lesson.BuildSolution();
            for (int i = 0; i < sols.Length; i++)
                if (!IsPlaced(sols[i])) return sols[i];
            return sols.Length > 0 ? sols[sols.Length - 1] : default;
        }

        void BuildDemoVisuals()
        {
            HideDemoVisuals();
            HideTarget();
            if (Board == null || Board.DemoLayer == null || Lesson == null) return;
            _demoRoot = Board.DemoLayer;
            for (int i = _demoRoot.childCount - 1; i >= 0; i--)
                Destroy(_demoRoot.GetChild(i).gameObject);
            _targetRing = null; _ghostRing = null; _ghostFill = null;
            _ghostRt = null; _halo = null; _staticArrow = null;
            _trailRoot = null;
            _dashes.Clear();

            var target = NextTargetRect();
            _demoTap = _index == 3 && !_tapDone;
            if (_index < 4)
            {
                var tSize = Board.RectSizeLocal(target) + new Vector2(16f, 16f);
                var tRt = NewRt(_demoRoot, "TargetRing");
                tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 0.5f);
                tRt.sizeDelta = tSize;
                tRt.anchoredPosition = Board.RectCenterLocal(target);
                _targetRing = tRt.gameObject.AddComponent<Image>();
                _targetRing.sprite = RingSprite;
                _targetRing.type = Image.Type.Sliced;
                _targetRing.fillCenter = false;
                _targetRing.pixelsPerUnitMultiplier = 56f / 36f;
                _targetRing.color = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
                _targetRing.raycastTarget = false;
            }

            if (!Lesson.HasDemo) return;

            if (_demoTap)
                _demoA = _demoB = Board.RectCenterLocal(target);
            else
            {
                _demoA = Board.CellCenterLocal(target.X, target.Y);
                _demoB = Board.CellCenterLocal(target.Right - 1, target.Bottom - 1);
            }

            // ghost fill + ring + halo
            var haloRt = NewRt(_demoRoot, "Halo");
            haloRt.anchorMin = haloRt.anchorMax = new Vector2(0.5f, 0.5f);
            haloRt.sizeDelta = new Vector2(144f, 144f);
            _halo = haloRt.gameObject.AddComponent<Image>();
            _halo.sprite = CircleSprite;
            _halo.color = new Color(ThemeApplier.ColorOf(ThemeApplier.Role.Accent).r,
                ThemeApplier.ColorOf(ThemeApplier.Role.Accent).g,
                ThemeApplier.ColorOf(ThemeApplier.Role.Accent).b, 0.25f);
            _halo.raycastTarget = false;
            _halo.gameObject.SetActive(false);

            var ghostRt = NewRt(_demoRoot, "Ghost");
            ghostRt.anchorMin = ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRt.sizeDelta = new Vector2(96f, 96f);
            _ghostRt = ghostRt;
            var fillRt = NewRt(ghostRt, "Fill");
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            _ghostFill = fillRt.gameObject.AddComponent<Image>();
            _ghostFill.sprite = CircleSprite;
            var sc = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty); sc.a = 0.7f;
            _ghostFill.color = sc;
            _ghostFill.raycastTarget = false;
            var ringRt = NewRt(ghostRt, "Ring");
            ringRt.anchorMin = Vector2.zero; ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = Vector2.zero; ringRt.offsetMax = Vector2.zero;
            _ghostRing = ringRt.gameObject.AddComponent<Image>();
            _ghostRing.sprite = RingSprite;
            _ghostRing.type = Image.Type.Sliced;
            _ghostRing.fillCenter = false;
            _ghostRing.pixelsPerUnitMultiplier = 56f / 48f;
            _ghostRing.color = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            _ghostRing.raycastTarget = false;

            // dashed trail (drag)
            var trail = NewRt(_demoRoot, "Trail");
            trail.anchorMin = trail.anchorMax = new Vector2(0.5f, 0.5f);
            trail.pivot = new Vector2(0.5f, 0.5f);
            trail.anchoredPosition = Vector2.zero;
            trail.sizeDelta = Vector2.zero;
            _trailRoot = trail;
            if (!_demoTap)
            {
                BuildDashes(_demoA, _demoB);
                var arrRt = NewRt(_demoRoot, "StaticArrow");
                arrRt.anchorMin = arrRt.anchorMax = new Vector2(0.5f, 0.5f);
                arrRt.sizeDelta = new Vector2(64f, 64f);
                _staticArrow = arrRt.gameObject.AddComponent<Image>();
                _staticArrow.sprite = ArrowSprite;
                _staticArrow.preserveAspect = true;
                _staticArrow.color = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
                _staticArrow.raycastTarget = false;
                var dir = (_demoB - _demoA).normalized;
                arrRt.anchoredPosition = _demoB - dir * 18f;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                arrRt.localRotation = Quaternion.Euler(0f, 0f, ang);
                _staticArrow.gameObject.SetActive(false);
            }

            if (_ghostRt != null)
            {
                _ghostRt.anchoredPosition = _demoA;
                _ghostRt.gameObject.SetActive(false);
            }
            if (_halo != null)
                _halo.rectTransform.anchoredPosition = _demoA;
        }

        void BuildDashes(Vector2 a, Vector2 b)
        {
            if (_trailRoot == null) return;
            _dashes.Clear();
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            Vector2 dir = d / len;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            const float dash = 20f, gap = 16f;
            for (float s = 30f; s < len - 24f; s += dash + gap)
            {
                var go = new GameObject("dash", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_trailRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(dash, 12f);
                rt.anchoredPosition = a + dir * s;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);
                var im = go.GetComponent<Image>();
                im.color = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
                im.raycastTarget = false;
                if (AppRoot.Instance != null && AppRoot.Instance.Rounded != null)
                {
                    im.sprite = AppRoot.Instance.Rounded;
                    im.type = Image.Type.Sliced;
                    im.pixelsPerUnitMultiplier = 56f / 6f;
                }
                go.SetActive(false);
                _dashes.Add(im);
            }
        }

        IEnumerator DemoLoop()
        {
            var lesson = Lesson;
            while (!_stepDone && lesson != null)
            {
                while (_idle < lesson.DemoDelay && !_stepDone) yield return null;
                if (_stepDone) break;

                if (SaveStore.ReduceMotion)
                {
                    ShowStatic();
                    while (_idle >= lesson.DemoDelay && !_stepDone) yield return null;
                    HideDemoVisuals();
                    _idle = 0f;
                    yield return null;
                    continue;
                }

                if (_demoTap) yield return TapDemo();
                else yield return DragDemo();

                HideDemoVisuals();
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        IEnumerator DragDemo()
        {
            var a = _demoA;
            var b = _demoB;
            for (int i = 0; i < _dashes.Count; i++) _dashes[i].gameObject.SetActive(false);
            if (_ghostRt != null)
            {
                _ghostRt.gameObject.SetActive(true);
                _ghostRt.anchoredPosition = a;
            }
            if (_halo != null) _halo.gameObject.SetActive(false);
            float total = 1.2f;
            for (float t = 0f; t < total;)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / total);
                float e = k * k * (3f - 2f * k);
                if (_ghostRt != null) _ghostRt.anchoredPosition = Vector2.Lerp(a, b, e);
                float traveled = (b - a).magnitude * e;
                for (int i = 0; i < _dashes.Count; i++)
                {
                    var drt = (RectTransform)_dashes[i].transform;
                    float dist = (drt.anchoredPosition - a).magnitude;
                    if (!_dashes[i].gameObject.activeSelf && dist <= traveled + 4f)
                        _dashes[i].gameObject.SetActive(true);
                }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.4f);
            HideDemoVisuals();
        }

        IEnumerator TapDemo()
        {
            if (_ghostRt != null)
            {
                _ghostRt.gameObject.SetActive(true);
                _ghostRt.anchoredPosition = _demoA;
                if (_halo != null) { _halo.gameObject.SetActive(true); _halo.rectTransform.anchoredPosition = _demoA; }
            }
            yield return new WaitForSecondsRealtime(0.6f);
            HideDemoVisuals();
        }

        void ShowStatic()
        {
            if (_demoTap)
            {
                // demo tap tanpa gerak: cincin + halo tetap di sel target
                if (_ghostRt != null)
                {
                    _ghostRt.gameObject.SetActive(true);
                    _ghostRt.anchoredPosition = _demoA;
                }
                if (_halo != null)
                {
                    _halo.gameObject.SetActive(true);
                    _halo.rectTransform.anchoredPosition = _demoA;
                }
                return;
            }
            if (_staticArrow != null) _staticArrow.gameObject.SetActive(true);
            for (int i = 0; i < _dashes.Count; i++) _dashes[i].gameObject.SetActive(true);
            if (_ghostRt != null) _ghostRt.gameObject.SetActive(false);
            if (_halo != null) _halo.gameObject.SetActive(false);
        }

        void HideDemoVisuals()
        {
            if (_ghostRt != null) _ghostRt.gameObject.SetActive(false);
            if (_halo != null) _halo.gameObject.SetActive(false);
            if (_staticArrow != null) _staticArrow.gameObject.SetActive(false);
            for (int i = 0; i < _dashes.Count; i++)
                if (_dashes[i] != null) _dashes[i].gameObject.SetActive(false);
        }

        void HideTarget()
        {
            if (_targetRing != null) _targetRing.gameObject.SetActive(false);
        }

        static RectTransform NewRt(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}