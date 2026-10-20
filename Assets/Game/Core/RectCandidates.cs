using System.Collections.Generic;

namespace GridCut.Core
{
    /// <summary>
    /// Enumerates every valid candidate rectangle for a clue: contains the clue cell,
    /// has area equal to the clue value, stays in bounds, and contains no other clue.
    /// </summary>
    public static class RectCandidates
    {
        public static List<RectI>[] Build(Puzzle puzzle)
        {
            var result = new List<RectI>[puzzle.ClueCount];
            for (int c = 0; c < puzzle.ClueCount; c++)
                result[c] = ForClue(puzzle, c);
            return result;
        }

        public static List<RectI> ForClue(Puzzle puzzle, int clueIndex)
        {
            var clue = puzzle.Clues[clueIndex];
            int v = clue.Value;
            var list = new List<RectI>(16);

            // w <= h first so the factor pair is visited once, then the mirrored size
            for (int w = 1; w * w <= v && w <= puzzle.Cols; w++)
            {
                if (v % w != 0) continue;
                int h = v / w;
                if (h > puzzle.Rows) continue;

                TryAdd(puzzle, clueIndex, w, h, list);
                if (h != w && h <= puzzle.Cols && w <= puzzle.Rows)
                    TryAdd(puzzle, clueIndex, h, w, list);
            }
            return list;
        }

        private static void TryAdd(Puzzle puzzle, int clueIndex, int w, int h, List<RectI> list)
        {
            var clue = puzzle.Clues[clueIndex];
            int xMin = clue.Col - w + 1; if (xMin < 0) xMin = 0;
            int xMax = clue.Col; if (xMax > puzzle.Cols - w) xMax = puzzle.Cols - w;
            int yMin = clue.Row - h + 1; if (yMin < 0) yMin = 0;
            int yMax = clue.Row; if (yMax > puzzle.Rows - h) yMax = puzzle.Rows - h;

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    var rect = new RectI(x, y, w, h);
                    if (!ContainsForeignClue(puzzle, rect, clueIndex))
                        list.Add(rect);
                }
            }
        }

        private static bool ContainsForeignClue(Puzzle puzzle, in RectI rect, int clueIndex)
        {
            for (int row = rect.Y; row < rect.Bottom; row++)
                for (int col = rect.X; col < rect.Right; col++)
                {
                    int c = puzzle.ClueIndexAt(col, row);
                    if (c != -1 && c != clueIndex) return true;
                }
            return false;
        }
    }
}
