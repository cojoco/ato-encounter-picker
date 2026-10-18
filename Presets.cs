using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace EncounterPicker
{
    // A saved custom enemy set: the four enemy slots and the combat they run under.
    internal class Preset
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("combat")] public string Combat { get; set; }
        [JsonProperty("enemies")] public string[] Enemies { get; set; }
    }

    // Custom enemy sets saved in encounterpicker-presets.json, so they can be prepared before a run and reused in any run.
    internal static class PresetFile
    {
        internal static List<Preset> Presets = new List<Preset>();

        private static string path;

        internal static void Init()
        {
            path = Path.Combine(Paths.ConfigPath, "encounterpicker-presets.json");
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                Presets = JsonConvert.DeserializeObject<List<Preset>>(File.ReadAllText(path)) ?? new List<Preset>();
            }
            catch (Exception e)
            {
                Plugin.Record($"Could not read {path}: {e.Message}");
            }
        }

        // Saves a set under a name, replacing any set with the same name.
        internal static void Save(string name, string combat, string[] enemies)
        {
            Presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            Presets.Add(new Preset { Name = name, Combat = combat, Enemies = (string[])enemies.Clone() });
            Write();
        }

        internal static void Delete(Preset preset)
        {
            Presets.Remove(preset);
            Write();
        }

        private static void Write()
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(Presets, Formatting.Indented));
            }
            catch (Exception e)
            {
                Plugin.Record($"Could not write {path}: {e.Message}");
            }
        }
    }
}
