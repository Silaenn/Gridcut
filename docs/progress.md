# Progres GridCut — jurnal tahap

Perbarui tiap akhir sesi kerja. Angka ukuran/waktu dicatat sesuai instruksi spesifikasi bagian 9.

## Status tahap

| Tahap | Kriteria | Status |
|-------|----------|--------|
| 0. Pra-produksi | Syarat portal tercatat; ukuran build kosong diukur | ⏳ dokumen portal selesai; ukuran build kosong belum diukur (butuh editor) |
| 1. Inti logika | 1.000 soal semua ukuran grid, nol multi-solusi, semua tes lulus | ✅ (19 Okt 2026) |
| 2. Gray-box | 3 orang tanpa petunjuk bisa memainkan, tidak sering salah sentuh | 🟡 kode gameplay lengkap ditulis; uji kompilasi/editor menunggu batch |
| 3. Konten | 150 level terekspor, skor dalam rentang target, 15 level dimainkan sendiri | 🟡 150 level terekspor & lolos verifikasi unik+deduktif; fidelity skor masih titik awal (band menengah delta ~3); uji rasa 15 level menyusul |
| 4. Meta-game | — | 🟡 menu, level select berhalaman, tema (5), suara, bintang, save — ditulis; uji menunggu |
| 5. Visual & audio | — | 🟡 tema 5 palet, SFX prosedural (place/invalid/win/unlock), tanpa asset audio |
| 6. Integrasi & optimasi | — | 🟡 IAdService (stub + porta define), SafeArea, InputSystemUIInputModule; build WebGL menunggu |
| 7. QA & submit | — | ⬜ |

## Yang sudah dibuat

- `docs/portal-requirements.md` — hasil riset tiga portal (bagian tahap 0).
- `Assets/Game/Core/` — modul murni C# (tanpa UnityEngine), asmdef `GridCut.Core` non-engine-ref:
  - `Prng.cs` — xoshiro256** + SplitMix64, deterministik per seed.
  - `Grid.RectI.cs` / `Grid.Puzzle.cs` — persegi panjang integer, puzzle, clue.
  - `RectCandidates.cs` — semua kandidat valid satu clue.
  - `BacktrackSolver.cs` — hitung jumlah solusi (berhenti di 2) untuk uji keunikan.
  - `DeductionSolver.cs` — 4 teknik deduksi lengkap dengan bobot GDD + skor kesulitan.
  - `PuzzleGenerator.cs` — partisi acak → tempatkan clue → cek keunikan → saringan pola + hash kanonik.
  - `LevelPlan.cs` ukuran & kurva skor per level; `LevelColors.cs` pewarnaan serakah dengan 6 warna; `LevelJson.cs` writer/parser; `LevelSetBuilder.cs` pipeline ekspor + kalibrasi.
