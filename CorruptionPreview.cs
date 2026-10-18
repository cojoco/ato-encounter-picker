using HarmonyLib;

namespace EncounterPicker
{
    // The corruption window shows the enemies of the fight to come. On the campaign map it draws them from the combat's own enemy list
    // (CorruptionManager.cs:512-523), so a node with custom enemies would show the wrong ones. The window is drawn once and has no clean
    // hook to patch (it is a coroutine that may wait on other players first), so while it is open the sprites are set from the node's
    // custom enemies. On the Obelisk Challenge map the window goes through GetRandomCombat, which is already patched.
    internal static class CorruptionPreview
    {
        internal static void Tick()
        {
            CorruptionManager window = MapManager.Instance != null ? MapManager.Instance.corruption : null;
            if (window == null || !window.IsActive())
            {
                return;
            }

            string nodeId = Traverse.Create(window).Field("nodeSelectedDataId").GetValue<string>();
            string[] enemies = SpecFile.EnemiesFor(nodeId);
            if (enemies == null)
            {
                return;
            }

            for (int i = 0; i < window.monsterSprite.Length; i++)
            {
                NPCData npc = i < enemies.Length && !string.IsNullOrEmpty(enemies[i]) ? Globals.Instance.GetNPC(enemies[i]) : null;
                if (npc != null)
                {
                    window.monsterSprite[i].gameObject.SetActive(true);
                    window.monsterSprite[i].sprite = npc.SpriteSpeed;
                }
                else
                {
                    window.monsterSprite[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
