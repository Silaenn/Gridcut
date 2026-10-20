# Tahap 0 — Ringkasan syarat portal (riset web)

Diperbarui: 19 Okt 2026. Angka hanya dari dokumentasi developer resmi; yang belum dikonfirmasi ditulis eksplisit.

## CrazyGames (jalur self-serve paling jelas — mulai dari sini)
- Initial load **≤ 50 MB**, ideal **≤ 20 MB** agar masuk homepage mobile (diukur sampai event `Gameplay start` pertama bila SDK terpasang).
- Total ≤ 250 MB, maks 1.500 file. Hanya build terkompresi (Brotli/Gzip); development build tidak jalan. Path relatif saja.
- SDK: CrazySDK Unity (https://sdk.crazygames.com/UnityCrazySDK.unitypackage). Basic Launch: SDK opsional tapi event `Gameplay start` minimal harus ada. Full Launch (iklan/revenue): SDK wajib (Gameplay start/stop, Data module, User module).
- Custom build mereka (`CrazySDK` menu → release build) direkomendasikan; `index.html` klien diabaikan untuk game Unity.
- Iklan: hanya via SDK. Basic Launch = semua iklan mati. Midgame max 1/3 menit (SDK yang mengatur); **game harus pause + mute saat iklan**. Rewarded: wajib ada alternatif gratis, tidak dirantai, tanpa reward saat `adError`.
- Rating: **PEGI 12**. Cover wajib 3 rasio (1920×1080, 800×1200, 800×800) + video preview 15–20 dtk tanpa suara. Tanpa login eksternal di Basic.
- Save: Data module SDK (cloud akun), atau APS. Game Unity **di-disable di iOS secara default** sampai stabil.
- Catatan teknis: cursor lock hanya setelah interaksi; URP di macOS matikan Depth Priming; input mouse bisa loncat abnormal (perlu filter); DPR dikelola mereka (1 di perangkat low-memory). Unity 6.2 opsi build berganti nama "Optimize for code size and build time" → pilih Disk Size with LTO.

## Coolmath Games
- Model: **lisensi non-eksklusif** (license fee). CMG yang meng-upload dan membuat thumbnail sendiri.
- Wajib HTML5, "thinking game", **bebas iklan termasuk iklan milik dev sendiri**, tanpa link keluar termasuk halaman credits (nama boleh, URL/email/sosmed tidak), **tanpa analytics pihak dev**, tanpa name entry.
- SDK: disebut "simple SDK" setelah disetujui; dokumen tidak publik → keputusan: implement via `IAdService` no-op di build CMG sampai SDK diterima. QA tool mereka: https://www.coolmathgames.com/qa-tool.
- Batas ukuran build: **tidak dikonfirmasi** (tidak dipublikasikan).
- Submit: form di https://developers.coolmathgames.com/.

## Armor Games
- Submit via portal + email `mygame@armorgames.com`, wajib mengungkap penggunaan generative AI.
- SDK: Armor Games Interface (AGI) Unity package (prefab + `ag.min.jspre`, api_key). Cloud save: SaveData/LoadGame. Quests, storefront.
- ⚠️ Docs mereka masih menyebut `document.domain = "armorgames.com"` (deprecated di browser modern) — **klarifikasi dulu sebelum integrasi**.
- Batas ukuran & aturan iklan: **tidak dikonfirmasi**.

## Implikasi untuk GridCut
1. Target < 10 MB aman untuk semua portal, tapi build Unity 6 WebGL kosong saja ~12,5 MB (Brotli + LTO) → disiplin optimasi (lihat bagian 2 spec), dan URP lebih baik dihindari/dipangkas (hemat ~3,6 MB) karena game ini flat 2D.
2. Urutan submit praktis: CrazyGames Basic Launch dulu (build < 10 MB + event gameplay start) → Coolmath (build tanpa iklan/link/analytics) → Armor + AGI (quests/cloud save) bila menjalani deal.
3. Ad-ready: satu antarmuka `IAdService` + fungsi pause+mute tunggal dipanggil semua implementasi; rewarded wajib punya alternatif gratis → gunakan untuk bonus hint (bukan syarat).
4. Save: satu payload JSON kecil → sama untuk localStorage (CMG), Data module Crazy (cloud), AGI SaveData (Armor). Simpan versi format + migrasi.
5. Konten: PEGI 12 aman (game ini general audience), tanpa name entry — tidak ada yang harus di-disable karena game tidak punya entry nama.
6. Siapkan aset submit untuk CrazyGames: 3 cover rasio + video 15–20 dtk dua rasio. CMG/Armor buatkan tambahan berdasarkan master thumbnail.
