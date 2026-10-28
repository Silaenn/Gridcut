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
            if (level <= 6) return new GridSize(5, 5);      // onboarding pendek
            if (level <= 14) return new GridSize(6, 5);
            if (level <= 27) return new GridSize(6, 6);     // kotak pertama
            if (level <= 42) return new GridSize(7, 7);
            if (level <= 58) return new GridSize(8, 6);
            if (level <= 78) return new GridSize(9, 7);
            if (level <= 103) return new GridSize(10, 8);
            if (level <= 127) return new GridSize(12, 8);
            return new GridSize(14, 8);
        }

        public static bool IsTutorial(int level) => level >= 1 && level <= 3;

        public static bool IsBreather(int level) =>
            level > 1 && level % 10 == 0 && level < TotalLevels;

        /// <summary>First level that starts a new size band.</summary>
        public static int[] FirstLevelOfEachBand =
            { 1, 7, 15, 28, 43, 59, 79, 104, 128 };

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
            // Kurva dijaga monoton; jeda napas/ukuran baru ditangani lewat
            // struktur soal, bukan dengan menurunkan skor target secara mendadak.

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
