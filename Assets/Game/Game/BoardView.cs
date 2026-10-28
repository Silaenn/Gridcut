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
    /// Papan puzzle layout vertikal (spec bagian 7 revisi): grid selebar mungkin,
    /// 5×5 sel 132 / jarak 8 / padding 28 → papan 748, pusat horizontal, atas y=132.
    /// Papan + Hint ditumpuk jarak 44 lalu dipusatkan di zona y=132..1052.
    /// Area papan 1536×748; sel &lt; 110 memakai jarak 6, padding 24, inset 4.
    /// </summary>
    public class BoardView : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public PuzzleSession Session { get; private set; }
        public float Cell { get; private set; }
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public float BoardWidth { get; private set; }
        public float BoardHeight { get; private set; }
        public float BoardCenterY { get; private set; }
        public float HintCenterY { get; private set; }
        public bool TutorialLayout { get; private set; }

        // Gerbang input tutorial (null = normal gameplay)
        public System.Func<RectI, bool> PlaceGate;
        public System.Func<int, int, bool> TapGate;
        public event System.Action Rejected;
        public RectTransform DemoLayer { get; private set; }

        static readonly Color InvalidPreview = new Color(0.88f, 0.31f, 0.35f, 1f);

        public event System.Action DragStarted;
        public event System.Action DragCellChanged;

        const float MaxCell = 140f;
        const float AreaW = 1536f;
        const float AreaH = 690f;
        const float BoardRadius = 48f;
        const float InputMargin = 24f;
        const float HintHeight = 128f;
        const float HintGap = 44f;
        const float ZoneTopScreen = 132f;
        const float ZoneBottomScreen = 1052f;

        // normal / compact (<110)
        const float NormalGap = 8f, NormalPad = 28f, NormalInset = 6f;
        const float SmallGap = 6f, SmallPad = 24f, SmallInset = 4f;
        const float SmallThreshold = 110f;

        RectTransform _rt;
        RectTransform _slot;
        RectTransform _pieces;
        RectTransform _clues;
        RectTransform _hint;
        RectTransform _previewRt;
        CanvasGroup _previewGroup;
        Coroutine _rejectCo;
        Image _hintRing;
        Image _pFill, _pRing;
        readonly List<Image> _rectFill = new List<Image>(64);
        readonly List<Image> _neutralFill = new List<Image>(64);
        Coroutine _hintCo;

        bool _dragging;
        int _dragPointerId = int.MinValue;
        int _dc0, _dr0, _dc1, _dr1;
        bool _movedAny;

        float Gap { get; set; }
        float Pad { get; set; }
        float Inset { get; set; }

        void Awake() => _rt = (RectTransform)transform;

        public void SetLevel(PuzzleSession session)
        {
            Session = session;
            Cols = session.Puzzle.Cols;
            Rows = session.Puzzle.Rows;
            Rebuild();
        }

        public void SetTutorialLayout(bool on) => TutorialLayout = on;

        public void Rebuild()
        {
            if (_slot == null) _slot = transform.parent as RectTransform;
            if (Session == null) return;
            StopAllCoroutines();
            _hintCo = null;

            ComputeLayout();

            SetupBg();
            SetupCells();

            _pieces = Ensure("Pieces", 2);
            _clues = Ensure("Clues", 3);
            _hint = Ensure("Hint", 4);
            EnsureInput(5);
            EnsureDemo(6);

            BuildPieces();
            BuildPreview();
            BuildClues();
            BuildHintRing();
            ShowPlaced();
        }

        void ComputeLayout()
        {
            float h = _slot != null ? _slot.rect.height : 1080f;
            if (h <= 1f) h = 1080f;

            float cw = Mathf.Min(AreaW, _slot != null ? _slot.rect.width : AreaW);
            // Batas tinggi ini menjaga jarak ke baris tombol atas (120 px) di semua
            // ukuran papan, termasuk 5x5 dan papan 7-8 baris.
            float cell = Mathf.Floor(Mathf.Min(
                (cw - 2f * NormalPad - NormalGap * (Cols - 1)) / Cols,
                (AreaH - 2f * NormalPad - NormalGap * (Rows - 1)) / Rows));
            cell = Mathf.Clamp(cell, 24f, MaxCell);

            if (cell < SmallThreshold)
            {
                Gap = SmallGap; Pad = SmallPad; Inset = SmallInset;
            }
            else
            {
                Gap = NormalGap; Pad = NormalPad; Inset = NormalInset;
            }
            Cell = cell;
            BoardWidth = Cols * Cell + (Cols - 1) * Gap + 2f * Pad;
            BoardHeight = Rows * Cell + (Rows - 1) * Gap + 2f * Pad;

            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(BoardWidth, BoardHeight);

            if (TutorialLayout)
            {
                // pusat papan tutorial di y=528 layar (sedikit di atas tengah)
                BoardCenterY = h * 0.5f - 528f;
                HintCenterY = BoardCenterY;
                _rt.anchoredPosition = new Vector2(0f, BoardCenterY);
                return;
            }

            float zoneTop = h * 0.5f - ZoneTopScreen;
            float zoneBottom = h * 0.5f - ZoneBottomScreen;
            float zoneCenter = (zoneTop + zoneBottom) * 0.5f;
            BoardCenterY = zoneCenter + (HintGap + HintHeight) * 0.5f;
            HintCenterY = zoneCenter - (BoardHeight + HintGap + HintHeight) * 0.5f + HintHeight * 0.5f;
            _rt.anchoredPosition = new Vector2(0f, BoardCenterY);
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

        void EnsureDemo(int index)
        {
            var t = _rt.Find("Demo") as RectTransform;
            if (t == null)
            {
                var go = new GameObject("Demo", typeof(RectTransform));
                t = (RectTransform)go.transform;
                t.SetParent(_rt, false);
            }
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero;
            t.SetSiblingIndex(Mathf.Min(index, _rt.childCount - 1));
            ClearChildren(t);
            DemoLayer = t;
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
            _previewGroup = go.AddComponent<CanvasGroup>();
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

        /// <summary>Perayaan papan setelah level selesai (spec bagian 14): keping
        /// berdenyut berurutan dan kotak warna jatuh selama sekitar 1,2 detik.</summary>
        public void Celebrate()
        {
            if (SaveStore.ReduceMotion) return;
            StartCoroutine(CelebrateCo());
        }

        System.Collections.IEnumerator CelebrateCo()
        {
            StartCoroutine(BoardGlowCo());
            var pieces = new List<Image>();
            for (int i = 0; i < _rectFill.Count; i++)
                if (_rectFill[i] != null && _rectFill[i].gameObject.activeSelf) pieces.Add(_rectFill[i]);
            for (int i = 0; i < _neutralFill.Count; i++)
                if (_neutralFill[i] != null && _neutralFill[i].gameObject.activeSelf) pieces.Add(_neutralFill[i]);

            const float dur = 0.6f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                float s = 1f + 0.06f * Mathf.Sin(k * Mathf.PI);
                for (int i = 0; i < pieces.Count; i++)
                    pieces[i].rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            for (int i = 0; i < pieces.Count; i++)
                pieces[i].rectTransform.localScale = Vector3.one;
        }

        // ------------------------- feedback keping -------------------------

        /// <summary>Pop kecil pada keping yang baru terpasang + kilat tipis.</summary>
        public void PlayPlaceFx(int owner, in RectI rect)
        {
            if (SaveStore.ReduceMotion) return;
            Image img = null;
            if (owner >= 0 && owner < _rectFill.Count) img = _rectFill[owner];
            else
            {
                Vector2 wantPos = PieceCenter(rect);
                Vector2 wantSize = new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));
                for (int i = 0; i < _neutralFill.Count; i++)
                {
                    var n = _neutralFill[i];
                    if (n == null || !n.gameObject.activeSelf) continue;
                    if ((n.rectTransform.anchoredPosition - wantPos).sqrMagnitude < 1f
                        && (n.rectTransform.sizeDelta - wantSize).sqrMagnitude < 1f)
                    { img = n; break; }
                }
            }
            if (img != null) StartCoroutine(PiecePopCo(img.rectTransform));
            StartCoroutine(PlaceFlashCo(rect));
        }

        System.Collections.IEnumerator PiecePopCo(RectTransform rt)
        {
            Vector3 rest = rt.localScale;
            for (float t = 0f; t < 0.16f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.16f);
                float s = k < 0.5f ? Mathf.Lerp(0.92f, 1.04f, k / 0.5f)
                                   : Mathf.Lerp(1.04f, 1f, (k - 0.5f) / 0.5f);
                rt.localScale = new Vector3(rest.x * s, rest.y * s, rest.z);
                yield return null;
            }
            rt.localScale = rest;
        }

        System.Collections.IEnumerator PlaceFlashCo(RectI rect)
        {
            var go = new GameObject("placeFx", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_pieces, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));
            rt.anchoredPosition = PieceCenter(rect);
            rt.SetAsLastSibling();
            var im = go.GetComponent<Image>();
            im.sprite = AppRoot.Instance != null ? AppRoot.Instance.Rounded : null;
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = 56f / Mathf.Clamp(0.23f * Cell, 16f, 32f);
            im.raycastTarget = false;
            var c = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.18f);
                c.a = 0.3f * (1f - k);
                im.color = c;
                float s = 1f + 0.02f * k;
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Keping menghilang: salinan kecil mengecil + memudar.</summary>
        public void PlayRemoveFx(int owner, in RectI rect)
        {
            if (SaveStore.ReduceMotion) return;
            StartCoroutine(RemoveFxCo(owner, rect));
        }

        System.Collections.IEnumerator RemoveFxCo(int owner, RectI rect)
        {
            var go = new GameObject("removeFx", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_pieces, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));
            rt.anchoredPosition = PieceCenter(rect);
            rt.SetAsLastSibling();
            var root = go.GetComponent<Image>();
            float radius = Mathf.Clamp(0.23f * Cell, 16f, 32f);
            MakePieceRoot(root, radius);
            MakePieceFill(root, radius);
            Color frame, fill;
            if (owner >= 0 && owner < Session.Puzzle.ClueCount)
            {
                int ci = Session.Puzzle.Clues[owner].ColorIndex;
                frame = ThemeApplier.ColorOf(ThemeApplier.Role.RectEdge, ci);
                fill = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, ci);
            }
            else
            {
                var gl = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
                frame = new Color(gl.r, gl.g, gl.b, 0.45f);
                fill = new Color(gl.r, gl.g, gl.b, 0.55f);
            }
            TintPiece(root, frame, fill);
            var cg = go.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 0.16f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.16f);
                cg.alpha = 1f - k;
                float s = 1f - 0.15f * k;
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Angka clue berdenyut kecil saat areanya terpasang.</summary>
        public void PulseClue(int clueIndex)
        {
            if (SaveStore.ReduceMotion || clueIndex < 0 || _clues == null) return;
            var t = _clues.Find($"clue{clueIndex}") as RectTransform;
            if (t != null) StartCoroutine(CluePopCo(t));
        }

        System.Collections.IEnumerator CluePopCo(RectTransform rt)
        {
            Vector3 rest = rt.localScale;
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.2f);
                float s = k < 0.5f ? Mathf.Lerp(1f, 1.18f, k / 0.5f)
                                   : Mathf.Lerp(1.18f, 1f, (k - 0.5f) / 0.5f);
                rt.localScale = new Vector3(rest.x * s, rest.y * s, rest.z);
                yield return null;
            }
            rt.localScale = rest;
        }

        System.Collections.IEnumerator BoardGlowCo()
        {
            var ring = AppRoot.Instance != null ? AppRoot.Instance.RoundedRing : null;
            if (DemoLayer == null || ring == null) yield break;
            var go = new GameObject("winGlow", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(DemoLayer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(BoardWidth, BoardHeight);
            rt.anchoredPosition = Vector2.zero;
            rt.SetAsLastSibling();
            var im = go.GetComponent<Image>();
            im.sprite = ring;
            im.type = Image.Type.Sliced;
            im.fillCenter = false;
            im.pixelsPerUnitMultiplier = 56f / 48f;
            im.raycastTarget = false;
            var c = ThemeApplier.ColorOf(ThemeApplier.Role.Accent);
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.6f);
                c.a = 0.5f * Mathf.Sin(k * Mathf.PI);
                im.color = c;
                float s = 1f + 0.012f * Mathf.Sin(k * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            Destroy(go);
        }

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

        void SetPreviewVisible(bool on)
        {
            if (_previewRt != null) _previewRt.gameObject.SetActive(on);
            if (on && _previewGroup != null) _previewGroup.alpha = 1f;
        }

        public void ClearPreview()
        {
            if (_rejectCo != null) { StopCoroutine(_rejectCo); _rejectCo = null; }
            if (_previewRt != null)
            {
                _previewRt.gameObject.SetActive(false);
                if (_previewGroup != null) _previewGroup.alpha = 1f;
            }
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
            if (!TryCell(e.position, out int c, out int r))
            {
                if (!TryCellClamped(e.position, out c, out r))
                {
                    SetPreviewVisible(false);
                    return;
                }
            }

            if (c == _dc0 && r == _dr0 && !_movedAny)
            {
                if (TapGate != null && !TapGate(c, r))
                {
                    RejectPreview();
                    return;
                }
                if (Session.RemoveRectAt(c, r)) { SetPreviewVisible(false); return; }
                var one = PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows);
                if (Session.RectValid(one)) { SetPreviewVisible(false); Session.Place(one); }
                else SetPreviewVisible(false);
                return;
            }

            var rect = PuzzleSession.Snap(_dc0, _dr0, c, r, Cols, Rows);
            if (PlaceGate != null && !PlaceGate(rect))
            {
                RejectPreview();
                return;
            }
            SetPreviewVisible(false);
            Session.Place(rect);
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
            if (_rejectCo != null) { StopCoroutine(_rejectCo); _rejectCo = null; }
            if (_previewRt != null)
            {
                _previewRt.gameObject.SetActive(false);
                if (_previewGroup != null) _previewGroup.alpha = 1f;
            }
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

        public Vector2 CellCenterLocal(int col, int row) => CellCenter(col, row);
        public Vector2 RectCenterLocal(in RectI rect) => PieceCenter(rect);
        public Vector2 RectSizeLocal(in RectI rect) => new Vector2(PieceSpan(rect.W), PieceSpan(rect.H));

        void RejectPreview()
        {
            Rejected?.Invoke();
            if (_rejectCo != null) StopCoroutine(_rejectCo);
            _rejectCo = StartCoroutine(RejectCo());
        }

        System.Collections.IEnumerator RejectCo()
        {
            if (_previewRt == null || !_previewRt.gameObject.activeSelf) yield break;
            var basePos = _previewRt.anchoredPosition;
            const float dur = 0.28f;
            for (float t = 0f; t < dur;)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                var p = basePos;
                if (!SaveStore.ReduceMotion)
                    p.x += Mathf.Sin(k * Mathf.PI * 6f) * 8f * (1f - k);
                _previewRt.anchoredPosition = p;
                if (_previewGroup != null) _previewGroup.alpha = 1f - k;
                yield return null;
            }
            _previewRt.anchoredPosition = basePos;
            _previewRt.gameObject.SetActive(false);
            if (_previewGroup != null) _previewGroup.alpha = 1f;
            _rejectCo = null;
        }

        void UpdatePreview()
        {
            if (Session == null || _previewRt == null) return;
            if (_rejectCo != null) { StopCoroutine(_rejectCo); _rejectCo = null; }
            if (_previewGroup != null) _previewGroup.alpha = 1f;
            var rect = PuzzleSession.Snap(_dc0, _dr0, _dc1, _dr1, Cols, Rows);
            // Jangan bocorkan apakah rect cocok dengan clue sebelum dilepas:
            // semua rect yang boleh dipasang tetap Aksen. Merah hanya untuk
            // gerbang yang benar-benar menolak (tutorial/akhir level).
            bool allowed = PlaceGate == null || PlaceGate(rect);
            var tone = allowed ? ThemeApplier.ColorOf(ThemeApplier.Role.Accent) : InvalidPreview;
            var fill = tone; fill.a = allowed ? 0.3f : 0.2f;
            _pFill.color = fill;
            _pRing.color = tone;
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

        /// <summary>Batalkan hint yang masih berdenyut (mis. saat Reset).</summary>
        public void CancelHint()
        {
            if (_hintCo != null) { StopCoroutine(_hintCo); _hintCo = null; }
            if (_hintRing != null) _hintRing.gameObject.SetActive(false);
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