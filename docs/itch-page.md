# GridCut — itch.io page pack

Status: draft, ready to paste. The page can be created now; publish only after the build and screenshots have been checked in a private/draft page.

## What still needs your decision

- itch.io username and wanted URL slug (`gridcut` if free, otherwise `gridcut-puzzle`)
- developer/studio name for the credits (default below: **Silaenn**)
- free, or free with donations
- AI disclosure: recommended **Yes → Code + Text & Dialog** (see below)
- optional trailer link (YouTube/Vimeo)
- whether the WebGL ZIP should be built here before uploading

## Basic information

| Field | Value |
|---|---|
| Title | `GridCut` |
| Project URL | `gridcut` (fallback: `gridcut-puzzle`) |
| Short description / tagline | `Draw rectangles. Match every number. 150 logic puzzles with no guessing.` |
| Classification | Games |
| Kind of project | HTML |
| Release status | Released |
| Pricing | No payments (free). Optional: `$0 or donate`. |
| Genre | Puzzle |
| Average session | A few minutes |
| Visibility | Draft first, then Public |

## Tags (max 10)

Use these, in this order:

1. `puzzle`
2. `logic`
3. `2d`
4. `minimalist`
5. `relaxing`
6. `singleplayer`
7. `brain-training`
8. `touch-friendly`
9. `family-friendly`
10. `unity`

Do not add unrelated tags for reach. itch.io's quality guidelines explicitly penalise that.

## Metadata

| Field | Value |
|---|---|
| Platforms | Browser / HTML5 only. Do **not** tick Windows, macOS, Linux, Android, or iOS unless you upload those builds. |
| Inputs | Mouse, Touchscreen |
| Languages | English only |
| Accessibility | Interactive tutorial (levels 1–3) |
| Multiplayer | Single-player |
| Content | None (no adult content, violence, gambling, flashing imagery) |
| AI disclosure | Yes → Code, Text & Dialog. Graphics: No. Sound: No. |

About the AI disclosure: levels are produced by a deterministic solver-based generator, which itch treats as procedural generation, not generative AI. The code and some UI text were produced with AI assistance, so the honest setting is Code + Text & Dialog. If you prefer not to disclose code, that is your call, but it would not match itch.io's definition.

## Long description

Paste this into the description editor. Select the short section titles and apply **Header 2** from the formatting menu. Keep the bullet list as a bullet list.

```text
GridCut is a small logic puzzle about cutting a grid into rectangles.

Every number on the board belongs to one rectangle and tells you its area. Drag from one cell to another to draw that rectangle. It has to contain exactly one number, cover the right number of cells, and not overlap anything else. Fill the board and the level is done.

That is the whole game. The hard part is working out where each piece goes.

150 levels, one solution each

Every board is checked by a solver before it ships. It has exactly one solution, and the solver can reach it with deduction alone. You should never have to guess, and undo and reset are always there if you get stuck.

The first few levels are a tutorial. After that the boards grow from 5x5 to 14x8. New ideas show up every few levels, but the game never throws a wall at you. If a level takes a while, that is the point.

Features

- 150 levels across nine board sizes
- No timer, no lives, no rush
- 3 hints per level
- Undo and reset at any time
- Stars for solving with fewer hints
- 5 color themes, unlocked as you play
- Progress saves automatically
- Mouse and touch controls
- No ads, no account, no install

Controls

- Mouse: click and drag across the grid
- Touch: drag with one finger
- Tap a single cell to place a 1x1 piece
- Esc or Back: leave the level

Credits

Game design, code, and levels: Silaenn
Music: Cakeflaps, TinyWorlds (CC0)
Sound effects: Kenney, Fupi (CC0)
Font: Fredoka (SIL Open Font License)
Built with Unity.

Bug reports

If a level looks wrong or something breaks, leave a comment with the level number and what happened. Please mention your browser and whether you were on desktop or mobile.

AI disclosure

No AI-generated art, music, or voice is used in the game. Some code and UI text were drafted with AI assistance and reviewed by me.
```

## Shorter blurb (for social posts, devlog, Reddit)

```text
GridCut is a quiet logic puzzle about cutting a grid into rectangles. Every number tells you the area of the piece that covers it. 150 levels, no timer, no guessing. Play in your browser.
```

Indonesian version:

```text
GridCut adalah puzzle logika santai: bagi grid menjadi persegi panjang, lalu cocokkan setiap angka dengan luas kepingnya. 150 level, tanpa timer, tanpa menebak. Bisa dimainkan langsung di browser.
```

## Page theme

