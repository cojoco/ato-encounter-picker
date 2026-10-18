using System.Linq;
using HarmonyLib;

namespace EncounterPicker
{
    // Helpers for inspecting and redrawing the map: the F10 dump and the node redraw used after assignments change.
    internal static class MapTools
    {
        // Redraws every node icon from the current assignments.
        internal static void RedrawNodes()
        {
            foreach (Node node in MapManager.Instance.GetMapNodeDict().Values)
            {
                if (node != null)
                {
                    node.AssignNode();
                }
            }
        }

        // For "combat:<id>" assignments, the combat's fixed enemy list (empty on the random Obelisk Challenge map).
        internal static string DescribeCombat(string assignment)
        {
            if (!assignment.StartsWith("combat:"))
            {
                return "";
            }
            CombatData combat = Globals.Instance.GetCombatData(assignment.Substring("combat:".Length));
            if (combat == null || combat.NPCList == null)
            {
                return "";
            }
            // Read the field directly: the property is patched by KeepAllEnemies, and the dump should show what the combat itself says.
            int removeSlot = Traverse.Create(combat).Field("npcRemoveInMadness0Index").GetValue<int>();
            string removed = removeSlot >= 0 ? $" (slot {removeSlot} removed at madness 0, acts 1-2{(Plugin.KeepAllEnemies.Value ? ", kept by KeepAllEnemies" : "")})" : "";
            return $" enemies [{string.Join(", ", combat.NPCList.Select(n => n != null ? n.Id : ""))}]{removed}";
        }

        internal static string DescribeCombatFor(string nodeId)
        {
            return AtOManager.Instance.gameNodeAssigned.TryGetValue(nodeId, out string value) ? DescribeCombat(value) : "";
        }

        // Logs every assigned node with its zone, name, action and (for combats) enemies.
        internal static void DumpMap()
        {
            var manager = AtOManager.Instance;
            if (manager == null || manager.gameNodeAssigned == null || manager.gameNodeAssigned.Count == 0)
            {
                Plugin.Record("No assigned map nodes yet (not in a run?).");
                return;
            }

            Plugin.Record($"Map dump: current node '{manager.currentMapNode}', {manager.gameNodeAssigned.Count} assigned nodes, {manager.mapVisitedNodes.Count} visited.");
            foreach (var entry in manager.gameNodeAssigned.OrderBy(e => e.Key))
            {
                NodeData node = Globals.Instance.GetNodeData(entry.Key);
                string zone = node != null && node.NodeZone != null ? node.NodeZone.ZoneId : "?";
                string name = node != null ? node.NodeName : "?";
                string visited = manager.mapVisitedNodes.Contains(entry.Key) ? " [visited]" : "";
                Plugin.Record($"  {entry.Key} (zone {zone}, '{name}') -> {entry.Value}{visited}{DescribeCombat(entry.Value)}");
            }
        }
    }
}
