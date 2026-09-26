# GK2 Extract All

A BepInEx plugin for **Graveyard Keeper 2**: adds an **Extract all** button to the autopsy
window. One press pulls every organ and every pocket item out of the corpse, one by one,
using the game's own extraction flow.

## Features

- **Extract all** button in the autopsy window (organs + pocket items).
- Sequential: one extraction per step, with a short configurable pause (no instant mass calls).
- Extractions that the game refuses (missing instrument, unknown organ, ...) are skipped and
  counted; a summary under the button shows **Extracted N of M**.
- Works with mouse and controller; settings in the in-game **Mods** menu.

## Settings (in-game Mods menu)

- Language (en / ru / auto), button on/off, delay between extractions (100–2000 ms).

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

- `src/GK2ExtractAll.Core` — pure logic (netstandard2.0): extraction queue + texts, unit-tested.
- `src/GK2ExtractAll` — the BepInEx plugin (netstandard2.1).
- `tests/GK2ExtractAll.Core.Tests` — xUnit.
- `docs/superpowers` — design spec and implementation plan.

Not affiliated with the developers or publishers of Graveyard Keeper 2.
