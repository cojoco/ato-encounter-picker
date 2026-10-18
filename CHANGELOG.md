# Changelog

## 0.4.0

### Added
- **Saved sets.** Custom enemy sets can be saved from the Custom enemies tab (**Save as** name box and **Save this set**) and loaded from the new **Saved sets** tab, which shows portraits and rewards and has a **Delete** button. Sets are stored in `BepInEx/config/encounterpicker-presets.json`, so you can prepare them before a run and reuse them in any run.

### Changed
- The filter box now sits directly above the list it filters and is labelled with what it filters.
- The panel is taller, pinned to the top of the screen, and never taller than the screen.
- The window title reads "Encounter Picker" instead of "Dungeon master".
- The config section `[DungeonMaster]` is now `[EncounterPicker]`. Existing `Picker` and `NoCorruption` settings reset to their defaults, so set them again if you changed them.
- History log lines say `Picker:` instead of `DM picker:`.

## 0.3.0

- The picker is on by default.
- The picker panel was restyled, with hide and clear-all buttons and enemy portraits.
- The combat search also matches enemies' internal ids.
- Custom enemies show in the corruption window.
