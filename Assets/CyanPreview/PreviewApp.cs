using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CyanPreview
{
    public sealed class PreviewApp : MonoBehaviour
    {
        ReferenceBridge reference;
        SpriteMeshView view;
        Camera previewCamera;
        string id = "12345", cache, status = "Choose your prepared local reference folder.", error;
        float zoom = 2.5f, depth = .7f, phase;
        bool animate;
        string gameFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Rain World";
        Process importer;
        Task<string> importerOutput, importerError;
        GUIStyle title, subtitle, small;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindObjectOfType<PreviewApp>() == null) new GameObject("Cyan Lizor Preview").AddComponent<PreviewApp>();
        }
        void Start()
        {
            Application.targetFrameRate = 30;
            var backgroundCamera = new GameObject("Background camera").AddComponent<Camera>();
            backgroundCamera.depth = -10;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.10f, .12f, .14f);
            var cameraObject = new GameObject("Preview camera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 130;
            previewCamera.transform.position = new Vector3(-40, 0, -10);
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.12f, .16f, .19f);
            var besideApp = Path.Combine(Path.GetDirectoryName(Application.dataPath), "local-data");
            cache = PlayerPrefs.GetString("ReferenceCache", besideApp);
            id = PlayerPrefs.GetString("LastId", "12345");
            view = new SpriteMeshView(transform);
            if (File.Exists(Path.Combine(cache, "reference.json"))) LoadReference();
        }
        void LoadReference()
        {
            try
            {
                if (reference != null) throw new InvalidOperationException("Restart the app to change reference installations.");
                reference = new ReferenceBridge(cache);
                PlayerPrefs.SetString("ReferenceCache", cache);
                Generate();
            }
            catch (Exception e) { Fail(e); }
        }
        void Generate()
        {
            try
            {
                reference.Generate(id);
                phase = 0;
                reference.Pose(phase, depth);
                reference.Draw();
                view.Refresh(reference);
                view.SetScale(zoom);
                status = reference.Identity + "  ·  Cyan lizard  ·  " + reference.Manifest.gameVersion;
                error = null;
                PlayerPrefs.SetString("LastId", id);
                PlayerPrefs.Save();
            }
            catch (Exception e) { Fail(e); }
        }
        void Fail(Exception e)
        {
            error = e.Message;
            Debug.LogException(e);
        }
        void Update()
        {
            if (importer != null && importer.HasExited && importerOutput.IsCompleted && importerError.IsCompleted)
            {
                if (importer.ExitCode == 0) { importer.Dispose(); importer = null; LoadReference(); }
                else
                {
                    error = "Reference import failed: " + importerError.Result;
                    importer.Dispose(); importer = null;
                }
            }
            if (previewCamera != null)
                previewCamera.rect = new Rect(.29f, .12f, .71f, .75f);
            if (reference == null || reference.Graphics == null || error != null || !animate) return;
            try
            {
                phase += Time.deltaTime * 1.5f;
                reference.Pose(phase, depth);
                reference.Draw();
                view.Refresh(reference);
            }
            catch (Exception e) { animate = false; Fail(e); }
        }
        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "CreatureId" && reference != null)
            { Generate(); Event.current.Use(); }
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
                subtitle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
                title.normal.textColor = new Color(.35f, .95f, .88f);
            }
            GUI.Label(new Rect(24, 18, Screen.width - 48, 40), "CYAN LIZOR", title);
            GUI.Label(new Rect(25, 59, Screen.width - 50, 28), "Appearance lab / original ID generation / controlled preview pose", subtitle);
            float panelWidth = Mathf.Max(235, Screen.width * .26f);
            GUILayout.BeginArea(new Rect(24, 111, panelWidth, Screen.height - 140), GUI.skin.box);
            GUILayout.Space(12);
            GUILayout.Label("CREATURE ID", subtitle);
            GUI.SetNextControlName("CreatureId");
            id = GUILayout.TextField(id, 64, GUILayout.Height(32));
            GUILayout.Label("Seed number or ID.spawner.number", small);
            GUI.enabled = reference != null;
            if (GUILayout.Button("Generate cyan lizard", GUILayout.Height(34))) Generate();
            GUI.enabled = true;
            GUILayout.Space(20);
            GUILayout.Label("SIZE  " + zoom.ToString("0.0") + "×", subtitle);
            float nextZoom = GUILayout.HorizontalSlider(zoom, .75f, 4f);
            if (nextZoom != zoom) { zoom = nextZoom; view?.SetScale(zoom); }
            GUILayout.Space(15);
            GUILayout.Label("VIEW ANGLE", subtitle);
            float nextDepth = GUILayout.HorizontalSlider(depth, -.95f, .95f);
            if (nextDepth != depth && reference != null && error == null)
            {
                depth = nextDepth;
                try { reference.Pose(phase, depth); reference.Draw(); view.Refresh(reference); }
                catch (Exception e) { Fail(e); }
            }
            animate = GUILayout.Toggle(animate, " Gentle pose motion");
            GUILayout.Label("Motion is a preview pose, not game locomotion.", small);
            GUILayout.Space(18);
            if (reference == null)
            {
                GUILayout.Label("RAIN WORLD INSTALLATION", small);
                gameFolder = GUILayout.TextField(gameFolder);
                GUI.enabled = importer == null;
                if (GUILayout.Button("Prepare local game assets")) ImportReference();
                GUI.enabled = true;
                if (importer != null) GUILayout.Label("Preparing your local reference…", small);
                GUILayout.Label("Prepared reference folder", small);
                cache = GUILayout.TextField(cache ?? "");
                if (GUILayout.Button("Load prepared reference")) LoadReference();
            }
            else GUILayout.Label(reference.Summary ?? "", small);
            GUILayout.Space(12);
            if (GUILayout.Button("Save comparison image")) StartCoroutine(Capture());
            GUILayout.EndArea();
            GUI.Label(new Rect(Screen.width * .31f, Screen.height - 85, Screen.width * .66f, 25), status, subtitle);
            GUI.Label(new Rect(Screen.width * .31f, Screen.height - 58, Screen.width * .66f, 45),
                "Neutral preview palette · no mods · visual parity awaiting in-game comparison", small);
            if (!string.IsNullOrEmpty(error))
                GUI.Box(new Rect(Screen.width * .31f, 115, Screen.width * .66f, 220), error);
        }
        void ImportReference()
        {
            try
            {
                if (!File.Exists(Path.Combine(gameFolder, "RainWorld_Data", "Managed", "Assembly-CSharp.dll")))
                    throw new DirectoryNotFoundException("Choose the Rain World installation folder.");
                string exe = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools", "CyanReferenceImporter.exe");
                if (!File.Exists(exe)) throw new FileNotFoundException("Importer is not packaged here. In development, run tools/prepare_reference.py; see README.");
                cache = Path.Combine(Application.persistentDataPath, "local-data");
                importer = Process.Start(new ProcessStartInfo {
                    FileName = exe,
                    Arguments = "--game \"" + Path.GetFullPath(gameFolder).TrimEnd('\\') + "\" --output \"" + cache.TrimEnd('\\') + "\"",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                });
                importerOutput = importer.StandardOutput.ReadToEndAsync();
                importerError = importer.StandardError.ReadToEndAsync();
                error = null;
            }
            catch (Exception e) { importer?.Dispose(); importer = null; Fail(e); }
        }
        System.Collections.IEnumerator Capture()
        {
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            var folder = Path.Combine(Application.persistentDataPath, "Comparisons");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "cyan-" + (reference?.Seed ?? 0) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            File.WriteAllBytes(file, image.EncodeToPNG());
            Destroy(image);
            status = "Saved: " + file;
        }
        void OnDestroy() { view?.Dispose(); reference?.Dispose(); }
    }
}
