using System.Collections.Generic;

namespace GridCut.Core
{
    /// <summary>
    /// Assigns a palette color index (0..PaletteColors-1) to every clue so that two
    /// solution rectangles sharing an edge never share a color. Greedy, deterministic.
    /// </summary>
    public static class LevelColors
    {
        public static int[] AssignColorIndices(Puzzle puzzle, List<RectI> solution)
        {
            int n = puzzle.ClueCount;
            var colors = new int[n];
            var adjacency = BuildAdjacency(solution, n);

            for (int i = 0; i < n; i++)
            {
                bool[] used = new bool[PuzzleGenerator.PaletteColors];
                for (int j = 0; j < adjacency[i].Count; j++)
                    used[colors[adjacency[i][j]]] = true;

                int pick = 0;
                while (pick < PuzzleGenerator.PaletteColors - 1 && used[pick]) pick++;
                colors[i] = pick;
            }
            return colors;
        }

        private static List<int>[] BuildAdjacency(List<RectI> rects, int n)
        {
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>(4);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (rects[i].SharesEdge(rects[j]))
                    {
                        adj[i].Add(j);
                        adj[j].Add(i);
                    }
            return adj;
        }
    }
}
