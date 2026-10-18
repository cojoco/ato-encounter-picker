using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace EncounterPicker
{
    // Host-side dungeon master panel: when the party agrees on a combat, event or town node, travel is held until the DM picks what it becomes.
    internal static class Picker
    {
        private class Entry
        {
            public string Id;
            public string Label;
            public string Search;
            public NPCData[] Npcs;
        }

        internal static Node Pending;
        internal static bool Bypass;

        private static bool decided;
        private static string chosen;

        private static string filter = "";
        private static Vector2 scroll;
        private static int tab; // 0 combats, 1 events, 2 custom enemies

        private static List<Entry> combats;
        private static List<Entry> events;
        private static List<Entry> npcs;

        // Custom enemies editor: four slots, the combat they run under, and the node they were prefilled for.
        private static readonly string[] slots = new string[4];
        private static int slotSel;
        private static string slotsNode;
        private static string baseCombat = "";
        private static string[] customEnemies;
        private static string message = "";
        private static bool choosingBase;
        private static bool skipCorruption;

        private const int MaxRows = 150;

        private static void BuildLists()
        {
            if (combats != null)
            {
                return;
            }

            // The combat registry has no public accessor.
            var source = Traverse.Create(Globals.Instance).Field("_CombatDataSource").GetValue<Dictionary<string, CombatData>>();
            combats = source.OrderBy(k => k.Key).Select(k =>
            {
                string npcs = k.Value.NPCList == null ? "" : string.Join(", ", k.Value.NPCList.Where(n => n != null).Select(n => NpcName(n)));
                // Campaign combats are named after their node: csen_8 belongs to sen_8 ('Eastern Farmland').
                string place = k.Key.StartsWith("c") ? Globals.Instance.GetNodeData(k.Key.Substring(1))?.NodeName : null;
                string label = $"{(string.IsNullOrEmpty(place) ? "" : place + ": ")}{npcs}   ({k.Key})";
                return new Entry { Id = k.Key, Label = label, Search = label.ToLowerInvariant(), Npcs = k.Value.NPCList };
            }).ToList();

            var npcSource = Traverse.Create(Globals.Instance).Field("_NPCsSource").GetValue<Dictionary<string, NPCData>>();
            npcs = npcSource.Values.Select(n =>
            {
                string label = $"{NpcName(n)}   ({n.Id})";
                return new Entry { Id = n.Id, Label = label, Search = label.ToLowerInvariant(), Npcs = new[] { n } };
            }).OrderBy(e => e.Label).ToList();

            events = Globals.Instance.Events.OrderBy(k => k.Key).Select(k =>
            {
                string name = Texts.Instance.GetText(k.Key + "_nm", "events");
                if (string.IsNullOrEmpty(name))
                {
                    name = k.Value.EventName;
                }
                string label = string.IsNullOrEmpty(name) ? k.Key : $"{name}   ({k.Key})";
                return new Entry { Id = k.Key, Label = label, Search = label.ToLowerInvariant() };
            }).ToList();
        }

        // The game pays out from the enemies themselves (NPCData reward fields, summed in AtOManager.GetTeamNPCReward / GetExperienceFromCombat /
        // GetGoldFromCombat), not from the combat. Experience and gold are split over the four heroes with integer division (RewardsManager.cs:588),
        // so this shows the per-hero amount. It mirrors the game's enemy variant swaps but not thermometer, scarab, poverty or prosperity bonuses.
        private static string RewardSummary(IEnumerable<NPCData> enemies)
        {
            var atO = AtOManager.Instance;
            bool despair = MadnessManager.Instance.IsMadnessTraitActive("despair") || atO.IsChallengeTraitActive("despair");
            int tier = 0;
            int xp = 0;
            int gold = 0;
            foreach (NPCData npc in enemies.Where(n => n != null))
            {
                NPCData n = npc;
                if (atO.ShouldUseUpgradedMob(n))
                {
                    n = n.UpgradedMob;
                }
                if (n != null && atO.GetNgPlus() > 0 && n.NgPlusMob != null)
                {
                    n = n.NgPlusMob;
                }
                if (despair && n != null && n.HellModeMob != null)
                {
                    n = n.HellModeMob;
                }
                if (n == null)
                {
                    continue;
                }
                tier = System.Math.Max(tier, n.TierReward != null ? n.TierReward.TierNum : 0);
                xp += n.ExperienceReward;
                gold += n.GoldReward;
            }
            return $"base loot tier {tier} / {xp / 4} xp / {gold / 4} gold each";
        }

        // Readable form of a "combat:<id>" / "event:<id>" assignment, using the same labels as the lists.
        private static string Friendly(string assignment)
        {
            if (string.IsNullOrEmpty(assignment))
            {
                return "nothing";
            }
            if (assignment == "town:town")
            {
                return "town";
            }
            string[] parts = assignment.Split(new[] { ':' }, 2);
            if (parts.Length == 2)
            {
                if (parts[0] == "combat")
                {
                    return "combat - " + (combats.FirstOrDefault(e => e.Id == parts[1])?.Label ?? parts[1]);
                }
                if (parts[0] == "event")
                {
                    return "event - " + (events.FirstOrDefault(e => e.Id == parts[1])?.Label ?? parts[1]);
                }
            }
            return assignment;
        }

        // Same lookup the combat screen uses for monster names, falling back to the data name and then the id.
        private static string NpcName(NPCData npc)
        {
            string name = Texts.Instance.GetText(npc.Id + "_name", "monsters");
            if (string.IsNullOrEmpty(name))
            {
                name = npc.NPCName;
            }
            return string.IsNullOrEmpty(name) ? npc.Id : name;
        }

        // The combat a custom enemy set runs under (it sets the end-of-combat event, healing and effects): the node's own combat, else one from the same zone.
        private static string FallbackBase(string nodeId)
        {
            Entry exact = combats.FirstOrDefault(e => e.Id == "c" + nodeId);
            if (exact != null)
            {
                return exact.Id;
            }
            string zone = nodeId.Split('_')[0];
            Entry inZone = combats.FirstOrDefault(e => e.Id.StartsWith("c" + zone + "_"));
            return (inZone ?? combats[0]).Id;
        }

        private static void PrefillSlots(string nodeId, string current)
        {
            slotsNode = nodeId;
            slotSel = 0;
            message = "";
            choosingBase = false;
            skipCorruption = SpecFile.Get(nodeId)?.Corruption == false;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = "";
            }

            string[] custom = SpecFile.EnemiesFor(nodeId);
            if (custom != null)
            {
                for (int i = 0; i < slots.Length && i < custom.Length; i++)
                {
                    slots[i] = custom[i] ?? "";
                }
            }

            CombatData combat = null;
            if (current != null && current.StartsWith("combat:"))
            {
                baseCombat = current.Substring("combat:".Length);
                combat = Globals.Instance.GetCombatData(baseCombat);
            }
            else
            {
                baseCombat = FallbackBase(nodeId);
            }

            bool anyCustom = slots.Any(x => x != "");
            if (!anyCustom && combat != null && combat.NPCList != null)
            {
                for (int i = 0; i < slots.Length && i < combat.NPCList.Length; i++)
                {
                    slots[i] = combat.NPCList[i] != null ? combat.NPCList[i].Id : "";
                }
            }
        }

        private static string SlotLabel(int i)
        {
            if (string.IsNullOrEmpty(slots[i]))
            {
                return $"{i + 1}: (empty)";
            }
            NPCData npc = Globals.Instance.GetNPC(slots[i]);
            return $"{i + 1}: {(npc != null ? NpcName(npc) : slots[i])}";
        }

        internal static void Draw()
        {
            if (Pending == null || decided)
            {
                return;
            }
            BuildLists();

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.depth = -1000;

            const float w = 900f;
            const float h = 820f;
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            var area = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);

            // Draw the box a few times so the map behind is dimmed enough to read the text.
            for (int i = 0; i < 3; i++)
            {
                GUI.Box(area, GUIContent.none);
            }

            GUILayout.BeginArea(new Rect(area.x + 16f, area.y + 16f, w - 32f, h - 32f));

            string nodeId = Pending.nodeData.NodeId;
            AtOManager.Instance.gameNodeAssigned.TryGetValue(nodeId, out string current);
            bool visited = AtOManager.Instance.mapVisitedNodes.Contains(nodeId);
            GUILayout.Label($"<b>Dungeon master: {Pending.nodeData.NodeName} ({nodeId}){(visited ? " - visited before" : "")}</b>");
            GUILayout.Label($"Currently: {Friendly(current)}");

            if (slotsNode != nodeId)
            {
                PrefillSlots(nodeId, current);
            }
            string[] existing = SpecFile.EnemiesFor(nodeId);
            if (existing != null)
            {
                GUILayout.Label($"Custom enemies set for this node: [{string.Join(", ", existing)}]");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(tab == 0, "Combats", GUI.skin.button))
            {
                tab = 0;
            }
            if (GUILayout.Toggle(tab == 1, "Events", GUI.skin.button))
            {
                tab = 1;
            }
            if (GUILayout.Toggle(tab == 2, "Custom enemies", GUI.skin.button))
            {
                tab = 2;
            }
            GUILayout.Label("Filter:", GUILayout.Width(50f));
            filter = GUILayout.TextField(filter, GUILayout.Width(300f));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Turn this node into a town"))
            {
                decided = true;
                chosen = "town:town";
            }

            skipCorruption = GUILayout.Toggle(skipCorruption, "No corruption offer for this node");

            float listHeight = h - 250f;
            if (tab == 2)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Runs under: {combats.FirstOrDefault(e => e.Id == baseCombat.ToLower())?.Label ?? baseCombat}", GUILayout.ExpandWidth(true));
                if (GUILayout.Button(choosingBase ? "Back to enemies" : "Change", GUILayout.Width(150f)))
                {
                    choosingBase = !choosingBase;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("The combat sets the end-of-combat event, healing and special effects. Rewards come from the enemies below.");

                GUILayout.BeginHorizontal();
                for (int i = 0; i < slots.Length; i++)
                {
                    if (GUILayout.Toggle(slotSel == i, SlotLabel(i), GUI.skin.button))
                    {
                        slotSel = i;
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear selected slot"))
                {
                    slots[slotSel] = "";
                }
                GUI.enabled = slots.Any(x => x != "");
                if (GUILayout.Button("Run with these enemies"))
                {
                    if (Globals.Instance.GetCombatData(baseCombat) == null)
                    {
                        message = $"Unknown combat '{baseCombat}'.";
                    }
                    else
                    {
                        decided = true;
                        chosen = "combat:" + baseCombat.ToLower();
                        customEnemies = (string[])slots.Clone();
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.Label($"Rewards from these enemies: {RewardSummary(slots.Select(id => id == "" ? null : Globals.Instance.GetNPC(id)))} (before any combat performance bonus)");
                if (message != "")
                {
                    GUILayout.Label(message);
                }
                listHeight -= 160f;
            }

            string needle = filter.Trim().ToLowerInvariant();
            var rows = (tab == 1 ? events : tab == 2 && !choosingBase ? npcs : combats).Where(e => needle == "" || e.Search.Contains(needle)).ToList();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(listHeight));
            foreach (Entry entry in rows.Take(MaxRows))
            {
                string reward = entry.Npcs != null ? $"   [{RewardSummary(entry.Npcs)}]" : "";
                if (GUILayout.Button(entry.Label + reward, GUILayout.ExpandWidth(true)))
                {
                    if (tab == 2 && choosingBase)
                    {
                        baseCombat = entry.Id;
                        choosingBase = false;
                        message = "";
                    }
                    else if (tab == 2)
                    {
                        slots[slotSel] = entry.Id;
                        slotSel = (slotSel + 1) % slots.Length;
                    }
                    else
                    {
                        decided = true;
                        chosen = (tab == 1 ? "event:" : "combat:") + entry.Id;
                    }
                }
            }
            if (rows.Count > MaxRows)
            {
                GUILayout.Label($"...and {rows.Count - MaxRows} more. Type in the filter to narrow the list.");
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button(visited ? "Just go there (no encounter)" : "Keep as is"))
            {
                decided = true;
                chosen = null;
            }

            GUILayout.EndArea();
        }

        // Custom enemies are set by the Custom tab; any other pick clears the node's custom enemies.
        private static void SetNodeEnemies(string nodeId, string[] enemies)
        {
            if (enemies != null)
            {
                SpecFile.GetOrCreate(nodeId).Enemies = enemies;
            }
            else if (SpecFile.Get(nodeId) != null)
            {
                SpecFile.Get(nodeId).Enemies = null;
                SpecFile.Prune(nodeId);
            }
        }

        private static void SetNodeCorruption(string nodeId, bool skip)
        {
            if (skip)
            {
                SpecFile.GetOrCreate(nodeId).Corruption = false;
            }
            else if (SpecFile.Get(nodeId) != null)
            {
                SpecFile.Get(nodeId).Corruption = null;
                SpecFile.Prune(nodeId);
            }
        }

        // Applies the DM's choice outside OnGUI, since changing game state mid-layout upsets IMGUI.
        internal static void Tick()
        {
            if (!decided || Pending == null)
            {
                return;
            }

            Node node = Pending;
            bool wasVisited = AtOManager.Instance.mapVisitedNodes.Contains(node.nodeData.NodeId);
            string assignment = chosen;
            string[] enemies = customEnemies;
            bool skipOffer = skipCorruption;
            Pending = null;
            decided = false;
            chosen = null;
            customEnemies = null;
            slotsNode = null;

            SetNodeCorruption(node.nodeData.NodeId, skipOffer);

            if (assignment != null)
            {
                // The game only runs a node's action the first time it is visited; forget the earlier visit so a pick on a revisit runs.
                AtOManager.Instance.mapVisitedNodes.Remove(node.nodeData.NodeId);
                AtOManager.Instance.gameNodeAssigned[node.nodeData.NodeId] = assignment;
                node.AssignNode();
                SetNodeEnemies(node.nodeData.NodeId, enemies);
                Plugin.Record($"DM picker: node '{node.nodeData.NodeId}'{(wasVisited ? " (visited before)" : "")} -> {assignment}{MapTools.DescribeCombat(assignment)}{(enemies != null ? $" with custom enemies [{string.Join(", ", enemies)}]" : "")}{(skipOffer ? " (no corruption)" : "")}");
            }
            else
            {
                Plugin.Record($"DM picker: node '{node.nodeData.NodeId}'{(wasVisited ? " (visited before)" : "")} kept as is.");
            }

            Bypass = true;
            MapManager.Instance.TravelToThisNode(node);
        }
    }

    // Holds the host's travel to a combat, event or town node until the DM has chosen what it becomes.
    [HarmonyPatch(typeof(MapManager), nameof(MapManager.TravelToThisNode))]
    internal static class TravelToThisNodePatch
    {
        private static bool Prefix(Node _node)
        {
            bool hold = ShouldHold(_node);
            if (!hold)
            {
                Diagnostics.LogCorruptionCheck(_node);
            }
            return !hold;
        }

        private static bool ShouldHold(Node _node)
        {
            if (Picker.Bypass)
            {
                Picker.Bypass = false;
                return false;
            }
            if (!Plugin.DmPicker.Value || Picker.Pending != null || _node == null || _node.nodeData == null)
            {
                return false;
            }
            // Only the host (or a solo player) decides what a node becomes.
            if (GameManager.Instance.IsMultiplayer() && !NetworkManager.Instance.IsMaster())
            {
                return false;
            }
            // Node.action is only filled in for nodes drawn on this map load (not visited ones), so read the saved assignment instead.
            string action = _node.GetNodeAction();
            if (action != "combat" && action != "event" && action != "town")
            {
                return false;
            }

            Picker.Pending = _node;
            return true;
        }
    }
}
