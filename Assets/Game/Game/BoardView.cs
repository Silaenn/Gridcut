using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>
    /// Renders a puzzle board (background, grid lines, clue labels, placed-rect fills)
    /// and handles pointer → cell input with an axis-locked snapped preview. The whole
    /// board rebuilds on level start and on screen resize; puzzles are small enough
    /// (max 14x8 cells) that rebuilds are effectively free.
    /// </summary>
    public class BoardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public PuzzleSession Session { get; private set; }
        public float Cell { get; private set; }
        public int Cols { get; private set; }
        public int Rows { get; private set; }

        RectTransform _rt;
        Image _preview;
        List<Image> _rectFill = new List<Image>(64);          // per clue fill image

        bool _dragging;
        int _dc0, _dr0, _dc1, _dr1;
        RectTransform _slot;
        Coroutine _flash;

        void Awake() => _rt = (RectTransform)transform;

        /// <summary>Replaces the current level.</summary>
        public void SetLevel(PuzzleSession session)
        {
            Session = session;
            Cols = session.Puzzle.Cols;
            Rows = session.Puzzle.Rows;
            Rebuild();
        }

        public void RebuildIfReady()
        {
            if (Session != null && _slot != null) Rebuild();
        }

        public void Rebuild()
        {
            if (_slot == null) _slot = transform.parent as RectTransform;
            if (Session == null) return;
            ClearChildren(_rt);
            _rectFill.Clear();

            float availW = _slot.rect.width * 0.95f;
            float availH = _slot.rect.height * 0.95f;
            Cell = Mathf.Max(24f, Mathf.Floor(Mathf.Min(availW / Cols, availH / Rows)));
            float bw = Cols * Cell, bh = Rows * Cell;
            _rt.sizeDelta = new Vector2(bw, bh);

            // background and cell area
            var bg = UiFactory.Panel(_rt, "bg", ThemeApplier.ColorOf(ThemeApplier.Role.CellEmpty));

            // rect fills (one child per clue; enabled state mirrors Placed)
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var go = new GameObject($"fill{ci}", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                go.transform.SetParent(_rt, false);
                img.raycastTarget = false;
                img.enabled = false;
                _rectFill.Add(img);
            }

            // grid lines
            var lineColor = ThemeApplier.ColorOf(ThemeApplier.Role.GridLine);
            for (int c = 0; c <= Cols; c++)
                Line($"v{c}", lineColor, new Vector2(2f, bh), new Vector2(-bw / 2f + c * Cell, 0f));
            for (int r = 0; r <= Rows; r++)
                Line($"h{r}", lineColor, new Vector2(bw, 2f), new Vector2(0f, -bh / 2f + r * Cell));

            // clue labels
            var tc = ThemeApplier.ColorOf(ThemeApplier.Role.Text);
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                var clue = Session.Puzzle.Clues[ci];
                var t = UiFactory.Text(_rt, $"clue{ci}", clue.Value.ToString(),
                    Mathf.RoundToInt(Cell * 0.42f), tc, TMPro.TextAlignmentOptions.Center);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Cell - 4f, Cell - 4f);
                rt.anchoredPosition = CellCenter(clue.Col, clue.Row);
            }

            // preview rect (on top of fills, below nothing yet)
            var pgo = new GameObject("preview", typeof(RectTransform), typeof(Image));
            _preview = pgo.GetComponent<Image>();
            pgo.transform.SetParent(_rt, false);
            _preview.raycastTarget = false;
            _preview.enabled = false;

            // input catcher full board
            var igo = new GameObject("input", typeof(RectTransform), typeof(Image));
            var ii = igo.GetComponent<Image>();
            ii.color = Color.clear;
            ii.raycastTarget = true;
            igo.transform.SetParent(_rt, false);
            var irt = (RectTransform)igo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var proxy = igo.AddComponent<InputProxy>();
            proxy.Owner = this;

            ShowPlaced();
        }

        void Line(string name, Color c, Vector2 size, Vector2 pos)
        {
            var img = UiFactory.Image(_rt, name, c);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        Vector2 CellCenter(int col, int row) =>
            new Vector2(-(Cols * Cell) / 2f + (col + 0.5f) * Cell,
                         (Rows * Cell) / 2f - (row + 0.5f) * Cell);

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        // ------------------------- placed rendering -----------------------------

        public void ShowPlaced()
        {
            for (int ci = 0; ci < Session.ClueCount; ci++)
            {
                if (ci >= _rectFill.Count || _rectFill[ci] == null) continue;
                _rectFill[ci].enabled = false;
            }
            foreach (var rect in Session.Placed)
            {
                int owner = -1;
                for (int row = rect.Y; row < rect.Bottom && owner == -1; row++)
                    for (int col = rect.X; col < rect.Right && owner == -1; col++)
                        owner = Session.PlacedClueAt(col, row);
                if (owner < 0 || owner >= _rectFill.Count) continue;

                var img = _rectFill[owner];
                var rc = ThemeApplier.ColorOf(ThemeApplier.Role.Rect, Session.Puzzle.Clues[owner].ColorIndex);
                img.color = new Color(rc.r, rc.g, rc.b, 0.92f);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.sizeDelta = new Vector2(rect.W * Cell - 2f, rect.H * Cell - 2f);
                img.rectTransform.anchoredPosition = new Vector2(
                    -(Cols * Cell) / 2f + (rect.X + rect.W * 0.5f) * Cell,
                     (Rows * Cell) / 2f - (rect.Y + rect.H * 0.5f) * Cell);
                img.enabled = true;
            }
        }

        /// <summary>Hides the drag preview (called on win overlay open).</summary>
        public void ClearPreview()
        {
            if (_preview != null) _preview.enabled = false;
            _dragging = false;
        }

        public void ApplyTheme()
        {
            Rebuild();
        }

        // ------------------------------- input -----------------------------------

        public void OnPointerDown(PointerEventData e) => Begin(e);

        void Begin(PointerEventData e)
        {
            if (Session == null || Session.IsSolved) return;
            if (_flash != null) { StopCoroutine(_flash); _flash = null; }   // cancel invalid flash
            if (!TryCell(e.position, out int c, out int r)) return;
            _dragging = true;
            _dc0 = _dc1 = c;
            _dr0 = _dr1 = r;
            UpdatePreview();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            if (!TryCell(e.position, out int c, out int r)) return;
            if (c != _dc1 || r != _dr1)
            {
                _dc1 = c; _dr1 = r;
                UpdatePreview();
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (TryCell(e.position, out int c, out int r)) { _dc1 = c; _dr1 = r; }
            var rect = PuzzleSession.Snap(_dc0, _dr0, _dc1, _dr1, Cols, Rows);
            if (!Session.CouldPlace(rect)) _flash = StartCoroutine(FlashInvalid(rect));
            Session.Place(rect);
        }

        /// <summary>A tap without a drag still places a 1x1 rect on that cell.</summary>
        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            OnEndDrag(e);
        }

        bool TryCell(Vector2 screenPos, out int col, out int row)
        {
            col = row = -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, null, out var local))
                return false;
            float u = local.x + Cols * Cell * 0.5f;
            float v = Rows * Cell * 0.5f - local.y;
            col = Mathf.FloorToInt(u / Cell);
            row = Mathf.FloorToInt(v / Cell);
            return col >= 0 && col < Cols && row >= 0 && row < Rows;
        }

        void UpdatePreview()
        {
            if (Session == null || _preview == null) return;
            var rect = PuzzleSession.Snap(_dc0, _dr0, _dc1, _dr1, Cols, Rows);
            bool ok = Session.CouldPlace(rect);
            _preview.color = ok
                ? new Color(0.30f, 0.85f, 0.35f, 0.5f)
                : new Color(0.92f, 0.22f, 0.22f, 0.42f);
            _preview.enabled = true;
            FitPreview(rect);
        }

        void FitPreview(in RectI rect)
        {
            _preview.rectTransform.anchorMin = _preview.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _preview.rectTransform.sizeDelta = new Vector2(rect.W * Cell - 3f, rect.H * Cell - 3f);
            _preview.rectTransform.anchoredPosition = new Vector2(
                -(Cols * Cell) / 2f + (rect.X + rect.W * 0.5f) * Cell,
                 (Rows * Cell) / 2f - (rect.Y + rect.H * 0.5f) * Cell);
        }

        /// <summary>Red flash on a rejected drop so invalid taps are visible, not just audible.</summary>
        System.Collections.IEnumerator FlashInvalid(RectI rect)
        {
            _preview.color = new Color(0.92f, 0.22f, 0.22f, 0.55f);
            _preview.enabled = true;
            FitPreview(rect);
            yield return new WaitForSeconds(0.16f);
            _preview.enabled = false;
            _flash = null;
        }

        /// <summary>Sits on the invisible full-board input image and forwards events.</summary>
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
