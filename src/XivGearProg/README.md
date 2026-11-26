# XIVGear Prog

Dalamud API 15 plugin for https://xivgearprog.top. Command: `/xgp`.

## Install and connect
1. Add `https://raw.githubusercontent.com/CheesyChiz/DalamudPlugins/main/repo.json` under `/xlsettings` → Experimental → Custom Plugin Repositories.
2. Install **XIVGear Prog** from `/xlplugins`.
3. Sign in on the website. Open your account → **Connect game** → **Create token**.
4. Paste the token into `/xgp` and click **Save token**. Automatic sync is enabled immediately.

## What syncs
- Equipped combat gear, materia, and relic stats.
- All saved combat gear sets (not just one per job).
- Gear counts from local inventory and Armoury Chest, with HQ/NQ distinction.

The plugin checks for changes every 5 minutes. Unchanged snapshots are not resent. Failed uploads retry on the same 5-minute interval. **Sync now** sends an immediate update. You can pause automatic sync, disconnect locally, or revoke the token on the website. Retainers, chat, logs, achievements and other players are not sent.

Game sets have stable source identifiers and are marked Game on the website. Existing targets, manually created sets and settings are preserved. Existing imported names are retained when a game set updates. Deleted game sets are retained on the website rather than destructively removed.

Tokens grant only gear-upload access, are stored in the local Dalamud plugin configuration, and are never logged. The server stores token hashes. Creating a replacement token revokes the old one.

## Build
Install .NET 10 SDK and Dalamud API 15, then run `dotnet build -c Release` in this directory. `DALAMUD_HOME` can override the SDK path. The package is generated in `bin/Release/XivGearProg/latest.zip`.

## Validation
Compiled against Dalamud API 15. Server tests cover token authorization, CSRF, revocation, account isolation and nondestructive merging. Live in-game validation is still required for this initial release.

## License and attribution
AGPL-3.0-only. `XivGearSet.cs` is from [XivGearExport](https://github.com/VioletStardustXIV/XivGearExport), commit `1a4a4ebf7f9d10911bf1b7975024fd4672ba5622`, licensed under AGPL-3.0. The upstream conversion preserves materia and Phantom/Manderville/Resistance relic stats. Tomestone's public plugin was reviewed for its token-based connection flow; no Tomestone source was incorporated.

Version 0.1.0.4: uploads character name and home world for the account profile; keeps the saved token masked and reports changed sets, inventory updates, or no changes. Automatic checks run every 5 minutes. Use Sync now for an immediate upload.
