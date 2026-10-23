using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>Five themes, palette per spec bagian 7. Hex codes are the starting points.</summary>
    public static class ThemeLibrary
    {
        public sealed class Theme
        {
            public string Name;
            public string UnlockLevelHint;   // level at which theme unlocks (display)
            public int UnlockLevel;
            public string Background, CellEmpty, GridLine, Board, Locked, Lock, Text, Accent;
            public string[] Rects;           // 6 colors

            public string Desc => $"{Name} (setelah level {UnlockLevel} selesai)";
        }

        public static readonly Theme Paper = new Theme
        {
            Name = "Kertas", UnlockLevel = 1, UnlockLevelHint = "bawaan",
            Background = "#F5F1E8", CellEmpty = "#FBF9F4", GridLine = "#D2C9B6",
            Board = "#EAE1CE",   // warna Papan sementara — di-slot ulang dari tabel bag. 4
            Locked = "#DFDCD2",  // warna Ubin terkunci sementara s/d tabel bag. 4
            Lock = "#A9A293",    // warna Gembok sementara s/d tabel bag. 4
            Text = "#2B2A33", Accent = "#E4572E",
            Rects = new[] { "#A8DADC", "#F6BD60", "#F7A8B8", "#B5E48C", "#C7B8EA", "#FFD6A5" }
        };

        public static readonly Theme Night = new Theme
        {
            Name = "Malam", UnlockLevel = 10, UnlockLevelHint = "level 10",
            Background = "#1E1F2B", CellEmpty = "#242636", GridLine = "#3A3D52",
            Board = "#2F3348",   // sementara s/d tabel bag. 4
            Locked = "#282A3A",
            Lock = "#757CA0",
            Text = "#ECEFF4", Accent = "#FFB454",
            Rects = new[] { "#5CC8FF", "#FF8FA3", "#8BE28B", "#C79BFF", "#FFD166", "#66E0C2" }
        };

        public static readonly Theme Sea = new Theme
        {
            Name = "Laut", UnlockLevel = 30, UnlockLevelHint = "level 30",
            Background = "#E6F4F5", CellEmpty = "#F4FBFB", GridLine = "#A9D3D6",
            Board = "#BFDCE0",   // sementara s/d tabel bag. 4
            Locked = "#D2E1E3",
            Lock = "#7BA0A6",
            Text = "#12394A", Accent = "#0E7C86",
            Rects = new[] { "#7FD1D8", "#F9C784", "#F59DA6", "#A3D9A5", "#B7A7E8", "#FFE08A" }
        };

        public static readonly Theme Forest = new Theme
        {
            Name = "Hutan", UnlockLevel = 60, UnlockLevelHint = "level 60",
            Background = "#EAF0E4", CellEmpty = "#F5F8F1", GridLine = "#B5C9A7",
            Board = "#C3D3B4",   // sementara s/d tabel bag. 4
            Locked = "#D6DECC",
            Lock = "#8CA081",
            Text = "#23361F", Accent = "#D9822B",
            Rects = new[] { "#9BD28A", "#F2C26B", "#E89A7B", "#8CC7C0", "#C9A7DA", "#F0E08A" }
        };

        public static readonly Theme Neon = new Theme
        {
            Name = "Neon", UnlockLevel = 100, UnlockLevelHint = "level 100",
            Background = "#0F0F1A", CellEmpty = "#151526", GridLine = "#34345A",
            Board = "#1F1F35",   // sementara s/d tabel bag. 4
            Locked = "#161625",
            Lock = "#8F8FD6",
            Text = "#F4F4FF", Accent = "#FF2E93",
            Rects = new[] { "#00E5FF", "#FF6B6B", "#B6FF00", "#FFB400", "#9B5CFF", "#00FF9C" }
        };

        public static readonly Theme[] All = { Paper, Night, Sea, Forest, Neon };
    }
}
