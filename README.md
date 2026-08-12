# NutStaller

A launcher and installer for [reNut](https://github.com/masterspike52/reNut), the PC recompilation of
Banjo-Kazooie: Nuts & Bolts.

![NutStaller](NutStaller/Assets/bg.png)

## What it does

- Downloads the latest reNut release and the latest extract-xiso build
- Extracts your Banjo-Kazooie: Nuts & Bolts (US) ISO with live progress
- Writes `renut.cfg` so the in-game path wizard never shows up
- Edits keybinds in `renut.toml`
- Checks for reNut updates
- Works with mouse or an XInput gamepad (dpad moves, A selects, LB/RB switch page, Start plays)

## Usage

1. Run `NutStaller.exe`
2. Pick an install folder
3. Hit "Do everything" and select your ISO when asked
4. Hit "Play"

Drop an ISO into the install folder beforehand and step 3 picks it up automatically.

## Building

```
dotnet publish -p:PublishProfile=win-x64
```

Produces a single self-contained `bin\publish\NutStaller.exe`. Requires the .NET 8 SDK.

## Credits

reNut by masterspike52 and contributors. extract-xiso by the XboxDev team.
Banjo-Kazooie: Nuts & Bolts by Rare. See the Credits page in the app for the full list.
