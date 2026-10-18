using HarmonyLib;

namespace MapDumper
{
    // Harmony patches that change what the game does. Patches that only log live in Diagnostics.cs; the travel hook is in Picker.cs.

    // Applies a node's custom enemies after the game has chosen its own, and logs what was chosen.
    [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.SetCombatData))]
    internal static class SetCombatDataPatch
    {
        private static void Postfix(CombatData _combatData, string[] ___teamNPCAtO)
        {
            if (_combatData == null)
            {
                return;
            }
            string[] chosen = ___teamNPCAtO ?? new string[0];
            Plugin.Record($"SetCombatData: combat '{_combatData.CombatId}' at node '{AtOManager.Instance.currentMapNode}' -> enemies [{string.Join(", ", chosen)}]");

            string[] forced = SpecFile.EnemiesFor(AtOManager.Instance.currentMapNode);
            if (forced != null)
            {
                for (int i = 0; i < forced.Length; i++)
                {
                    AtOManager.Instance.SetTeamNPCSingle(forced[i], i);
                }
                Plugin.Record($"  overridden with [{string.Join(", ", forced)}]");
            }
        }
    }

    // The map re-rolls unvisited nodes when it loads, so reapply the spec's combats and events afterwards.
    [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.AssignSingleGameNode))]
    internal static class AssignSingleGameNodePatch
    {
        private static void Postfix(Node _node)
        {
            SpecFile.ApplyToNode(_node.nodeData.NodeId);
        }
    }

    // The map's node popup previews enemies by calling this directly, and the Obelisk Challenge map rolls enemies here, so patch the roll
    // itself to keep the preview and the fight consistent.
    [HarmonyPatch(typeof(Functions), nameof(Functions.GetRandomCombat))]
    internal static class GetRandomCombatPatch
    {
        private static void Postfix(ref NPCData[] __result, string nodeSelectedId, bool forceIsThereRare)
        {
            string[] forced = SpecFile.EnemiesFor(nodeSelectedId);
            if (forced == null || forceIsThereRare)
            {
                return;
            }
            var replaced = new NPCData[4];
            for (int i = 0; i < replaced.Length && i < forced.Length; i++)
            {
                if (!string.IsNullOrEmpty(forced[i]))
                {
                    replaced[i] = Globals.Instance.GetNPC(forced[i]);
                }
            }
            __result = replaced;
        }
    }

    // Both the popup preview and BattleTeamManager.InitNPCTeam read the trim slot through this getter, so patching it keeps them consistent.
    [HarmonyPatch(typeof(CombatData), nameof(CombatData.NpcRemoveInMadness0Index), MethodType.Getter)]
    internal static class KeepAllEnemiesPatch
    {
        private static void Postfix(ref int __result)
        {
            if (Plugin.KeepAllEnemies.Value)
            {
                __result = -1;
            }
        }
    }

    // The game decides "is this a town" from the node's data, not from the assignment. Make the data follow the assignment, so a node the DM
    // turned into a town (or out of one) behaves that way when travelling to it, on the map icon, and after a save is reloaded.
    [HarmonyPatch(typeof(NodeData), nameof(NodeData.GoToTown), MethodType.Getter)]
    internal static class GoToTownPatch
    {
        private static void Postfix(NodeData __instance, ref bool __result)
        {
            var assigned = AtOManager.Instance?.gameNodeAssigned;
            if (assigned == null || __instance.NodeId == null || !assigned.TryGetValue(__instance.NodeId, out string value) || value == null)
            {
                return;
            }
            if (value == "town:town")
            {
                __result = true;
            }
            else if (__result && (value.StartsWith("combat:") || value.StartsWith("event:")))
            {
                __result = false;
            }
        }
    }

    // The travel code skips the corruption offer for nodes whose data says so (MapManager.cs:1635). Make that follow the DM's per-node
    // choice and the global NoCorruption setting.
    [HarmonyPatch(typeof(NodeData), nameof(NodeData.DisableCorruption), MethodType.Getter)]
    internal static class DisableCorruptionPatch
    {
        private static void Postfix(NodeData __instance, ref bool __result)
        {
            if (Plugin.NoCorruption.Value || SpecFile.Get(__instance.NodeId)?.Corruption == false)
            {
                __result = true;
            }
        }
    }
}
