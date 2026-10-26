using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using GridCut.Core;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace GridCut.Game
{
    /// <summary>
    /// Papan puzzle (spec bagian 7). Sel 140 / jarak 8 / padding 28 pada 1920×1080,
    /// papan 788×788 berpusat di (A/2, H/2) dengan A = lebar kanvas − 432.
    /// Mode ringkas otomatis bila sel normal &lt; 96: A = lebar − 252, M = 32,
    /// padding 20, jarak 6, inset keping 4.
    /// Urutan gambar: sel → keping → preview → angka clue → cincin hint → input.
    /// </summary>
    public class BoardView : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public PuzzleSession Session { get; private set; }
        public float Cell { get; private set; }              // ukuran sel (bukan pitch)
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public bool IsCompact { get; private set; }
        public float BoardWidth { get; private set; }
        public float BoardHeight { get; private set; }

        public event System.Action DragStarted;
        public event System.Action DragCellChanged;

        // ---- spek (unit kanvas 1920×1080) ----
        const float MaxCell = 140f;
        const float CompactBelow = 96f;
        const float BoardRadius = 48f;
        const float InputMargin = 24f;

        const float NormalCard = 432f;   // lebar kartu + margin kanan
        const float CompactCard = 252f;
        const float NormalM = 48f;
        const float CompactM = 32f;

        public static float NormalPad => 28f;
        public static float CompactPad => 20f;
        public static float NormalGap => 8f;
        public static float CompactGap => 6f;
        public static float NormalInset => 6f;
        public static float CompactInset => 4f;

        /// <summary>Sel dari rumus spek. areaW = A (lebar area kiri), areaH = tinggi kanvas.</summary>
        public static float SpecCell(float areaW, float areaH, int cols, int rows, bool compact)
        {
            float m = compact ? CompactM : NormalM;
            float pad = compact ? CompactPad : NormalPad;
            float gap = compact ? CompactGap : NormalGap;
            return Mathf.Clamp(
                Mathf.Floor(Mathf.Min(
                    (areaW - 2f * m - 2f * pad - gap * (cols - 1)) / cols,
                    (areaH - 2f * m - 2f * pad - gap * (rows - 1)) / rows)),
                24f, MaxCell);
        }

        /// <summary>True bila rumus normal menghasilkan sel &lt; 96 (mis. 14×8).</summary>
        public static bool IsCompactFor(float canvasW, float canvasH, int cols, int rows)
        {
            float aNormal = canvasW - NormalCard;
            return SpecCell(aNormal, canvasH, cols, rows, false) < CompactBelow;
        }

        /// <summary>Sel normal sebelum dikoreksi mode ringkas (dipakai uji panel lama).</summary>
        public static float EstimateCell(float slotW, float slotH, int cols, int rows) =>
            SpecCell(slotW, slotH, cols, rows, false);

        RectTransform _rt;
        RectTransform _slot;
        RectTransform _pieces;
        RectTransform _clues;
        RectTransform _hint;
        RectTransform _previewRt;
        Image _hintRing;
        Image _pFill, _pRing;
        TMP_Text _pNum;
        readonly List<Image> _rectFill = new List<Image>(64);
        readonly List<Image> _neutralFill = new List<Image>(64);
        Coroutine _hintCo;
        bool _forceCompact;

        bool _dragging;
        int _dragPointerId = int.MinValue;
        int _dc0, _dr0, _dc1, _dr1;
        bool _movedAny;

        float Gap => IsCompact ? CompactGap : NormalGap;
        float Pad => IsCompact ? CompactPad : NormalPad;
        float Inset => IsCompact ? CompactInset : NormalInset;

        void Awake() => _rt = (RectTransform)transform;

        public void SetLevel(PuzzleSession session)
        {
            Session = session;
            Cols = session.Puzzle.Cols;
            Rows = session.Puzzle.Rows;
            Rebuild();
        }

        public void SetCompact(bool compact) => _forceCompact = compact;

        public void RebuildIfReady()
        {
            if (Session != null && _slot != null) Rebuild();
        }

        public void Rebuild()
        {
            if (_slot == null) _slot = transform.parent as RectTransform;
            if (Session == null) return;
            StopAllCoroutines();
            _hintCo = null;

            IsCompact = _forceCompact;
            Cell = SpecCell(_slot.rect.width, _slot.rect.height, Cols, Rows, IsCompact);
            BoardWidth = Cols * Cell + (Cols - 1) * Gap + 2f * Pad;
            BoardHeight = Rows * Cell + (Rows - 1) * Gap + 2f * Pad;
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(BoardWidth, BoardHeight);
            _rt.anchoredPosition = Vector2.zero;

            SetupBg();
            SetupCells();

            _pieces = Ensure("Pieces", 2);
            _clues = Ensure("Clues", 3);
            _hint = Ensure("Hint", 4);
            EnsureInput(5);

            BuildPieces();
            BuildPreview();
            BuildClues();
            BuildHintRing();
            ShowPlaced();
        }

        RectTransform Ensure(string childName, int index)
        {
            var t = _rt.Find(childName) as RectTransform;
            if (t == null)
            {
                var go = new GameObject(childName, typeof(RectTransform));
                t = (RectTransform)go.transform;
                t.SetParent(_rt, false);
            }
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero;
            t.SetSiblingIndex(Mathf.Min(index, _rt.childCount - 1));
            ClearChildren(t);
            return t;
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        void SetupBg()
        {
            var bgT = _rt.Find("bg");
            if (bgT == null)
            {
                var go = new GameObject("bg", typeof(RectTransform), typeof(Image));
                bgT = go.transform;
                bgT.SetParent(_rt, false);
            }
            var bg = bgT.GetComponent<Image>();
            if (bg == null) bg = bgT.gameObject.AddComponent<Image>();
            var rt = (RectTransform)bgT;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            bg.sprite = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 56f / BoardRadius;
            bg.color = ThemeApplier.ColorOf(ThemeApplier.Role.Board);
            bg.raycastTarget = false;
            UiFactory.ApplyShadow(bg, new Vector2(0f, -16f),
                ThemeApplier.ColorOf(ThemeApplier.Role.ShadowBoard));
            bgT.SetSiblingIndex(0);
        }

        void SetupCells()
        {
            var t = _rt.Find("Cells") as RectTransform;
            if (t == null)
            {
                var go = new GameObject("Cells", typeof(RectTransform));
                t = (RectTransform)go.transform;
                t.SetParent(_rt, false);
            }
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero;
            t.SetSiblingIndex(1);
            ClearChildren(t);

            var glg = t.GetComponent<GridLayoutGroup>();
            if (glg == null) glg = t.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(Cell, Cell);
            glg.spacing = new Vector2(Gap, Gap);
            glg.padding = new RectOffset(Mathf.RoundToInt(Pad), Mathf.RoundToInt(Pad),
                                         Mathf.RoundToInt(Pad), Mathf.RoundToInt(Pad));
            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.UpperLeft;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = Cols;

            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            var cellColor = ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty);
            float radius = Mathf.Clamp(0.14f * Cell, 10f, 20f);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var img = UiFactory.Image(t, $"cell{r}x{c}", cellColor);
                    if (rounded != null)
                    {
                        img.sprite = rounded;
                        img.type = UnityEngine.UI.Image.Type.Sliced;
                        img.pixelsPerUnitMultiplier = 56f / radius;
                    }
                }
        }

        void EnsureInput(int index)
        {
            var t = _rt.Find("Input") as RectTransform;
            if (t == null)
            {
                var go = new GameObject("Input", typeof(RectTransform), typeof(Image));
                t = (RectTransform)go.transform;
                t.SetParent(_rt, false);
            }
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = new Vector2(-InputMargin, -InputMargin);
            t.offsetMax = new Vector2(InputMargin, InputMargin);
            t.SetSiblingIndex(index);
            var img = t.GetComponent<Image>();
            if (img == null) img = t.gameObject.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;
            var proxy = t.GetComponent<InputProxy>();
            if (proxy == null) proxy = t.gameObject.AddComponent<InputProxy>();
            proxy.Owner = this;
        }

        // ------------------------------ keping ---------------------------------

        void BuildPieces()
        {
            _rectFill.Clear();
            _neutralFill.Clear();
            float radius = Mathf.Clamp(0.23f * Cell, 16f, 32f);
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var go = new GameObject($"fill{ci}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_pieces, false);
                var img = go.GetComponent<Image>();
                MakePieceRoot(img, radius);
                MakePieceFill(img, radius);
                img.raycastTarget = false;
                img.gameObject.SetActive(false);
                _rectFill.Add(img);
            }
        }

        void MakePieceRoot(Image img, float radius)
        {
            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            if (rounded != null)
            {
                img.sprite = rounded;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 56f / radius;
            }
        }

        void MakePieceFill(Image root, float radius)
        {
            var go = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var inner = go.GetComponent<Image>();
            var rounded = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            if (rounded != null)
            {
                inner.sprite = rounded;
                inner.type = UnityEngine.UI.Image.Type.Sliced;
                inner.pixelsPerUnitMultiplier = 56f / Mathf.Max(10f, radius - Inset);
            }
            inner.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.rectTransform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Inset, Inset);
            rt.offsetMax = new Vector2(-Inset, -Inset);
        }

        void BuildPreview()
        {
            var go = new GameObject("preview", typeof(RectTransform));
            go.transform.SetParent(_pieces, false);
            _previewRt = (RectTransform)go.transform;
            float radius = Mathf.Clamp(0.23f * Cell, 16f, 32f);

            _pFill = UiFactory.Image(go.transform, "Fill", Color.clear);
            MakePieceRoot(_pFill, radius);
            _pFill.rectTransform.anchorMin = Vector2.zero;
            _pFill.rectTransform.anchorMax = Vector2.one;
            _pFill.rectTransform.offsetMin = Vector2.zero;
            _pFill.rectTransform.offsetMax = Vector2.zero;

            _pRing = UiFactory.Image(go.transform, "Ring", Color.clear);
            var ring = AppRoot.Instance != null ? AppRoot.Instance.RoundedRing : null;
            if (ring != null)
            {
                _pRing.sprite = ring;
                _pRing.type = UnityEngine.UI.Image.Type.Sliced;
                _pRing.fillCenter = false;
                _pRing.pixelsPerUnitMultiplier = 56f / radius;
            }
            var rr = _pRing.rectTransform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;

            int fs = ClueFontSize;
            _pNum = UiFactory.Text(go.transform, "Num", "0", fs, null, TextAlignmentOptions.Center);
            var bold = AppRoot.Instance != null ? AppRoot.Instance.FontBold : null;
            if (bold != null) _pNum.font = bold;
            var nrt = _pNum.rectTransform;
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
            go.SetActive(false);
        }

        void BuildClues()
        {
            var textColor = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            var bold = AppRoot.Instance != null ? AppRoot.Instance.FontBold : null;
            int fs = ClueFontSize;
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var clue = Session.Puzzle.Clues[ci];
                var t = UiFactory.Text(_clues, $"clue{ci}", clue.Value.ToString(), fs, textColor,
                    TextAlignmentOptions.Center);
                if (bold != null) t.font = bold;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Cell, Cell);
                rt.anchoredPosition = CellCenter(clue.Col, clue.Row);
            }
        }

        int ClueFontSize => Mathf.Clamp(Mathf.RoundToInt(Cell * 0.5f), 40, 72);

        void BuildHintRing()
        {
            var go = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_hint, false);
            _hintRing = go.GetComponent<Image>();
            var ring = AppRoot.Instance != null ? AppRoot.Instance.RoundedRing : null;
            if (ring != null)
            {
                _hintRing.sprite = ring;
                _hintRing.type = UnityEngine.UI.Image.Type.Sliced;
                _hintRing.fillCenter = false;
                _hintRing.pixelsPerUnitMultiplier = 56f / Mathf.Clamp(0.23f * Cell, 16f, 32f);
            }
            _hintRing.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Cell, Cell);
            go.SetActive(false);
        }

        Vector2 CellCenter(int col, int row) => new Vector2(
            -BoardWidth * 0.5f + Pad + col * (Cell + Gap) + Cell * 0.5f,
             BoardHeight * 0.5f - Pad - row * (Cell + Gap) - Cell * 0.5f);

        Vector2 PieceCenter(in RectI rect) => new Vector2(
            -BoardWidth * 0.5f + Pad + rect.X * (Cell + Gap) + Inset + PieceSpan(rect.W) * 0.5f,
             BoardHeight * 0.5f - Pad - rect.Y * (Cell + Gap) - Inset - PieceSpan(rect.H) * 0.5f);

        float PieceSpan(int span) => span * Cell + (span - 1) * Gap - 2f * Inset;

        // ------------------------- keping terpasang -----------------------------

        public void ShowPlaced()
        {
            for (int i = 0; i < _rectFill.Count; i++)
                if (_rectFill[i] != null) _rectFill[i].gameObject.SetActive(false);

            int neutralUsed = 0;
            for (int i = 0; i < Session.Placed.Count; i++)
            {
                var rect = Session.Placed[i];
                int owner = Session.RectOwner(i);
                Image img;
                if (owner >= 0 && owner < _rectFill.Count && _rectFill[owner] != null)
                {
                    int ci = Session.Puzzle.Clues[owner].ColorIndex;
                    img = _rectFill[owner];
                    TintPiece(img, ThemeApplier.ColorOf(ThemeApplier.Role.RectEdge, ci),
                        ThemeApplier.ColorOf(ThemeApplier.Role.Rect, ci));
                }
                else
                {
                    img = NeutralFill(neutralUsed++);
                    var gl = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                    TintPiece(img, new Color(gl.r, gl.g, gl.b, 0.45f),
                        new Color(gl.r, gl.g, gl.b, 0.55f));
                }
                PlaceGeometry(img, rect);
                img.gameObject.SetActive(true);
            }
            for (int n = neutralUsed; n < _neutralFill.Count; n++)
                if (_neutralFill[n] != null) _neutralFill[n].gameObject.SetActive(false);
        }

        void PlaceGeometry(Image img, in RectI rect)
        {
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));
            img.rectTransform.anchoredPosition = PieceCenter(rect);
        }

        static Image PieceInner(Component root) =>
            root == null || root.transform.childCount == 0 ? null
                : root.transform.GetChild(0).GetComponent<Image>();

        static void TintPiece(Image root, Color frame, Color fill)
        {
            root.color = frame;
            var inner = PieceInner(root);
            if (inner != null) inner.color = fill;
        }

        Image NeutralFill(int n)
        {
            if (n < _neutralFill.Count && _neutralFill[n] != null) return _neutralFill[n];
            var go = new GameObject("fillNeutral", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            float radius = Mathf.Clamp(0.23f * Cell, 16f, 32f);
            MakePieceRoot(img, radius);
            MakePieceFill(img, radius);
            img.raycastTarget = false;
            go.transform.SetParent(_pieces, false);
            if (n < _neutralFill.Count) _neutralFill[n] = img; else _neutralFill.Add(img);
            return img;
        }

        public void ClearPreview()
        {
            if (_previewRt != null) _previewRt.gameObject.SetActive(false);
            _dragging = false;
        }

        public void ApplyTheme() => Rebuild();

        // ------------------------------- input ---------------------------------

        public void OnPointerDown(PointerEventData e) => Begin(e);
        public void OnPointerUp(PointerEventData e)
        {
            if (_dragging && e.pointerId == _dragPointerId) OnEndDrag(e);
        }

        void Begin(PointerEventData e)
        {
            if (Session == null || Session.IsSolved || _dragging) return;
            if (!TryCell(e.position, out int c, out int r)) return;
            _dragging = true;
            _dragPointerId = e.pointerId;
            _dc0 = _dc1 = c;
            _dr0 = _dr1 = r;
            _movedAny = false;
            UpdatePreview();
            DragStarted?.Invoke();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _dragPointerId) return;
            if (!TryCell(e.position, out int c, out int r)
                && !TryCellClamped(e.position, out c, out r)) return;
            if (c != _dc1 || r != _dr1)
            {
                _movedAny |= c != _dc0 || r != _dr0;
                _dc1 = c; _dr1 = r;
                UpdatePreview();
                DragCellChanged?.Invoke();
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _dragPointerId) return;
            _dragging = false;
            if (_previewRt != null) _previewRt.gameObject.SetActive(false);
            if (!TryCell(e.position, out int c, out int r)
                && !TryCellClamped(e.position, out c, out r)) return;
            if (c == _dc0 && r == _dr0 && !_movedAny)
            {
                if (Session.RemoveRectAt(c, r)) return;
                var one = PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows);
                if (Session.RectValid(one)) Session.Place(one);
                return;
            }
            Session.Place(PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows));
        }

        void Update()
        {
            if (!_dragging) return;
            bool mouseDown = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool touchDown = false;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                var phase = ts.primaryTouch.phase.ReadValue();
                touchDown = phase == TouchPhase.Began || phase == TouchPhase.Moved
                            || phase == TouchPhase.Stationary;
            }
            if (!mouseDown && !touchDown) CancelDrag();
        }

        void CancelDrag()
        {
            _dragging = false;
            if (_previewRt != null) _previewRt.gameObject.SetActive(false);
        }

        void OnApplicationFocus(bool hasFocus) { if (!hasFocus) CancelDrag(); }
        void OnDisable() => CancelDrag();

        bool TryCell(Vector2 screenPos, out int col, out int row)
        {
            col = row = -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, null, out var local))
                return false;
            float gridW = Cols * Cell + (Cols - 1) * Gap;
            float gridH = Rows * Cell + (Rows - 1) * Gap;
            float u = local.x + BoardWidth * 0.5f - Pad;
            float v = BoardHeight * 0.5f - local.y - Pad;
            if (u < 0f || v < 0f || u > gridW || v > gridH) return false;
            col = Mathf.Clamp((int)(u / (Cell + Gap)), 0, Cols - 1);
            row = Mathf.Clamp((int)(v / (Cell + Gap)), 0, Rows - 1);
            return true;
        }

        bool TryCellClamped(Vector2 screenPos, out int col, out int row)
        {
            col = row = 0;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, null, out var local))
                return false;
            float gridW = Cols * Cell + (Cols - 1) * Gap;
            float gridH = Rows * Cell + (Rows - 1) * Gap;
            float u = Mathf.Clamp(local.x + BoardWidth * 0.5f - Pad, 0f, gridW - 0.01f);
            float v = Mathf.Clamp(BoardHeight * 0.5f - local.y - Pad, 0f, gridH - 0.01f);
            col = Mathf.Min(Cols - 1, (int)(u / (Cell + Gap)));
            row = Mathf.Min(Rows - 1, (int)(v / (Cell + Gap)));
            return true;
        }

        void UpdatePreview()
        {
            if (Session == null || _previewRt == null) return;
            var rect = PuzzleSession.Snap(_dc0, _dr0, _dc1, _dr1, Cols, Rows);
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            var fill = accent; fill.a = 0.3f;
            _pFill.color = fill;
            _pRing.color = accent;
            _pNum.color = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            _pNum.text = (rect.W * rect.H).ToString();
            _pNum.fontSize = ClueFontSize;
            _previewRt.SetAsLastSibling();
            _previewRt.anchorMin = _previewRt.anchorMax = new Vector2(0.5f, 0.5f);
            _previewRt.sizeDelta = new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));
            _previewRt.anchoredPosition = PieceCenter(rect);
            _previewRt.gameObject.SetActive(true);
        }

        /// <summary>Hint: cincin Aksen di sel clue sasaran, berdenyut tiga kali.</summary>
        public void HintPulse(int clueIndex, System.Action apply)
        {
            if (_hintCo != null) StopCoroutine(_hintCo);
            _hintCo = StartCoroutine(HintPulseCo(clueIndex, apply));
        }

        System.Collections.IEnumerator HintPulseCo(int clueIndex, System.Action apply)
        {
            var clue = Session.Puzzle.Clues[clueIndex];
            var accent = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            if (_hintRing != null)
            {
                var rt = _hintRing.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Cell, Cell);
                rt.anchoredPosition = CellCenter(clue.Col, clue.Row);
                _hintRing.color = accent;
                _hintRing.gameObject.SetActive(true);
                for (int i = 0; i < 6; i++)
                {
                    bool hot = (i & 1) == 0;
                    var c = accent; c.a = hot ? 1f : 0.15f;
                    _hintRing.color = c;
                    yield return new WaitForSecondsRealtime(0.15f);
                }
                _hintRing.gameObject.SetActive(false);
            }
            _hintCo = null;
            apply?.Invoke();
        }

        class InputProxy : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
        {
            public BoardView Owner;
            public void OnPointerDown(PointerEventData e) => Owner.OnPointerDown(e);
            public void OnDrag(PointerEventData e) => Owner.OnDrag(e);
            public void OnEndDrag(PointerEventData e) => Owner.OnEndDrag(e);
            public void OnPointerUp(PointerEventData e) => Owner.OnPointerUp(e);
        }
    }
}