using System;
namespace CyanPreview
{
    public enum TrayAction { None, Settings, PauseResume, Sound, StopPet, Quit, Hidden }
    public sealed class TrayState
    {
        public bool Paused { get; private set; }
        public bool Quitting { get; private set; }
        public void TogglePause(bool petActive) { if (!Quitting && petActive) Paused = !Paused; }
        public void Sync(bool petActive) { if (!petActive) Paused = false; }
        public void Quit() { Quitting = true; Paused = false; }
        public static bool HideOnClose(bool iconAvailable, bool exiting, bool endingSession) => iconAvailable && !exiting && !endingSession;
    }
    public static class PreferenceMigration
    {
        public const string Marker = "Lizor.LegacyImported";
        public static readonly string[] Keys = { "LastId", "ReferenceCache", "CompanionPreferences.v1" };
        public static void Apply(Func<string,bool> has, Action<string,string> set, Func<string,string> read)
        {
            if (has(Marker)) return;
            foreach (string key in Keys)
            {
                if (has(key)) continue;
                string value = read(key);
                if (value != null) set(key, value);
            }
            set(Marker, "1");
        }
    }
}
