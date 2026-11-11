using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CyanPreview
{
    public static class GameInstallation
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try { return Path.GetFullPath(path.Trim().Trim('"')); }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException) { return ""; }
        }
        public static bool IsInstallation(string path)
        {
            path = Normalize(path);
            return path.Length > 0 && Directory.Exists(Path.Combine(path, "RainWorld_Data"))
                && File.Exists(Path.Combine(path, "RainWorld_Data", "Managed", "Assembly-CSharp.dll"));
        }
        public static IEnumerable<string> LibraryPaths(string text)
        {
            // Steam's current libraryfolders.vdf plus the older numbered-string format.
            foreach (Match match in Regex.Matches(text ?? "", "\"(?:path|[0-9]+)\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
            {
                string path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Path.IsPathRooted(path)) yield return path;
            }
        }
        public static List<string> Find(IEnumerable<string> steamRoots, string savedFolder)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> add = path => { path = Normalize(path); if (IsInstallation(path) && seen.Add(path.TrimEnd(Path.DirectorySeparatorChar))) found.Add(path); };
            add(savedFolder);
            foreach (string root in steamRoots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                add(Path.Combine(root, "steamapps", "common", "Rain World"));
                try
                {
                    string libraries = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(libraries)) continue;
                    foreach (string library in LibraryPaths(File.ReadAllText(libraries)))
                        add(Path.Combine(library, "steamapps", "common", "Rain World"));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException) { }
            }
            return found;
        }
        public static List<string> Detect(string savedFolder)
        {
            var roots = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam") };
            roots.Add(WindowsSteamPaths.CurrentUser());
            roots.Add(WindowsSteamPaths.Machine());
            return Find(roots, savedFolder);
        }
        public static string StartBlocker(bool ready, bool preparing, bool editor, bool failed)
        {
            if (editor) return "Start is disabled in Unity Editor. Open the Windows build.";
            if (preparing) return "Start is disabled while game assets are being prepared.";
            if (!ready) return failed ? "Start is disabled: setup failed. Check the message above and retry." : "Start is disabled until game assets are prepared and an ID is loaded.";
            return null;
        }
    }
}