- `Assets/Tests/EditMode/` — asmdef Unity Test Runner + bridge NUnit ke `TestCases`.
- `Tooling/` — konsol dotnet (di luar Assets, tidak ikut build Unity): `tests | testslarge | soak | levels | calibrate | verify`.
- `Assets/Game/Content/Resources/levels.json` — 150 level (dipindah ke folder Resources agar bisa dimuat runtime via `Resources.Load`).
- `Assets/Game/Game/` — lapisan game (referensi UnityEngine, asmdef `GridCut.Game` → `GridCut.Core`), semua UI dibangun lewat kode lewat `[RuntimeInitializeOnLoadMethod]` sehingga scene kosong cukup:
  - `GameRoot.cs` — bootstrap: save, repository, audio, canvas (ScreenSpaceOverlay + CanvasScaler 1280x800 match 0.5) + SafeArea + EventSystem (`InputSystemUIInputModule`, project pakai Input System only), Three screens routing Home/LevelSelect/Gameplay.
  - `GameplayScreen.cs` — HUD (level, progres clue, waktu), tombol Undo/Reset/Hint/Menu, overlay menang (bintang + waktu), layout adaptif: panel kanan 360 unit (lanskap) / strip bawah (potret).
  - `PuzzleSession.cs` — state runtime satu puzzle: validasi (rect = sel bebas + tepat 1 clue belum terletak + luas = nilai clue, identik dengan definisi RectCandidates), Undo/Reset/Hint (pop dari solusi), event win.
  - `BoardView.cs` — render board: bg CellEmpty, garis GridLine, label clue TMP, fill per clue warna palet; input pointer down/drag/up dengan preview snap berwarna valid/invalid; tap 1x1 juga diterima; rebuild penuh saat resize (murah, maks 14x8 sel).
  - `HomeScreen.cs` / `LevelSelectScreen.cs` — menu (Lanjutkan/Pilih Level/Suara/Reset 2-langkah), pilih level berhalaman 45 per halaman dengan bintang; tema 5 swatch terkunci sesuai progres.
  - `ThemeApplier.cs` + `ThemeLibrary.cs` — 5 tema (Kertas bawaan, Malam L10, Laut L30, Hutan L60, Neon L100), langganan per-role warna.
  - `AudioService.cs` — SFX prosedural (tak ada asset audio di build): place/invalid/tap/win/unlock; AudioService.Play mute via SaveStore.
  - `SaveStore.cs` — PlayerPrefs: unlocked, bintang terbaik (packed "id:stars;..."), suara, tema.
  - `AdService.cs` — IAdService + NullAdService + define portal (`GRIDCUT_CRAZYGAMES` dsb.) untuk integrasi Tahap 6.
- `Assets/Editor/GridCutEditor.cs` — batch: `PrepareProject` (import TMP Essentials bila belum, hapus sisa URP GraphicsSettings, buat `Assets/Scenes/Main.unity` kosong, pasang Build scène list), `BuildWebGL`.

## Keputusan teknis penting

1. **Solver deduksi harus selalu valid (sound)**:
   - Hipotesis (rule 4) yang berhasil MENYELESAIKAN puzzle TIDAK boleh diadopsi: penyelesaian lewat anggapan tidak menjamin keunikan solusi (contoh nyata: puzzle 2x2 dua clue bernilai 2 punya dua solusi; anggapan pertama selesai tanpa kontradiksi). Hanya kontradiksi yang boleh mengeliminasi kandidat.
   - Rule 3 dieksekusi sebagai "andaikan kandidat X dipilih; jika sisanya provably buntu → X mustahil, eliminasi" — bukan pencarian terselubung.
2. **Dua solver saling memeriksa**: backtracking bebas heuristik berlaku sebagai pemeriksa; seluruh 1000 soal oleh generator hanya diterima bila keduanya sepakat (tepat satu solusi dan solver deduksi selesai).
3. **Saringan pola**: 1x1 ≤ 15%; ≥ 30% persegi berluas ≥ 4 untuk grid ≥ 8x6 (48 sel); maksimum 70% persegi penuh selebar/setinggi grid de-duplicate hash kanonik (termasuk rotasi/pencerminan).
4. **Proyek**: manifest dibersihkan (Timeline, Visual Scripting, Multiplayer, paket 2D tak terpakai, modul fisika/partikel/terrain/video dihapus); color space diubah ke Gamma — efeknya masuk saat Unity di-restart.
5. **Kalibrasi skor** (temuan penting):
   - Skor mentah solver **diskret** dan berlompatan besar: soal yang diselesaikan hanya lewat rule 1 bernilai persis 3,0 (dan dinormalisasi jadi 1,0); begitu rule 2 menyentuh, loncat ke norm ≥ 4,2; rule 4 (bobot 8) melonjakkan raw ke ≥ 16. Karena itu skala 1..10 spesifikasi GDD tidak mungkin dipetakan mentah.
   - Solusi yang dipakai: normalisasi linear per ukuran via jangkar persentil p5–p95 dari sampel 500 soal (`LevelSetBuilder.Normalize`) + toleransi tepi bawah (nilai lantai diterima saat target berada di dekat lantai) + **best-fit terdekat** bila seeds/band benar-benar kosong, ditandai `TimeOut` di statistik supaya terlihat untuk kalibrasi manual (spesifikasi bagian 6: "ukuran grid dinaikkan lebih cepat, bukan skor dipaksakan"). Fidelity skor adalah titik awal dan disetel saat playtest.
   - **Kandidat di-pool per band ukuran**: setiap band ukuran grid menyeluruh seed sekali (hingga ±200 ribu) mengumpulkan kandidat valid unik, lalu target level dalam band diurutkan dan definition dengan kandidat terdekat; ini menghilangkan pembuangan bersama waktu (sebelumnya tiap level memindai kembali seed 1..60000) → ekspor 150 level kini ~1,5 detik.
   - Peta skor CDF persentil sempat dicoba dan DITOLAK: frekuensi puzzle mudah tinggi membuat puzzle mudah mendapatkan nilai tinggi di skala.
   - **Batas dketahui**: formula skor GDD (Σbobot/nClue + 2×bobot terberat) punya rentang hanya beralih di 1+2×w (w = bobot langkah maks), sehingga band menengah (misal 8x6 target ~3-4,5) jarang terhuni mass sempurna ±0,4. Semua level tetap valid, tapi delta ke target yakni terbesar ~3 di band menengah; jadikan titik setel saat kalibrasi playtest (spec mengizinkan: "rumus skor awal heuristik, disetel saat kalibrasi").

