using GridCut.Core;

namespace GridCut.Game
{
    /// <summary>
    /// Lima tema (spec bagian 4) — nilai hex persis dari tabel. Satu tema menyimpan
    /// 11 peran warna + 6 warna keping; pemakai merujuk peran (ThemeApplier.Role),
    /// bukan hex langsung.
    /// </summary>
    public static class ThemeLibrary
    {
        public sealed class Theme
        {
            public string Name;
            public string UnlockLevelHint;   // level at which theme unlocks (display)
            public int UnlockLevel;

            // 11 peran warna (tabel bag. 4)
            public string Background;   // Latar       — latar penuh layar
            public string Dot;          // Titik       — pola titik latar
            public string CellEmpty;    // Permukaan   — tombol sekunder, kartu, sel grid
            public string GridLine;     // Tepi        — bingkai & bayangan permukaan, bintang kosong
            public string Board;        // Papan       — plat di bawah grid
            public string Primary;      // Utama       — tombol utama, bintang terisi
            public string Accent;       // Aksen       — level aktif, lencana, preview drag
            public string Text;         // Teks        — teks dan ikon
            public string TextMuted;    // Teks redup  — subjudul
            public string Locked;       // Ubin terkunci — isi ubin terkunci
            public string Lock;         // Gembok      — ikon gembok
            public string[] Rects;      // 6 warna keping (dipakai juga huruf logo/kotak melayang)

            // warna turunan (bag. 4): tema Kertas memakai nilai tabel persis,
            // tema lain diturunkan via rumus HSV bag. 3 (V×0,85, S+0,14)
            public string ShadowPrimary;   // bayangan Utama
            public string ShadowAccent;    // bayangan Aksen
            public string ShadowBoard;     // bayangan Papan
            public string[] PieceEdges;    // Tepi keping 1..6 (bayangan Tepi = Tepi persis)

            public string Desc => $"{Name} (setelah level {UnlockLevel} selesai)";
        }

        public static readonly Theme Paper = new Theme
        {
            Name = "Kertas", UnlockLevel = 1, UnlockLevelHint = "bawaan",
            Background = "#FFF1D6", Dot = "#F5DFB0", CellEmpty = "#FFFAEF",
            GridLine = "#EAD9B5", Board = "#E9D3A8", Primary = "#FFC857",
            Accent = "#FF5D73", Text = "#3B2F2F", TextMuted = "#6B5A45",
            Locked = "#F2E5CA", Lock = "#C9B58C",
            Rects = new[] { "#FF8FA3", "#FFC857", "#7BDFF2", "#95E06C", "#B79CED", "#FF9F68" },
            ShadowPrimary = "#D9A22B", ShadowAccent = "#D43F58", ShadowBoard = "#D4BC8A",
            PieceEdges = new[] { "#D9627A", "#D9A22B", "#4FB4C9", "#6AB846", "#8C70C9", "#D9733C" }
        };

        public static readonly Theme Night = new Theme
        {
            Name = "Malam", UnlockLevel = 10, UnlockLevelHint = "level 10",
            Background = "#1E1F2B", Dot = "#272A3B", CellEmpty = "#2C2F45",
            GridLine = "#3F4363", Board = "#3A3E5C", Primary = "#FFB454",
            Accent = "#FF7A90", Text = "#ECEFF4", TextMuted = "#A9B0C6",
            Locked = "#252839", Lock = "#4A4F73",
            Rects = new[] { "#5CC8FF", "#FF8FA3", "#8BE28B", "#C79BFF", "#FFD166", "#66E0C2" },
            ShadowPrimary = "#D98C29", ShadowAccent = "#D94961", ShadowBoard = "#262B4E",
            PieceEdges = new[] { "#30A0D9", "#D95B72", "#5BC05B", "#9865D9", "#D9A938", "#3CBE9E" }
        };

        public static readonly Theme Sea = new Theme
        {
            Name = "Laut", UnlockLevel = 30, UnlockLevelHint = "level 30",
            Background = "#E6F4F5", Dot = "#D2E9EB", CellEmpty = "#F4FBFB",
            GridLine = "#BFE0E3", Board = "#B8DCE0", Primary = "#F9C74F",
            Accent = "#0E7C86", Text = "#12394A", TextMuted = "#4B6F7C",
            Locked = "#D5EAEC", Lock = "#9CC3C8",
            Rects = new[] { "#7FD1D8", "#F9C784", "#F59DA6", "#A3D9A5", "#B7A7E8", "#FFE08A" },
            ShadowPrimary = "#D4A026", ShadowAccent = "#006872", ShadowBoard = "#82B8BE",
            PieceEdges = new[] { "#52B0B8", "#D49C53", "#D06873", "#71B873", "#8772C5", "#D9B657" }
        };

        public static readonly Theme Forest = new Theme
        {
            Name = "Hutan", UnlockLevel = 60, UnlockLevelHint = "level 60",
            Background = "#EAF0E4", Dot = "#DCE6D2", CellEmpty = "#F5F8F1",
            GridLine = "#CBD9BE", Board = "#C5D6B6", Primary = "#F2B84B",
            Accent = "#D9622B", Text = "#23361F", TextMuted = "#55694F",
            Locked = "#DDE7D3", Lock = "#A9BD9A",
            Rects = new[] { "#9BD28A", "#F2C26B", "#E89A7B", "#8CC7C0", "#C9A7DA", "#F0E08A" },
            ShadowPrimary = "#CE9223", ShadowAccent = "#B8420B", ShadowBoard = "#9AB681",
            PieceEdges = new[] { "#71B25C", "#CE9B3E", "#C56F4D", "#5FA9A0", "#A274B9", "#CCBA59" }
        };

        public static readonly Theme Neon = new Theme
        {
            Name = "Neon", UnlockLevel = 100, UnlockLevelHint = "level 100",
            Background = "#0F0F1A", Dot = "#181830", CellEmpty = "#1B1B2E",
            GridLine = "#34345A", Board = "#262647", Primary = "#FFD60A",
            Accent = "#FF2E93", Text = "#F4F4FF", TextMuted = "#A3A3CC",
            Locked = "#14142A", Lock = "#3C3C66",
            Rects = new[] { "#00E5FF", "#FF6B6B", "#B6FF00", "#FFB400", "#9B5CFF", "#00FF9C" },
            ShadowPrimary = "#D9B400", ShadowAccent = "#D9096D", ShadowBoard = "#18183C",
            PieceEdges = new[] { "#00C3D9", "#D93D3D", "#9BD900", "#D99900", "#7130D9", "#00D985" }
        };

        public static readonly Theme[] All = { Paper, Night, Sea, Forest, Neon };
    }
}
