# Encounter Picker

A BepInEx mod for **Across the Obelisk** that lets the host of a run (or a solo player) act as a dungeon master: when the party travels to a node, the host chooses what that node becomes.

**Status:** tested solo and in one co-op session on the campaign map and the Obelisk Challenge map. It has not been tried with more than one co-op session, on every zone, or on game versions other than the one it was written against. Back up your save before using it on a run you care about.

## What it does

- **DM picker.** Click a combat, event or town node and the travel is held while a panel asks what the node should be:
  - any **combat** or **event** in the game, with readable names (enemies for combats, event names for events),
  - a **town** (and a town can be turned into an encounter),
  - **custom enemies**: choose up to four enemies yourself, under a base combat of your choice,
  - **no corruption offer** for that node (a toggle button),
  - or **Keep as is**.
  Visited nodes can be revisited: picking something re-runs the node. **Just go there** travels without running anything.
- **Portraits and reward preview.** Combat rows show up to four enemy portraits, enemy rows show one, and filled custom-enemy slots show their enemy. Combats and enemies in the lists also show their base loot tier and per-hero XP and gold. Combat performance bonuses are not included, so real rewards can be higher (about +50% was seen).
- **Spec file.** A JSON file that sets nodes ahead of time (see below).
- **KeepAllEnemies.** The game removes one enemy from many campaign combats at the base difficulty in acts 1-2. This setting keeps them.
- **Map dump.** F10 writes every node, its zone, name, action and enemies to the history log.

## The picker panel

- **Header:** the node's name and id (and "visited before" on a revisit), the node's current assignment, and a **Hide** button.
- **Hide / Show.** Hide collapses the panel to a small "Show encounter picker" bar at the top of the screen so you can look over the map first. Travel stays held until you choose. The panel is shown again for the next node.
- **Tabs:** **Combats**, **Events** and **Custom enemies**, with a **Filter** box. The filter matches the displayed names, the ids, and the enemies' internal ids (so `hydra` finds every hydra fight even though they are shown as "Hatred, Arrogance, ...").
- **Town and corruption:** **Turn this node into a town**, and a **No corruption offer** toggle (ON when highlighted).
- **List:** large rows with portraits and a reward line. Click a row to pick it. Lists show the first 150 matches, so type in the filter to narrow them.
- **Custom enemies tab:** four slot buttons (the selected slot is highlighted, with the enemy's portrait). Click an enemy in the list to fill the selected slot; the selection then moves to the next slot. **Clear selected slot** and **Clear all** empty slots, **Change** picks the base combat, and **Run with these enemies** starts the fight.
- **Footer:** **Keep as is** (or **Just go there** on a visited node).

## Requirements and install

1. Across the Obelisk with **BepInEx 5.4.x** installed (Windows: the `win_x64` build; Linux: the native BepInEx 5 setup). The game used Unity 2022.3 and Mono when this was written.
2. Copy `EncounterPicker.dll` into the game's `BepInEx/plugins/` folder.
3. Start the game once. The settings file `BepInEx/config/com.cojoco.encounterpicker.cfg` is created.
4. Set `Picker = true` under `[DungeonMaster]` and restart. The picker is off by default so the plugin does nothing unless asked.

BepInEx does not hot-reload plugins: restart the game after changing the DLL.

## Co-op

- The **host** needs the mod and runs the picker. Other players only receive the combat or event id, and look it up in their own game data, so **other players do not need the mod** as long as the DM picks vanilla combats and events. This is based on reading the game's network code and one real session, not on broad testing.
- Custom enemies are sent to other players with the rest of the team data, so they should appear for everyone.
- Other players' map previews can still show a node's original contents before the DM has chosen.
- Not supported: a separate fifth player acting as DM without a hero. The game's room and ready-check code assumes every connection owns a hero.

## Hotkeys

| Key | Action |
|---|---|
| F10 | Dump the map to the history log |
| F8 | Load `encounterpicker-spec.json` and apply it to the current map |

Change them under `[Hotkeys]` in the config file. F12 is the Steam screenshot key, so avoid it.

## Settings (`com.cojoco.encounterpicker.cfg`)

| Section / key | Default | Meaning |
|---|---|---|
| `[DungeonMaster] Picker` | `false` | Hold travel to combat, event and town nodes and show the DM panel (host or solo only) |
| `[DungeonMaster] NoCorruption` | `false` | Never show the corruption offer before combats |
| `[Combat] KeepAllEnemies` | `false` | Skip the easy-mode enemy trimming |
| `[Logging] Verbose` | `false` | Also log every node hover and click, and corruption checks |
| `[Hotkeys] DumpMap`, `LoadSpec` | F10, F8 | Hotkeys |

## Spec file

`BepInEx/config/encounterpicker-spec.json` maps node ids to overrides. Press F8 on the map to apply it. If the file does not exist, F8 writes an example. Find node ids with F10 (or with the picker's title, which shows the id).

```json
{
  "sen_10": { "combat": "csen_2" },
  "sen_8":  { "corruption": false },
  "ol4_3":  { "combat": "challenge_1", "enemies": ["sheep", "sheep", "sheep", ""] }
}
```

| Field | Meaning |
|---|---|
| `combat` | Make the node run this combat id |
| `event` | Make the node run this event id |
| `enemies` | Force exactly these four enemies (empty string for an empty slot). Needed on the Obelisk Challenge map, where the game re-rolls enemies |
| `corruption` | `false` skips the corruption offer for this node |

Unknown ids are reported in the history log and skipped. Choices made in the picker are kept in memory only and are lost when the game closes.

## Good to know

- **Rewards come from the enemies, not the combat.** XP, gold and loot tier are summed from the enemies' own data. The base combat of a custom set only affects the end-of-combat event, healing and special effects.
- **Enemy variants.** The game swaps some enemies for upgraded versions depending on your town tier, NG+ and difficulty. The fight and the rewards use the variant.
- **Resuming a save mid-fight** rebuilds the enemies from the saved fight, so overrides made after saving do not change it. Test overrides on a fight started fresh from the map.
- **Campaign popups** hide enemies of combats you have not unlocked ("?????"). That is the game's own behaviour.
- **Towns:** turning a node into a town, or a town into something else, works in single player. Other effects, such as a zone left without a normal town, have not been checked.
- **Corruption:** every ordinary combat offers a corruption, except the tutorial and the first Senenthia nodes, nodes the game flags, and combats that launch an event afterwards. Forcing a specific corruption is not implemented.

## Logs

Everything the plugin does is appended with a timestamp to `BepInEx/config/encounterpicker-history.log`, which survives restarts (BepInEx's own `LogOutput.log` is overwritten each launch). Useful lines:

- `DM picker: node ...` the DM's choice
- `SetCombatData: ...` and `Combat scene spawned enemies: ...` what the game chose and what actually spawned
- `Rewards shown: ...` the real payout per hero
- `Corruption offered at node ...`

## Building

Needs the .NET SDK. The project targets `netstandard2.1`, matching the game's assemblies.

```
dotnet build -c Release
```

It references the game's own DLLs (`Assembly-CSharp`, Unity modules, Photon, `Newtonsoft.Json`) from `AcrossTheObelisk_Data/Managed` and BepInEx and Harmony from `BepInEx/core`, and copies the result into `BepInEx/plugins/`. The game folder defaults to the Linux Steam path; override it with `-p:GameDir="C:\path\to\Across the Obelisk"`. No game code is included in this project.
