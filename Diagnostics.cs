using System.Collections.Generic;
using HarmonyLib;

namespace EncounterPicker
{
    // Patches that only log, so what the game did can be checked against what was expected. The first three are always on; the rest only log
    // when [Logging] Verbose is set, since they fire on every hover and click.

    // Logs the enemies the combat scene actually spawns, to compare against what SetCombatData chose.
    [HarmonyPatch(typeof(BattleMatch.BattleTeamManager), nameof(BattleMatch.BattleTeamManager.GenerateNPCs))]
    internal static class GenerateNPCsPatch
    {
        private static void Postfix()
        {
            var spawned = new List<string>();
            foreach (Character c in MatchManager.Instance.GetTeamNPC())
            {
                spawned.Add(c == null ? "-" : (c.NpcData != null ? c.NpcData.Id : "?") + (c.Alive ? "" : "(dead)"));
            }
            CombatData combat = AtOManager.Instance.GetCurrentCombatData();
            Plugin.Record($"Combat scene spawned enemies: [{string.Join(", ", spawned)}] (sandbox LessNPCs={SandboxManager.Instance.LessNPCs}, madness={AtOManager.Instance.GetMadnessDifficulty()}, combat removes slot {(combat != null ? combat.NpcRemoveInMadness0Index : -1)})");
        }
    }

    // What the game actually paid out, to compare with the picker's estimate (which leaves out the performance bonus).
    [HarmonyPatch(typeof(RewardsManager), "ShowRewards")]
    internal static class ShowRewardsPatch
    {
        private static void Postfix(RewardsManager __instance)
        {
            int tier = Traverse.Create(__instance).Field("tierRewardBase").GetValue<TierRewardData>()?.TierNum ?? -1;
            Plugin.Record($"Rewards shown: each hero {__instance.experienceEach} xp / {__instance.goldEach} gold, loot tier {tier}, enemies [{string.Join(", ", AtOManager.Instance.GetTeamNPC() ?? new string[0])}]");
        }
    }

    // Confirms when the corruption offer is actually rolled.
    [HarmonyPatch(typeof(CorruptionManager), nameof(CorruptionManager.InitCorruption))]
    internal static class InitCorruptionPatch
    {
        private static void Prefix(Node _node, bool next)
        {
            Plugin.Record($"Corruption offered at node {_node?.nodeData?.NodeId}{(next ? " (next card)" : "")}");
        }
    }

    // Logs which node the cursor is on, so on-screen names can be matched to node ids.
    [HarmonyPatch(typeof(MapManager), nameof(MapManager.ShowPopup))]
    internal static class ShowPopupPatch
    {
        private static void Postfix(Node _node)
        {
            if (!Plugin.Verbose.Value || _node == null || _node.nodeData == null)
            {
                return;
            }
            Plugin.Record($"Hover: {_node.nodeData.NodeId} ('{_node.nodeData.NodeName}') -> {AtOManager.Instance.gameNodeAssigned.GetValueOrDefault(_node.nodeData.NodeId, "")}{MapTools.DescribeCombatFor(_node.nodeData.NodeId)}");
        }
    }

    // Logs every click on a map node with the state that decides whether the game acts on it.
    [HarmonyPatch(typeof(Node), "OnMouseUp")]
    internal static class NodeClickPatch
    {
        private static void Prefix(Node __instance)
        {
            if (!Plugin.Verbose.Value || __instance.nodeData == null || MapManager.Instance == null)
            {
                return;
            }
            string id = __instance.nodeData.NodeId;
            Plugin.Record($"Click: {id} action='{__instance.action}' assigned='{__instance.GetNodeAction()}' canTravel={MapManager.Instance.CanTravelToThisNode(__instance)} " +
                $"visited={AtOManager.Instance.mapVisitedNodes.Contains(id)} selectedNode={MapManager.Instance.selectedNode} " +
                $"current='{AtOManager.Instance.currentMapNode}' picker={(Picker.Pending != null ? Picker.Pending.nodeData.NodeId : "none")}");
        }
    }

    internal static class Diagnostics
    {
        // Mirrors the conditions MapManager.TravelToThisNodeCorruption checks (MapManager.cs:1627-1650), so a combat that did or did not
        // offer a corruption can be explained from the log.
        internal static void LogCorruptionCheck(Node node)
        {
            if (!Plugin.Verbose.Value || node == null || node.nodeData == null || node.GetNodeAction() != "combat")
            {
                return;
            }
            string id = node.nodeData.NodeId;
            bool hardcoded = AtOManager.Instance.GetGameId() == "tuto".ToUpper() || id == "tutorial_1" || id == "sen_1" || id == "sen_2" || id == "sen_3" || id == "aqua_27";
            bool rawDisabled = Traverse.Create(node.nodeData).Field("disableCorruption").GetValue<bool>();
            CombatData combat = Globals.Instance.GetCombatData(node.GetNodeAssignedId());
            string endEvent = combat?.EventData == null ? "none"
                : $"{combat.EventData.EventId} (requirement {(combat.EventRequirementData == null ? "none" : AtOManager.Instance.PlayerHasRequirement(combat.EventRequirementData) ? "met" : "not met")})";
            Plugin.Record($"Corruption check: node {id}, combat {node.GetNodeAssignedId()}, hardcoded exclusion {hardcoded}, node flag disables {rawDisabled}, end-of-combat event {endEvent}");
        }
    }
}