## Cara kerja tool

```
cd Tooling
dotnet run -c Release -- tests     # 21 test cases, beberapa detik
dotnet run -c Release -- soak      # 1000 soal uji unik semua ukuran (~1 dtk)
dotnet run -c Release -- levels    # kalibrasi + 150 level -> Assets/Game/Content/levels.json
dotnet run -c Release -- calibrate # hanya rentang skor per ukuran
```

## Rencana lanjutan

- Tutup Unity GUI editor (user) → jalankan batch: `-executeMethod GridCut.EditorTools.GridCutEditor.PrepareProject -quit` lalu `-runTests -testPlatform EditMode`, lalu `BuildWebGL` (ukur).
- Fix tampak yang muncul dari batch/test (potensi: TMP Essentials, API signature, layout perangkat).
- Tahap 7: checklist QA otomatis (ukuran, waktu load, console bersih), lalu submit portal.

## Keputusan penting sesi ini

1. **UI dibangun dari kode** melalui `[RuntimeInitializeOnLoadMethod]` — tidak ada YAML scene manual; scene `Main.unity` dibuat kosong oleh editor batch.
2. **Panel kanan 360 canvas unit** (bukan px) — sesuai CanvasScaler, jadi konsisten lintas perangkat; di potret berpindah ke strip bawah otomatis.
3. **Input System only** (project activeInputHandler=1): UI pakai `InputSystemUIInputModule`, bukan StandaloneInputModule.
4. **Validasi pemain ≡ definisi RectCandidates**: rect valid ⇔ hanya sel bebas + berisi tepat satu clue yang belum terisi + luasnya = nilai clue; semua penempatan sah, sehingga soal tetap berpenyelesaian unik (undo tersedia untuk buntu).
5. **Hit/validasi hint tidak latih**: hint menempatkan rect sesuai `Level.Solution[ci]` (urutan clue ≡ urutan solusi pada ekspor).
6. levels.json dipindah ke `Assets/Game/Content/Resources/` → `Resources.Load("levels")`; tooling path ikut diubah, verify masih lulus.
7. Asset sisa URP & template 2D dihapus dari `Assets/Settings`.

## Aturan umum UI (dari spec, 20 Okt 2026) — status

