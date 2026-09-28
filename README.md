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

- Language (en / ru / auto), button on/off, delay between extractions (100-2000 ms).

## Requirements

- Graveyard Keeper 2 (Steam).
- BepInEx 5.4.23.5 x64 and **GK2 Mod Framework** (both installed by
  [GK2 Mod Installer](https://github.com/otkosss-png/GK2ModInstaller)).

## Install

Copy `BepInEx/plugins/GK2ExtractAll/GK2ExtractAll.dll` into
`<game>\BepInEx\plugins\GK2ExtractAll\`, or install the Workshop item via the
[GK2 Workshop Auto-Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3807406994).

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
