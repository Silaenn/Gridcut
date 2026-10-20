namespace GridCut.Core
{
    /// <summary>
    /// Level progression table: grid size per level band and the target difficulty score.
    /// Score scale is 1..10. Breath levels (every 10th) get 30% off; the first level of
    /// a new grid size gets 20% off.
    /// </summary>
    public static class LevelPlan
    {
        public const int TotalLevels = 150;

        public static GridSize SizeForLevel(int level)
        {
            if (level <= 3) return new GridSize(4, 4);      // tutorial
            if (level <= 7) return new GridSize(5, 4);      // warm-up: 5x4
            if (level <= 12) return new GridSize(5, 5);     // warm-up: 5x5
            if (level <= 30) return new GridSize(6, 5);
            if (level <= 55) return new GridSize(8, 6);
            if (level <= 85) return new GridSize(10, 7);
            if (level <= 120) return new GridSize(12, 8);
            return new GridSize(14, 8);
        }

        public static bool IsTutorial(int level) => level >= 1 && level <= 3;

        public static bool IsBreather(int level) => level >= 1 && level % 10 == 0;

        /// <summary>First level that starts a new size band.</summary>
        public static int[] FirstLevelOfEachBand =
            { 1, 4, 8, 13, 31, 56, 86, 121 };

        public static bool IsFirstOfNewSize(int level)
        {
            for (int i = 0; i < FirstLevelOfEachBand.Length; i++)
                if (FirstLevelOfEachBand[i] == level) return true;
            return false;
        }

        /// <summary>Target difficulty score for level n (1-based), tolerance applied by the caller.</summary>
        public static float TargetScore(int level)
        {
            float s = 1f + 9f * (level - 1) / 149f;
            if (IsBreather(level)) s *= 0.7f;
            if (IsFirstOfNewSize(level)) s *= 0.8f;
            return s;
        }
    }

    public struct GridSize
    {
        public int Cols, Rows;
        public GridSize(int cols, int rows) { Cols = cols; Rows = rows; }
        public int Cells => Cols * Rows;
    }
}
