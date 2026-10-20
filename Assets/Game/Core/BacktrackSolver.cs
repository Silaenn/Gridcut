using System.Collections.Generic;

namespace GridCut.Core
{
    /// <summary>
    /// Independent backtracking solver used to verify the unique-solution property.
    /// It uses no deduction heuristics: for each clue, try every surviving candidate,
    /// count complete valid selections, and stop at 2.
    /// </summary>
    public static class BacktrackSolver
    {
        /// <summary>1 or 2. 0 only if the puzzle has no solution at all.</summary>
        public static int CountSolutions(Puzzle puzzle, int cap = 2)
        {
            var state = new Search(puzzle, cap);
            return state.SearchAll();
        }

        private sealed class Search
        {
            private readonly Puzzle _p;
            private readonly int _cap;
            private readonly List<RectI>[] _cand;
            private readonly bool[] _fixed;
            private readonly RectI[] _sel;
            private readonly bool[] _covered;
            private readonly int _clueCount;
            private int _found;

            public Search(Puzzle p, int cap)
            {
                _p = p; _cap = cap;
                _cand = RectCandidates.Build(p);
                _clueCount = p.ClueCount;
                _fixed = new bool[_clueCount];
                _sel = new RectI[_clueCount];
                _covered = new bool[p.Cols * p.Rows];
            }

            public int SearchAll()
            {
                Descend();
                return _found >= _cap ? _cap : _found;
            }

            private void Descend()
            {
                if (_found >= _cap) return;

                // pick the most constrained unfixed clue
                int pick = -1, bestCount = int.MaxValue;
                for (int c = 0; c < _clueCount; c++)
                {
                    if (_fixed[c]) continue;
                    int n = _cand[c].Count;
                    if (n == 0) return; // dead branch
                    if (n < bestCount) { bestCount = n; pick = c; }
                }

                if (pick < 0)
                {
                    // all clues fixed; require full coverage
                    if (AllCovered()) _found++;
                    return;
                }

                var list = _cand[pick];
                for (int i = 0; i < list.Count; i++)
                {
                    var rect = list[i];
                    if (OverlapsAnyFixed(rect)) continue;
                    if (!RectIsFree(rect)) continue;

                    Mark(rect, true);
                    _fixed[pick] = true;
                    _sel[pick] = rect;

                    if (AnyCellUnreachable()) { /* fall through to undo */ }
                    else Descend();
                    if (_found >= _cap) { Undo(pick, rect); return; }

                    Undo(pick, rect);
                }
            }

            private void Undo(int pick, RectI rect)
            {
                _fixed[pick] = false; _sel[pick] = default;
                Mark(rect, false);
            }

            private void Mark(RectI rect, bool on)
            {
                for (int row = rect.Y; row < rect.Bottom; row++)
                    for (int col = rect.X; col < rect.Right; col++)
                        _covered[row * _p.Cols + col] = on;
            }

            private bool OverlapsAnyFixed(RectI rect)
            {
                for (int c = 0; c < _clueCount; c++)
                    if (_fixed[c] && rect.Overlaps(_sel[c])) return true;
                return false;
            }

            private bool RectIsFree(RectI rect)
            {
                for (int row = rect.Y; row < rect.Bottom; row++)
                    for (int col = rect.X; col < rect.Right; col++)
                        if (_covered[row * _p.Cols + col]) return false;
                return true;
            }

            private bool AllCovered()
            {
                for (int i = 0; i < _covered.Length; i++) if (!_covered[i]) return false;
                return true;
            }

            private bool AnyCellUnreachable()
            {
                int w = _p.Cols;
                for (int row = 0; row < _p.Rows; row++)
                    for (int col = 0; col < w; col++)
                    {
                        int idx = row * w + col;
                        if (_covered[idx]) continue;
                        bool reach = false;
                        for (int c = 0; c < _clueCount && !reach; c++)
                        {
                            if (_fixed[c]) continue;
                            var list = _cand[c];
                            for (int j = 0; j < list.Count && !reach; j++)
                                if (list[j].Contains(col, row)) reach = true;
                        }
                        if (!reach) return true;
                    }
                return false;
            }
        }
    }
}
