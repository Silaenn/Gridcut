namespace GridCut.Core
{
    /// <summary>
    /// Relic of the GridCut puzzle: split a grid into rectangles, each containing exactly
    /// one clue whose value equals the rectangle's area. Immutable data, no Unity types.
    /// </summary>
    public sealed class Puzzle
    {
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public Clue[] Clues { get; private set; }
        public int ClueCount => Clues.Length;

        private int[] _clueIndexAtCell;

        public Puzzle(int cols, int rows, Clue[] clues)
        {
            Cols = cols; Rows = rows; Clues = clues;
            _clueIndexAtCell = new int[cols * rows];
            for (int i = 0; i < _clueIndexAtCell.Length; i++) _clueIndexAtCell[i] = -1;
            for (int i = 0; i < clues.Length; i++)
                _clueIndexAtCell[clues[i].Row * cols + clues[i].Col] = i;
        }

        public int CellIndex(int col, int row) => row * Cols + col;
        public bool InBounds(int col, int row) => col >= 0 && col < Cols && row >= 0 && row < Rows;

        /// <summary>Index of the clue at this cell, or -1.</summary>
        public int ClueIndexAt(int col, int row) => InBounds(col, row) ? _clueIndexAtCell[col + row * Cols] : -1;
    }
}
