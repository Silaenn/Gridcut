namespace GridCut.Core
{
    /// <summary>Integer rectangle, half-open interval [X, X+W) x [Y, Y+H).</summary>
    public readonly struct RectI
    {
        public readonly int X;
        public readonly int Y;
        public readonly int W;
        public readonly int H;

        public RectI(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }

        public int Area => W * H;
        public int Right => X + W;
        public int Bottom => Y + H;

        public bool Contains(int col, int row) =>
            col >= X && col < Right && row >= Y && row < Bottom;

        /// <summary>Strict area overlap (corner touch does not count).</summary>
        public bool Overlaps(in RectI o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;

        /// <summary>True when the two rects share at least one edge segment (enclose adjacency).</summary>
        public bool SharesEdge(in RectI o)
        {
            // vertical edge overlap: o to the left or right; horizontal bands overlap
            if (X == o.Right || o.X == Right)
                return Y < o.Bottom && o.Y < Bottom;
            if (Y == o.Bottom || o.Y == Bottom)
                return X < o.Right && o.X < Right;
            return false;
        }
    }

    /// <summary>A numbered clue cell. Value equals the area of its rectangle.</summary>
    public readonly struct Clue
    {
        public readonly int Col;
        public readonly int Row;
        public readonly int Value;
        public readonly int ColorIndex;

        public Clue(int col, int row, int value, int colorIndex)
        {
            Col = col; Row = row; Value = value; ColorIndex = colorIndex;
        }
    }
}