Use the Paper palette from the game so the page matches the screenshots:

| Element | Hex |
|---|---|
| Background | `#FFF1D6` |
| Background 2 / content column | `#FFFAEF` |
| Text | `#3B2F2F` |
| Links | `#FF5D73` |
| Buttons | `#FFC857` |
| Headers | `#3B2F2F` |

Fonts: **Fredoka** for headers, **Nunito** for body text. Both are on Google Fonts and available in the itch theme editor.

Layout: keep the default **single column** for an HTML game so the game gets the full content width. Upload the screenshots anyway, then insert two or three of them into the description below the intro. For HTML5 pages itch hides the screenshot sidebar by default.

Banner: use `banner-1920x1080.png` if you want a header image. It is optional.

## Assets in this repo

| File | Use |
|---|---|
| `docs/itch/cover-630x500.png` | itch cover (minimum / normal size) |
| `docs/itch/cover-1260x1000.png` | itch cover, 2x for retina |
| `docs/itch/banner-1920x1080.png` | optional page banner |
| `docs/itch/02-gameplay.png` | screenshot 1, main gameplay |
| `docs/itch/03-level-complete.png` | screenshot 2, win overlay |
| `docs/itch/04-themes.png` | screenshot 3, theme picker (shows the fixed X) |
| `docs/itch/05-level-select.png` | screenshot 4, level select |
| `docs/itch/01-home.png` | screenshot 5, home screen |

All screenshots are 1920×1080. itch recommends 3–5 screenshots. Upload all five and put the gameplay shot first.

Optional trailer: 15–30 seconds, no voice, show one drag, one completed board, one theme switch. Link it as a YouTube or Vimeo URL in the project editor.

## Upload the build

1. Build the WebGL player to `Builds/WebGL`.
2. Check that `Builds/WebGL/index.html` exists.
3. Zip the **contents** of that folder, not the folder itself:

```bash
cd Builds/WebGL
zip -r ../../GridCut-WebGL-v1.0.zip . -x '*.DS_Store'
```

4. The ZIP must have `index.html` at its root. itch.io will reject a ZIP whose entry point is inside a subfolder.
5. Current Unity settings in this project: Gzip compression, decompression fallback off, 32 MB WebGL memory. If the page loads but crashes, raise WebGL memory to 256 MB. If assets fail to load, switch compression to Brotli or enable decompression fallback, then rebuild.
6. itch.io HTML5 limits: at most 1,000 files, 500 MB extracted total, 200 MB per file. This project is far below those limits.
7. Upload the ZIP as an HTML file. Check **This file will be played in the browser**. Leave **This file is a demo** unchecked.

Embed settings:

- Launch mode: **Embed in page** with a 1280×720 viewport, plus the fullscreen button. If the embed feels cramped, switch to **Click to launch in fullscreen**.
- **Click to play**: on.
- **Fullscreen button**: on.
- **Scrollbars**: off.
- **Mobile friendly**: on.

Test the draft page in an incognito window and on a phone before publishing. Confirm the canvas fills the frame, audio starts after the first click, touch drag works, and the theme popup opens over the win overlay.

## Publish checklist

- [ ] Build uploaded and playable from the draft page
- [ ] Cover and 3–5 screenshots uploaded
- [ ] Description pasted and section titles set to Header 2
- [ ] Tags and genre set
- [ ] Platforms limited to browser/HTML5
- [ ] Languages set to English only
- [ ] AI disclosure filled in
- [ ] Visibility still Draft
- [ ] Tested in incognito and on mobile
- [ ] Change visibility to Public
- [ ] Post a short devlog or announcement

Publishing for the first time puts the page on **Most Recent**. itch.io does not let you return to the top of that list, so do not publish until the page and build are ready. Do not recreate the page later for another boost; that violates the quality guidelines.

## Optional first devlog

Title: `GridCut is out`

```text
GridCut is a small browser puzzle about fitting rectangles into a grid.

Every number tells you the area of the piece that covers it. Drag to draw, fill the board, and the level is done. There are 150 levels, five color themes, and no timer.

It is free to play in the browser. If you find a level that feels off, leave a comment with the level number.
```

## Sources

- itch.io, Your first itch.io page: https://itch.io/docs/creators/getting-started
- itch.io, Uploading HTML5 games: https://itch.io/docs/creators/html5
- itch.io, Designing your page: https://itch.io/docs/creators/design
- itch.io, Content creator quality guidelines: https://itch.io/docs/creators/quality-guidelines
- itch.io, Generative AI disclosure: https://itch.io/t/4309690/generative-ai-disclosure-tagging
