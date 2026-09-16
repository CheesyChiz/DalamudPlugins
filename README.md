# CheesyChiz · Dalamud Plugins

Custom installation catalog for my FFXIV Dalamud plugins.

## Installation

Add this URL in `/xlsettings` → **Experimental** → **Custom Plugin Repositories**:

```text
https://raw.githubusercontent.com/CheesyChiz/DalamudPlugins/main/repo.json
```

Install individual plugins through `/xlplugins`. This URL replaces the separate
StockManager and PositionalCue catalog URLs; existing settings are retained.

## Plugins

| Plugin | Function | Required plugins | Command |
| --- | --- | --- | --- |
| [Stock Manager](https://github.com/CheesyChiz/StockManager) | Maintains Island Sanctuary stock targets using imported gathering routes. | Visland, vnavmesh | `/sm` |
| [Positional Cue](https://github.com/CheesyChiz/PositionalCue) | Displays upcoming rear/flank attacks from Wrath Combo with timing and optional sound cues. | Wrath Combo | `/pcue` |

Source code and releases are maintained in each plugin's repository.
