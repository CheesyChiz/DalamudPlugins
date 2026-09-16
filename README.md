<h1 align="center">CheesyChiz · Dalamud Plugins</h1>

One custom repository URL for my FFXIV Dalamud plugins. Install only the plugins you want;
they are independent and have their own requirements, source code and releases.

## Install

1. Open `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Add this URL and save:

   ```text
   https://raw.githubusercontent.com/CheesyChiz/DalamudPlugins/main/repo.json
   ```

3. Open `/xlplugins`, refresh the list and search for **Stock Manager** or **Positional Cue**.

If you already added the old StockManager or PositionalCue repository URL, replace it
with this one. You do not need to uninstall the plugins or delete their settings.
The old catalog URLs remain available for compatibility; use only the common URL to
avoid duplicate entries. Future plugins will be added here.

## Plugins

| | Plugin | What it does | Open |
| --- | --- | --- | --- |
| <img src="https://raw.githubusercontent.com/CheesyChiz/StockManager/main/assets/icon.png" width="64" alt="Stock Manager"> | [Stock Manager](https://github.com/CheesyChiz/StockManager) | Island Sanctuary inventory targets and imported Visland route management. Requires Visland and vnavmesh; see the plugin README. | `/sm` |
| <img src="https://raw.githubusercontent.com/CheesyChiz/PositionalCue/main/assets/icon.png" width="64" alt="Positional Cue"> | [Positional Cue](https://github.com/CheesyChiz/PositionalCue) · **test release** | Upcoming rear/flank HUD, approximate GCD countdown, correct-sector indicator and optional gentle sound. Requires Wrath Combo positional IPC. Does not move the character. | `/pcue` |

Both entries currently target **Dalamud API 15**. Positional Cue has been compiled and
logic-tested; its in-game validation is still pending.

### Positional Cue commands

- `/pcue` or `/positionalcue` — settings.
- `/pcue test` — toggle HUD preview.
- `/pcue on`, `/pcue off`, `/pcue toggle` — enable/disable.
- `/pcue sound` — test the configured chime.
- `/pcue help` — command list.

## Repository layout

This is the **installation catalog**, not a combined source-code repository.
`repo.json` points to versioned ZIP releases in each plugin's own repository.
Bug reports and source changes belong in the corresponding plugin repository.

For a new release, update only that plugin's entry: assembly version, API level,
download URLs, changelog and timestamp. Preserve all other entries. Publish and
verify the release ZIP before updating the catalog. Keep compatibility catalogs
in the plugin repositories in sync when their plugin is released.
