using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;

namespace MapDumper
{
    // One node's overrides, as written in mapdumper-spec.json or set by the DM picker.
    internal class NodeSpec
    {
        [JsonProperty("combat")] public string Combat { get; set; }
        [JsonProperty("event")] public string Event { get; set; }
        [JsonProperty("enemies")] public string[] Enemies { get; set; }
        // false skips the corruption offer shown before this node's combat.
        [JsonProperty("corruption")] public bool? Corruption { get; set; }
    }

    // The per-node override table: loaded from mapdumper-spec.json, and edited in memory by the DM picker.
    internal static class SpecFile
    {
        internal static Dictionary<string, NodeSpec> Specs = new Dictionary<string, NodeSpec>();

        private static string path;

        private const string ExampleSpec = "{\n  \"sen_10\": { \"combat\": \"csen_2\" },\n  \"sen_8\": { \"corruption\": false },\n  \"ol4_3\": { \"combat\": \"challenge_1\", \"enemies\": [\"sheep\", \"sheep\", \"sheep\", \"\"] }\n}\n";

        internal static void Init()
        {
            path = Path.Combine(Paths.ConfigPath, "mapdumper-spec.json");
        }

        internal static NodeSpec Get(string nodeId)
        {
            return nodeId != null && Specs.TryGetValue(nodeId, out NodeSpec spec) ? spec : null;
        }

        // The enemies this node's entry forces, or null to leave the game's own choice.
        internal static string[] EnemiesFor(string nodeId)
        {
            return Get(nodeId)?.Enemies;
        }

        // The node's entry, created if it has none yet.
        internal static NodeSpec GetOrCreate(string nodeId)
        {
            if (!Specs.TryGetValue(nodeId, out NodeSpec spec))
            {
                spec = new NodeSpec();
                Specs[nodeId] = spec;
            }
            return spec;
        }

        // Drops an entry that no longer overrides anything.
        internal static void Prune(string nodeId)
        {
            if (Specs.TryGetValue(nodeId, out NodeSpec spec) && string.IsNullOrEmpty(spec.Combat) && string.IsNullOrEmpty(spec.Event)
                && spec.Enemies == null && spec.Corruption == null)
            {
                Specs.Remove(nodeId);
            }
        }

        // Writes one node's combat/event into the game's node assignments. Returns true if the assignment changed.
        internal static bool ApplyToNode(string nodeId)
        {
            NodeSpec spec = Get(nodeId);
            if (spec == null)
            {
                return false;
            }
            var assigned = AtOManager.Instance.gameNodeAssigned;
            if (!string.IsNullOrEmpty(spec.Combat))
            {
                assigned[nodeId] = "combat:" + spec.Combat;
                return true;
            }
            if (!string.IsNullOrEmpty(spec.Event))
            {
                assigned[nodeId] = "event:" + spec.Event;
                return true;
            }
            return false;
        }

        // Reads the spec file, checks every id against the game's data, applies it to the current map and redraws the nodes.
        internal static void Load()
        {
            var manager = AtOManager.Instance;
            if (manager == null || manager.gameNodeAssigned == null || manager.gameNodeAssigned.Count == 0)
            {
                Plugin.Record("No assigned map nodes yet (not in a run?).");
                return;
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, ExampleSpec);
                Plugin.Record($"No spec file found, wrote an example to {path}. Edit it and press the key again.");
                return;
            }

            Dictionary<string, NodeSpec> loaded;
            try
            {
                loaded = JsonConvert.DeserializeObject<Dictionary<string, NodeSpec>>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Plugin.Record($"Could not read {path}: {e.Message}");
                return;
            }
            Specs = loaded ?? new Dictionary<string, NodeSpec>();

            int applied = 0;
            foreach (var entry in Specs)
            {
                if (!manager.gameNodeAssigned.ContainsKey(entry.Key) && Globals.Instance.GetNodeData(entry.Key) == null)
                {
                    Plugin.Record($"Spec: unknown node '{entry.Key}', skipped.");
                    continue;
                }
                NodeSpec spec = entry.Value;
                if (!string.IsNullOrEmpty(spec.Combat) && Globals.Instance.GetCombatData(spec.Combat) == null)
                {
                    Plugin.Record($"Spec: node '{entry.Key}' has unknown combat '{spec.Combat}', skipped.");
                    continue;
                }
                if (!string.IsNullOrEmpty(spec.Event) && Globals.Instance.GetEventData(spec.Event) == null)
                {
                    Plugin.Record($"Spec: node '{entry.Key}' has unknown event '{spec.Event}', skipped.");
                    continue;
                }
                if (spec.Enemies != null)
                {
                    foreach (string npc in spec.Enemies.Where(n => !string.IsNullOrEmpty(n)))
                    {
                        if (Globals.Instance.GetNPC(npc) == null)
                        {
                            Plugin.Record($"Spec: node '{entry.Key}' has unknown enemy '{npc}'.");
                        }
                    }
                }
                if (ApplyToNode(entry.Key))
                {
                    applied++;
                }
                string now = manager.gameNodeAssigned.TryGetValue(entry.Key, out string v) ? v : "?";
                Plugin.Record($"Spec: node '{entry.Key}' -> {now}{(spec.Enemies != null ? $", enemies [{string.Join(", ", spec.Enemies)}]" : "")}{(spec.Corruption == false ? ", no corruption" : "")}");
            }

            MapTools.RedrawNodes();
            Plugin.Record($"Spec loaded: {Specs.Count} nodes, {applied} assignments changed.");
        }
    }
}
