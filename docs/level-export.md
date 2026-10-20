# Ekspor level — skema data dan pipeline

Sumber: spesifikasi bagian 6. Berkas: `Assets/Game/Content/levels.json` (TextAsset, ~85 KB untuk 150 level — jauh di bawah ambang build).

## Pipeline (Tooling, di luar Assets)

```
cd Tooling
dotnet run -c Release -- tests      # 21 test cases
dotnet run -c Release -- soak       # 1000 soal uji keunikan semua ukuran (# criteria Tahap 1)
dotnet run -c Release -- levels     # kalibrasi + ekspor 150 level
dotnet run -c Release -- calibrate  # rentang skor per ukuran
dotnet run -c Release -- verify     # baca ulang levels.json, verifikasi ulang tiap level
```

Generator, solver, filter, dan pewarnaan semuanya dari `GridCut.Core` (murni C#) — Tooling hanya
pembungkus. Jangan edit logika soal di Tooling.

## Skema `levels.json`

```json
{
  "version": 1,
  "levels": [
    {
      "id": 1,
      "cols": 4, "rows": 4,
      "tutorial": true,
      "seed": 6,
      "score": 3.0,
      "clues":   [{"c":0,"r":2,"v":8,"color":1}, ...],
      "solution":[{"x":0,"y":0,"w":2,"h":4}, ...]
    }
  ]
}
```

- `clues[i]` dan `solution[i]` berindeks sejajar: `v` = luas rect, rect berisi clue `i`,
  `color` = indeks warna (0..5) yang ditetapkan saat ekspor (serakah; dua area bertetangga
  tak pernah berwarna sama). Pemain selalu melihat warna yang sama untuk clue yang sama.
- `solution` hanya untuk: pemeriksaan kemenangan (grid penuh), hint (Tahap 5 pemilihan sasaran),
  dan pewarnaan. Jangan dikirim ke klien dalam bentuk mentah bila nanti ada mode kompetisi —
  untuk MVP jaringan tidak ada, jadi aman dikirim dengan file.
- `score` di skala 1..10 setelah kalibrasi: skor mentah solver (Σbobot/nClue + 2×bobot maks)
  dinormalisasi per ukuran via jangkar persentil p5–p95 sampel 500 soal per ukuran.

## Properti yang dijamin

- Tepat satu solusi (diveri ulang verifier: backtrack + deduksi).
- Solusi selalu deduktif (pemain tak perlu menebak; teknik 1–4 GDD yang menyelesaikannya).
- 1x1 ≤ 15% rect; ≥ 30% rect luas ≥ 4 di grid ≥ 8x6; maks 70% linier penuh.
- Level tidak duplikat (hash kanonik termasuk rotasi/pencerminan).
- Level 1–3 ditandai `tutorial`; 3 bintang selalu untuk level tutorial (Tahap 4).
