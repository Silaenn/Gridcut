namespace GridCut.Core
{
    /// <summary>
    /// Level progression table: grid size per level band and the target difficulty score.
    /// Score scale is 1..10 on a linear, monotonic curve. Early bands are short so the
    /// first big board (7x7) arrives at level 21 instead of after a long easy stretch.
    /// Inside each band LevelSetBuilder spreads candidates over that band's target span.
    /// </summary>
    public static class LevelPlan
    {
        public const int TotalLevels = 150;

        public static GridSize SizeForLevel(int level)
        {
            if (level <= 5) return new GridSize(5, 5);      // tutorial + dua pemanasan
            if (level <= 12) return new GridSize(6, 5);
            if (level <= 20) return new GridSize(6, 6);     // kotak pertama
            if (level <= 34) return new GridSize(7, 7);     // papan besar pertama
            if (level <= 49) return new GridSize(8, 6);
            if (level <= 66) return new GridSize(9, 7);
            if (level <= 87) return new GridSize(10, 8);
            if (level <= 116) return new GridSize(12, 8);
            return new GridSize(14, 8);
        }

        public static bool IsTutorial(int level) => level >= 1 && level <= 3;

        /// <summary>Target difficulty score for level n (1-based).
        /// Linear 1..10: roughly +0.6 every 10 levels, so each chapter feels like a
        /// step without a wall. Within a size band the builder spreads candidates
        /// across exactly this target range (see LevelSetBuilder.BuildBand).</summary>
        public static float TargetScore(int level)
        {
            return 1f + 9f * (level - 1) / 149f;
        }
    }

    public struct GridSize
    {
        public int Cols, Rows;
        public GridSize(int cols, int rows) { Cols = cols; Rows = rows; }
        public int Cells => Cols * Rows;
    }
}