1. ✅ CanvasScaler 1920×1080, Scale With Screen Size, Match = 1 (height) — scene `Main.unity`.
2. ✅ SafeArea (root rect tunggal di bawah Canvas semua layar) — sudah ada.
3. ✅ Target sentuh: tombol Home/Gameplay/LevelSelect/Win/Confirm 150 akuan; grid level sel 150×150; baris halaman 150; spacing antar tombol ≥ 24; GridCutRoot sisa bootstrap dihapus dari scene.
4. ✅ 3 keadaan tombol: `ButtonFX` (skala 0.95 saat ditekan), disabled alpha 0.4 (dibake di AppRoot + warna terkunci ala 0.4 di Home/LevelSelect).
5. ✅ Transisi layar: fade + geser kecil (+32 unit x), 200 ms, `Time.unscaledDeltaTime` (AppRoot.FadeIn, CanvasGroup per layar).
6. ✅ Escape/Back desktop: `Keyboard.current` → `AppRoot.NavBack()` dengan riwayat layar (Home→Select→Game); tombol Back panel gameplay & level select memakai NavBack.
- Legacy 1280×800 → seluruh nilai UI (offsets/sizeDelta/font/grid) diskala ×1.35 ke koordinat 1080; rebake presisi per layar diterapkan langsung ke `Main.unity` (Home 2 tombol bawah berganding, gameplay HUD grid 2×2, overlay menang kartu 700×640).

## Sesi 20 Okt (sore): statik lintas-Play & wiring

- **Akar MissingReference klik tema**: `EditorSettings.asset m_EnterPlayModeOptions: 1` (domain reload mati) → static `ThemeApplier._subscribers` selamat lintas sesi Play membawa closure objek sesi lama yang sudah hancur.
- **Fix**: reset statik saat play mulai (`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, editor-only); semua langganan tema kini simetris — `Subscribe` di `OnEnable` (applyImmediately: re-tint saat layar muncul), `Unsubscribe` di `OnDisable`; `AppRoot` membereskan Background/LevelFinished di `OnDestroy`; `Subscribe` idempoten (dedupe per role+cb).
- **Auto-wire per layar** (`UiFactory.Wire/WireGo`): ref scene yang gagal ter-bind dipulihkan dari hierarki via nama child; warning Tint kini menyebut nama tombol.
- **Per-screen Show() dipulihkan**: `AppRoot.Switch` kini memanggil `Home.Show()`/`LevelSelect.Show()` (dulu hilang saat refactor — grid level kosong).
- **Swatch tema nimbun**: rebake lupa isi posisi x saat override ukuran → kelima Theme0–4 jadi `pos=(0,0)` bertumpuk; dipulihkan ke x = −458/−229/0/+229/+458 (gap 24).
- Status perbaikannya menunggu verifikasi Play user (Tahap 7 QA checklist belum dimulai).
- **Sweep glyph font**: atlas TMP Essentials tidak memuat blok simbol — teks scene yang pakai `◀▶↺↻→✨●` diganti aman (`«»`, teks polos), bintang ★☆ jadi sprite prosedural (`UiFactory.StarSprite/StarRow/SetStars`). Pemindaian `m_text` scene: tanpa escape eksotik tersisa.

## Gameplay §spec re-audit (21 Okt) — pasangan spec vs implementasi

- ✅ sudah: tanpa bar atas; grid tengah area; floor(min(area/cols, area/rows)); garis 2 acuan; tutorial L1–3 (baru).
- ✅ difix: cap sel 150 acuan (Val ×1.35 → 202.5), margin area 48 acuan (64.8), font clue 0,5×sel clamp 36–72 (49–97), sisa hint `Hint N/3` (PuzzleSession.HintLimit), overlay tutorial di atas grid.
- ✅ panel kolom vertikal (keputusan user: lebar 486 = ×1.35; urutan Menu « → Level+Progress/Waktu → Hint → Undo → Reset → Mute bawah): tombol 438×160 (≥150 sentuh), gap 24, pad 24 — total pas 1080.
- ✅ mode ringkas: sel nyata < 32 px (hitung `Cell × Screen.height/1080`) → panel 297, label status tersembunyi, font tombol 26 (dipilih per level sebelum board dibangun; BoardView.EstimateCell = sumber tunggal).
- Panel scene dibake ulang via YAML (GridLayoutGroup HUD 2×2 ternyata tidak pernah ada — anak panel diposisikan manual; MuteBtn klon UndoBtn id 12011–12019; nama & rantai refs terverifikasi unik).
