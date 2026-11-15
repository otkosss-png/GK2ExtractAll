# GK2 Extract All

A BepInEx plugin for **Graveyard Keeper 2**: adds **Extract all** / **Select...** buttons to the
autopsy window. One press pulls every organ and every pocket item out of the corpse, one by one,
using the game's own extraction flow, or only the parts you mark.

## Features

- **Extract all** button in the autopsy window (organs + pocket items).
- **Select... (new in 1.2.0)**: turns on the selection mode - a small square appears in the
  top-left corner of every organ/pocket cell (same idea as the pin square in Recipe Pin).
  Click a square to mark/unmark the part: yellow = extract, dark = leave it. The button turns
  into **Extract** and pulls out only the marked parts. Pressing **F** (the game's "move all"
  key) also respects the selection.
- **Controller support (new in 1.2.1)**: both buttons are part of the window's gamepad
  navigation (stick/d-pad down to them, press to activate); in selection mode pressing the
  action button on an organ/pocket cell toggles its mark; RB (also LT) toggles the mark of the
  focused cell.
- Sequential: one extraction per step, with a short configurable pause (no instant mass calls).
  Each extraction is a timed craft at the table; the mod waits for it to finish before the
  next one (1.2.2). Parts are tracked by item, so shifting cells and identical items
  (several pieces of meat) are handled correctly.
- Extractions that the game refuses (missing instrument, unknown organ, ...) are skipped and
  counted; a summary under the buttons shows **Extracted N of M**.
- Selection markers live on their own overlay layer, so the game's own clicks still work
  (clicking a cell extracts that single part).
- Works with mouse and controller; settings in the in-game **Mods** menu.

## Settings (in-game Mods menu)

- Language (auto = game language / en / ru / any file you add), button on/off, delay between
  extractions (100-2000 ms), selection preset.

## Translations (1.2.3)

All texts live in `BepInEx\plugins\GK2ExtractAll\Localization\<lang>.json` (`en.json` and
`ru.json` are written on first launch and never overwritten). To add a language, copy `en.json`
to e.g. `de.json`, translate the values (keep `{0}`/`{1}`), restart the game and pick `de` in the
Language setting. Missing or broken lines fall back to English. Keys starting with `settings.` /
`mod.` translate the Mods settings screen (copied to GK2 Mod Framework's localization folder).

## Requirements

- Graveyard Keeper 2 (Steam), game version 1.011 or newer (it ships BepInEx and loads
  Workshop mods by itself).
- **GK2 Mod Framework is optional (since 1.2.9).** With it, the settings appear in the in-game
  **Mods** menu; without it the mod still works and its settings live in the config file
  `BepInEx\config\*.cfg`.

## Install

Subscribe to the mod on the Steam Workshop - that is all. Or copy the `BepInEx` folder from the
release zip into the game folder by hand.

Steam Deck / Linux: set the launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`.

Do not use the old GK2 Workshop Auto-Loader any more - the game now does its job.

## Build

```
& "<dotnet>" build -c Release
```

References the game's assemblies from `$(GameDir)` (default
`E:\SteamLibrary\steamapps\common\Graveyard Keeper 2`).

## Layout

- `src/GK2ExtractAll.Core` - pure logic (netstandard2.0): extraction queue, selection, texts, unit-tested.
- `src/GK2ExtractAll` - the BepInEx plugin (netstandard2.1).
- `tests/GK2ExtractAll.Core.Tests` - xUnit.
- `docs/superpowers` - design spec and implementation plan.

Not affiliated with the developers or publishers of Graveyard Keeper 2.
