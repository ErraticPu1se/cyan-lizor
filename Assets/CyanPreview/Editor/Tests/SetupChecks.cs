using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CyanPreview.Editor
{
    public static class SetupChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run()
        {
            var defaults = CompanionPreferences.Read("", out var warning);
            Require(warning == null && defaults.scale == 1.2f && defaults.sound && defaults.roam && defaults.jumps && !defaults.clickToExplore, "First launch defaults changed");
            var partial = CompanionPreferences.Read("{\"sound\":false}", out warning);
            Require(!partial.sound && partial.roam && partial.scale == 1.2f, "Missing preference fields lost defaults");
            foreach (string invalid in new[] { "not json", "{", "{\"version\":99}" })
            {
                var restored = CompanionPreferences.Read(invalid, out warning);
                Require(warning != null && restored.scale == 1.2f, "Invalid preferences not recovered");
            }
            foreach (float scale in new[] { -.1f, 5f })
            {
                var restored = CompanionPreferences.Read("{\"scale\":" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"climbingMode\":999}", out warning);
                Require(restored.scale == 1.2f && restored.climbingMode == ClimbingMode.EdgesAndBackgrounds, "Unsafe preference values restored");
            }
            var expected = new CompanionPreferences { gameFolder = @"D:\Games\雨世界", scale = 1.5f, sound = false, roam = false, jumps = false, clickToExplore = true, climbingMode = ClimbingMode.EdgesOnly };
            const string key = "CyanSetupTest.preferences";
            string before = PlayerPrefs.GetString(key, ""); bool existed = PlayerPrefs.HasKey(key);
            try
            {
                string json = JsonUtility.ToJson(expected);
                Require(!json.Contains("controllerPid") && !json.Contains("vocalizeRequest") && !json.Contains("running"), "Runtime commands persisted");
                PlayerPrefs.SetString(key, json); PlayerPrefs.Save();
                var restored = CompanionPreferences.Read(PlayerPrefs.GetString(key), out warning);
                Require(JsonUtility.ToJson(restored) == json && warning == null, "Preferences did not round trip through storage");
            }
            finally { if (existed) PlayerPrefs.SetString(key, before); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
            string temp = Path.Combine(Path.GetTempPath(), "cyan-setup-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string steam = Path.Combine(temp, "Steam"), library = Path.Combine(temp, "Other 雨 library");
                string game = Path.Combine(library, "steamapps", "common", "Rain World");
                Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
                Directory.CreateDirectory(Path.Combine(game, "RainWorld_Data", "Managed"));
                File.WriteAllText(Path.Combine(game, "RainWorld_Data", "Managed", "Assembly-CSharp.dll"), "test fixture, not a game assembly");
                string escaped = library.Replace(((char)92).ToString(), new string((char)92, 2));
                File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\" \"" + escaped + "\" } }");
                var found = GameInstallation.Find(new[] { steam, steam, Path.Combine(temp, "Missing") }, "");
                Require(found.Count == 1 && found[0] == game, "Additional Steam library or duplicate detection failed");
                Require(GameInstallation.Find(new[] { steam }, game).Count == 1, "Saved installation duplicated");
                Require(GameInstallation.LibraryPaths("\"1\" \"" + escaped + "\"").Single() == library, "Legacy Steam library format failed");
                Require(GameInstallation.IsInstallation(" \"" + game + "\" "), "Quoted/Unicode/spaced folder rejected");
                Require(!GameInstallation.IsInstallation(Path.Combine(game, "RainWorld_Data")), "Data subfolder accepted as installation");
                Require(!GameInstallation.IsInstallation("\0") && !GameInstallation.IsInstallation(""), "Invalid input accepted");
                File.Delete(Path.Combine(game, "RainWorld_Data", "Managed", "Assembly-CSharp.dll"));
                Require(GameInstallation.Find(new[] { steam }, game).Count == 0, "Removed installation accepted");
                File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), "malformed \"path\"");
                Require(GameInstallation.Find(new[] { steam }, "").Count == 0, "Malformed library list failed");
            }
            finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
            Require(GameInstallation.StartBlocker(true, false, false, false) == null, "Ready player blocked");
            Require(GameInstallation.StartBlocker(false, true, false, false).Contains("prepared"), "Import blocker missing");
            Require(GameInstallation.StartBlocker(false, false, false, true).Contains("failed"), "Failure blocker missing");
            Require(GameInstallation.StartBlocker(false, false, false, false) != null, "Unprepared player allowed");
            Require(GameInstallation.StartBlocker(true, false, true, false).Contains("Editor"), "Editor launch allowed");
            Debug.Log("SETUP_PREFERENCES_CHECKS_PASSED");
        }
    }
}
