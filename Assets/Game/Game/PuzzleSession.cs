using System;
using System.Collections.Generic;
using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>
    /// Mutable runtime state of one puzzle at play time. The BoardView renders and
    /// feeds pointer results; session owns the rules:
    /// - a rect is placeable iff, after auto-removing rects it overlaps (spec
    ///   "menimpa"), it holds exactly one clue and its area equals that clue's
    ///   value;
    /// - the puzzle is solved when every clue has a placed rect.
    /// Spec MVP: tap pada persegi terpasang menghapusnya — menggantikan tombol undo.
    /// </summary>
    public class PuzzleSession
    {
        public readonly LevelData Level;
        public readonly Puzzle Puzzle;
        public readonly DateTime StartedAtUtc = DateTime.UtcNow;

        public readonly List<RectI> Placed = new List<RectI>();
        readonly int[] _cellOwner;      // cell -> placed clue index, -1 free
        readonly bool[] _placedClue;    // clue index -> placed?
        readonly List<int> _undo = new List<int>();   // urutan penempatan (paralel dengan Placed)

        public event Action<int, RectI> RectPlaced;   // clueIndex, rect
        public event Action<int, RectI> RectRemoved;  // clueIndex, rect
        public event Action Invalid;                  // rejected input
        public event Action<int> HintUsed;            // clueIndex
        public event Action<bool> SolvedChanged;      // true at solve time

        public int ClueCount => Puzzle.ClueCount;
        public int PlacedCount { get; private set; }
        public const int HintLimit = 3;      // spec: Hint button shows remaining (e.g. 2/3)
        public int HintCount { get; private set; }
        public int HintsLeft => HintLimit - HintCount;
        public bool IsSolved => PlacedCount == ClueCount;
        public bool CanUndo => _undo.Count > 0;

        PuzzleSession(LevelData level, Puzzle puzzle)
        {
            Level = level;
            Puzzle = puzzle;
            _cellOwner = new int[puzzle.Cols * puzzle.Rows];
            for (int i = 0; i < _cellOwner.Length; i++) _cellOwner[i] = -1;
            _placedClue = new bool[puzzle.ClueCount];
        }

        public static PuzzleSession Make(LevelData level)
        {
            var clues = new Clue[level.Clues.Length];
            for (int i = 0; i < level.Clues.Length; i++) clues[i] = level.Clues[i];
            return new PuzzleSession(level, new Puzzle(level.Cols, level.Rows, clues));
        }

        public int PlacedClueAt(int col, int row) => _cellOwner[col + row * Puzzle.Cols];

        /// <summary>Snaps a preview drag between two board cells, inclusive of both endpoints.</summary>
        public static RectI Snap(int c0, int r0, int c1, int r1, int cols, int rows)
        {
            if (c1 < 0) c1 = 0; else if (c1 >= cols) c1 = cols - 1;
            if (r1 < 0) r1 = 0; else if (r1 >= rows) r1 = rows - 1;
            int x0 = Math.Min(c0, c1), x1 = Math.Max(c0, c1);
            int y0 = Math.Min(r0, r1), y1 = Math.Max(r0, r1);
            return new RectI(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>
        /// Spec "menimpa": rect boleh tumpang tindih dengan rect terpasang — yang
        /// lama otomatis dihapus. Valid pasca-penimpaan: tepat satu angka di dalam
        /// rect (rect terpasang yang beririsan ikut dihitung karena akan dihapus)
        /// dan luasnya cocok.
        /// </summary>
        public bool CouldPlace(in RectI rect)
        {
            if (rect.W <= 0 || rect.H <= 0) return false;
            if (rect.X < 0 || rect.Y < 0 || rect.Right > Puzzle.Cols || rect.Bottom > Puzzle.Rows) return false;
            int ci = -1;
            for (int row = rect.Y; row < rect.Bottom; row++)
                for (int col = rect.X; col < rect.Right; col++)
                {
                    int c = Puzzle.ClueIndexAt(col, row);
                    if (c == -1) continue;
                    if (ci != -1 && c != ci) return false;   // dua angka dalam satu rect
                    ci = c;
                }
            if (ci == -1) return false;                      // tidak memuat angka
            if (rect.Area != Puzzle.Clues[ci].Value) return false;
            // clue belum dipakai, ATAU dipakai namun rect lamanya ikut terhapus
            return !_placedClue[ci] || OverlapsAnyPlaced(rect);
        }

        bool OverlapsAnyPlaced(in RectI rect)
        {
            foreach (var r in Placed)
                if (r.Overlaps(rect)) return true;
            return false;
        }

        public void Place(in RectI rect)
        {
            if (IsSolved) return;
            if (!CouldPlace(rect))
            {
                Invalid?.Invoke();
                return;
            }
            int ci = UniqueAnchor(rect);
            // spec menimpa: hapus rect lama yang beririsan sebelum menempatkan
            for (int i = Placed.Count - 1; i >= 0; i--)
                if (Placed[i].Overlaps(rect))
                    RemovePlacedAt(i);
            var r = rect;
            Fill(r, ci);
            Placed.Add(r);
            _placedClue[ci] = true;
            _undo.Add(ci);
            PlacedCount++;
            RectPlaced?.Invoke(ci, r);
            if (IsSolved) SolvedChanged?.Invoke(true);
        }

        int UniqueAnchor(in RectI rect)
        {
            for (int row = rect.Y; row < rect.Bottom; row++)
                for (int col = rect.X; col < rect.Right; col++)
                {
                    int c = Puzzle.ClueIndexAt(col, row);
                    if (c != -1) return c;   // CouldPlace memastikan tepat satu
                }
            return -1;
        }

        void Fill(in RectI r, int owner)
        {
            for (int row = r.Y; row < r.Bottom; row++)
                for (int col = r.X; col < r.Right; col++)
                    _cellOwner[col + row * Puzzle.Cols] = owner;
        }

        void Clear(in RectI r)
        {
            for (int row = r.Y; row < r.Bottom; row++)
                for (int col = r.X; col < r.Right; col++)
                    _cellOwner[col + row * Puzzle.Cols] = -1;
        }

        public void Undo()
        {
            if (IsSolved || _undo.Count == 0) return;
            int ci = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            var r = Placed[Placed.Count - 1];
            Placed.RemoveAt(Placed.Count - 1);
            _placedClue[ci] = false;
            PlacedCount--;
            Clear(r);
            RectRemoved?.Invoke(ci, r);
        }

        public void Reset()
        {
            if (Placed.Count == 0) return;
            for (int i = Placed.Count - 1; i >= 0; i--)
                RemovePlacedAt(i);
        }

        /// <summary>Spec: tap pada persegi terpasang menghapusnya (sel apa pun dalam rect).</summary>
        public bool RemoveRectAt(int col, int row)
        {
            for (int i = Placed.Count - 1; i >= 0; i--)
                if (Placed[i].Contains(col, row))
                {
                    RemovePlacedAt(i);
                    return true;
                }
            return false;
        }

        void RemovePlacedAt(int index)
        {
            var r = Placed[index];
            int ci = _cellOwner[r.X + r.Y * Puzzle.Cols];
            Placed.RemoveAt(index);
            _placedClue[ci] = false;
            PlacedCount--;
            Clear(r);
            _undo.RemoveAll(x => x == ci);
            RectRemoved?.Invoke(ci, r);
        }

        /// <summary>Auto-places the next unsolved clue from the stored solution; returns its rect.</summary>
        public RectI Hint()
        {
            if (IsSolved || HintCount >= HintLimit) return default;
            // solution order matches clue order in the export
            for (int ci = 0; ci < ClueCount; ci++)
            {
                if (_placedClue[ci]) continue;
                var rect = Level.Solution[ci];
                HintCount++;
                Place(rect);
                if (_placedClue[ci]) HintUsed?.Invoke(ci);
                return rect;
            }
            return default;
        }

        /// <summary>Percentage of clues placed, for the progress readout.</summary>
        public float Progress => ClueCount == 0 ? 1f : PlacedCount / (float)ClueCount;
    }
}
