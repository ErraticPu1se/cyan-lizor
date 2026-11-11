using System;
using UnityEngine;

namespace CyanPreview
{
    // User choices only: never persist process IDs, running state or sound requests.
    [Serializable]
    public sealed class CompanionPreferences
    {
        public int version = 1;
        public string gameFolder = "";
        public float scale = DesktopSettings.DefaultScale;
        public ClimbingMode climbingMode = ClimbingMode.EdgesAndBackgrounds;
        public bool sound = true, roam = true, jumps = true, clickToExplore;

        public static CompanionPreferences Read(string json, out string warning)
        {
            warning = null;
            var result = new CompanionPreferences();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                JsonUtility.FromJsonOverwrite(json, result);
                if (result.version != 1) throw new ArgumentException("Unsupported preferences version.");
                if (float.IsNaN(result.scale) || float.IsInfinity(result.scale) || result.scale < .5f || result.scale > 2)
                    result.scale = DesktopSettings.DefaultScale;
                if (!Enum.IsDefined(typeof(ClimbingMode), result.climbingMode)) result.climbingMode = ClimbingMode.EdgesAndBackgrounds;
                result.gameFolder = result.gameFolder ?? "";
                return result;
            }
            catch (ArgumentException)
            {
                warning = "Saved preferences could not be read. Default settings are in use.";
                return new CompanionPreferences();
            }
        }
    }
}
