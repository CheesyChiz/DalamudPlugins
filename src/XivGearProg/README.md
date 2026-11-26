# XIVGear Prog

Sync your FFXIV gear with [XIVGear Prog](https://xivgearprog.top). Open the plugin with `/xgp`.

## Install and connect
1. Add `https://raw.githubusercontent.com/CheesyChiz/DalamudPlugins/main/repo.json` under `/xlsettings` → Experimental → Custom Plugin Repositories.
2. Install **XIVGear Prog** from `/xlplugins`.
3. Sign in on the website. Open your account → **Connect game** → **Create token**.
4. Paste the token into `/xgp` and click **Save token**. Automatic sync is enabled immediately.

## What syncs
- Equipped combat gear, materia, and relic stats.
- Selected saved combat gear sets (not just one per job). Use **Choose gear sets to sync** to exclude entire jobs or individual sets. Existing website sets are kept until you delete them.
- Gear counts from local inventory and Armoury Chest, with HQ/NQ distinction.
- Character name and home world, tomestone balances and weekly progress, plus materials for gear exchanges.
- Exchange costs for supported SpecialShop equipment at iLvl 500 and above, read from game data. Unresolved currencies, collectable exchanges, market prices and vendor unlocks are not guessed. Prices are uploaded once per plugin session; subsequent uploads contain balances only.
- Cached retainer inventory gear and exchange materials, with last-checked timestamps.

The plugin checks for changes every 5 minutes. Unchanged snapshots are not resent. Failed uploads retry on the same 5-minute interval. **Sync now** sends an immediate update. You can pause automatic sync, disconnect locally, or revoke the token on the website. Retainer inventory snapshots update only after opening the inventory in game. Snapshots are cached locally and uploaded on the regular interval or with Sync now; the website shows when each was checked. Retainer market listings and equipped retainer gear are not included. Chat, logs, achievements and other players are not sent.

Game sets have stable character + game slot identifiers, shared with Allagan Tools imports to avoid duplicates. Existing targets, manually created sets and settings are preserved. Existing imported names are retained when a game set updates. Deleted game sets are retained on the website rather than destructively removed.

Tokens grant only gear-upload access, are stored in the local Dalamud plugin configuration, and are never logged. The server stores token hashes. Creating a replacement token revokes the old one.

## License
[AGPL-3.0-only](LICENSE).

`XivGearSet.cs` is from [XivGearExport](https://github.com/VioletStardustXIV/XivGearExport), licensed under AGPL-3.0.
