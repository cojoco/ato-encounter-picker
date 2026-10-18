using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EncounterPicker
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.cojoco.encounterpicker";
        public const string PluginName = "Encounter Picker";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyboardShortcut> DumpKey;
        internal static ConfigEntry<KeyboardShortcut> LoadSpecKey;
        internal static ConfigEntry<bool> KeepAllEnemies;
        internal static ConfigEntry<bool> DmPicker;
        internal static ConfigEntry<bool> NoCorruption;
        internal static ConfigEntry<bool> Verbose;

        private static string historyPath;

        // Logs to the BepInEx console/log and appends to a file that survives relaunches (LogOutput.log is overwritten each launch).
        internal static void Record(string message)
        {
            Log.LogInfo(message);
            try
            {
                File.AppendAllText(historyPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch (Exception e)
            {
                Log.LogWarning($"Could not write history file: {e.Message}");
            }
        }

        private void Awake()
        {
            Log = Logger;
            historyPath = Path.Combine(Paths.ConfigPath, "encounterpicker-history.log");
            SpecFile.Init();

            DumpKey = Config.Bind("Hotkeys", "DumpMap", new KeyboardShortcut(KeyCode.F10),
                "Logs every map node and what it is assigned to when pressed.");
            LoadSpecKey = Config.Bind("Hotkeys", "LoadSpec", new KeyboardShortcut(KeyCode.F8),
                "Loads encounterpicker-spec.json from the config folder and applies it to the current map.");
            KeepAllEnemies = Config.Bind("Combat", "KeepAllEnemies", false,
                "Skip the easy-mode trimming that removes one enemy from many campaign combats at madness 0 (acts 1-2).");
            DmPicker = Config.Bind("DungeonMaster", "Picker", false,
                "When the party travels to a combat, event or town node, hold the travel and let the host pick what the node becomes. Host or solo only.");
            NoCorruption = Config.Bind("DungeonMaster", "NoCorruption", false,
                "Never show the corruption offer before combats. Host or solo only.");
            Verbose = Config.Bind("Logging", "Verbose", false,
                "Also log every node hover and click, and the checks behind each corruption offer. Makes the history file grow quickly.");
            new Harmony(PluginGuid).PatchAll();

            // The BepInEx plugin object gets destroyed by the game right after startup, so Update() never runs on it.
            // Put the per-frame work and the picker's GUI on our own object instead.
            var runner = new GameObject("EncounterPickerRunner");
            runner.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runner);
            runner.AddComponent<HotkeyRunner>();

            Record("==== New session ====");
            Record($"{PluginName} {PluginVersion} loaded. Dump map: {DumpKey.Value}, load spec: {LoadSpecKey.Value}, DM picker: {(DmPicker.Value ? "on" : "off")}.");
        }
    }

    internal class HotkeyRunner : MonoBehaviour
    {
        private void OnGUI()
        {
            Picker.Draw();
        }

        private void Update()
        {
            Picker.Tick();
            CorruptionPreview.Tick();

            if (Plugin.DumpKey.Value.IsDown())
            {
                MapTools.DumpMap();
            }
            if (Plugin.LoadSpecKey.Value.IsDown())
            {
                SpecFile.Load();
            }
        }
    }
}
