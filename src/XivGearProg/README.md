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
- Cached retainer inventory gear and exchange materials, with last-checked timestamps.

The plugin checks for changes every 5 minutes. Unchanged snapshots are not resent. Failed uploads retry on the same 5-minute interval. **Sync now** sends an immediate update. You can pause automatic sync, disconnect locally, or revoke the token on the website. Retainer inventory snapshots update only after opening the inventory in game. Snapshots are cached locally and uploaded on the regular interval or with Sync now; the website shows when each was checked. Retainer market listings and equipped retainer gear are not included. Chat, logs, achievements and other players are not sent.

Game sets have stable character + game slot identifiers, shared with Allagan Tools imports to avoid duplicates. Existing targets, manually created sets and settings are preserved. Existing imported names are retained when a game set updates. Deleted game sets are retained on the website rather than destructively removed.

Tokens grant gear-upload access and access to plans explicitly sent to the plugin, are stored in the local Dalamud plugin configuration, and are never logged. The server stores token hashes. Creating a replacement token revokes the old one.

Prices and exchange requirements come from Garland Tools and are cached on the website server. Viewing prices does not require the game or plugin. The plugin supplies your inventory and currency balances.

To add another character, log in to that character and sync using the same token. Choose the character by name and home world in the website sidebar.

## License
[AGPL-3.0-only](LICENSE.md).

`XivGearSet.cs` is from [XivGearExport](https://github.com/VioletStardustXIV/XivGearExport), licensed under AGPL-3.0.

Job levels are synced for every unlocked combat job, including jobs excluded from gear-set uploads. The website uses the selected character’s job level for gear eligibility and calculation. Level gains are included in the next automatic check (every 5 minutes), or immediately with **Sync now**.

## Import a website build into the game
On the set page or in My sets, use **To game**. The website queues the plan for its character and shows when the plugin receives it. In `/xgp`, open **Gear plans**. Sets arrive immediately through the connected account, independently of the five-minute gear upload. If the game is closed, queued sets arrive on the next connection. **Fetch sets** is a manual retry. Select the received plan, review it, then use **Create NEW game set**. Switch to the build's job first. This creates a new slot without replacing existing sets or equipping it. Missing items are not obtained. Materia, relic stats, food and level-sync settings are not applied to your actual items. Check the game set before using it.

## Gear plans and missing items
Received plans are saved locally per character and remain available after creating a game set. Each item shows its icon, item level, quality and whether it is equipped/in your inventory or Armoury Chest, in a cached retainer inventory, or not found. Quantities are allocated per slot so one ring cannot satisfy two requirements. Owned status checks the item and quality; target materia and relic stats are shown separately, not assumed to match. Use Only missing / on retainers to focus the list.

Retainer locations and timestamps are snapshots, refreshed only after opening those inventories. Unchecked retainers may contain more items. Local inventory checks run every 30 seconds while Gear plans is open, or using Refresh inventory. This does not change the five-minute website upload interval.

Where to get it opens the exact item in Garland Tools. Exchange costs loads public prices from the website server on demand (including base-item exchanges). No token is sent with these public requests. Simulations continue to run in the website browser engine; Open website / simulate opens the website, it does not run a simulation inside Dalamud.

## Preview and compare
Select a plan from the left-hand list to see weapon/armour and accessory slots in two columns. Hover an icon for item details. In **Compare with equipped / saved game set**, choose equipped gear or a numbered game set. Both columns show items, HQ/NQ quality, materia and relic stats. **Refresh comparison** rereads the selected gear. Comparing never equips or overwrites a game set. DPS and level-sync calculations remain on the website.
