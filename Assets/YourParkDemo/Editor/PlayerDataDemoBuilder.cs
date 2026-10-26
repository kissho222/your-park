using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using YourPark.PlayerData;

namespace YourPark.Demo.Editor
{
    public static class PlayerDataDemoBuilder
    {
        public const string ScenePath = "Assets/YourParkDemo/PlayerDataDemo.unity";
        [MenuItem("Your Park/Create PlayerData Demo Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Demo Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .07f, .11f);
            camera.orthographic = true; camera.orthographicSize = .8f;
            cameraObject.transform.position = new Vector3(-.4f, .45f, -5);
            cameraObject.AddComponent<AudioListener>();
            var display = new GameObject("Motion Playback");
            var replay = display.AddComponent<LandmarkMotionReplayer>();
            var replayData = new SerializedObject(replay);
            var points = replayData.FindProperty("points"); points.arraySize = 6;
            var shader = Shader.Find("Unlit/Color");
            string materialPath = "Assets/YourParkDemo/Landmark.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader) { color = new Color(.2f, 1, .7f) }; AssetDatabase.CreateAsset(material, materialPath); }
            for (int i = 0; i < 6; i++)
            {
                var point = GameObject.CreatePrimitive(PrimitiveType.Sphere); point.name = "Landmark " + i;
                Object.DestroyImmediate(point.GetComponent<Collider>());
                point.transform.SetParent(display.transform); point.transform.localScale = Vector3.one * .055f;
                point.GetComponent<Renderer>().sharedMaterial = material;
                points.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
                point.SetActive(false);
            }
            replayData.ApplyModifiedPropertiesWithoutUndo();
            var app = new GameObject("PlayerData Demo");
            var document = app.AddComponent<UIDocument>();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/YourParkDemo/PanelSettings.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(1280, 800);
                AssetDatabase.CreateAsset(panel, "Assets/YourParkDemo/PanelSettings.asset");
            }
            document.panelSettings = panel;
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/YourParkDemo/PlayerDataDemo.tss");
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/YourParkDemo/PlayerDataDemo.uxml");
            var devices = app.AddComponent<UnityCaptureDevices>();
            var audio = app.AddComponent<AudioSource>(); audio.playOnAwake = false;
            var controller = app.AddComponent<PlayerDataDemoController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.FindProperty("capture").objectReferenceValue = devices;
            serialized.FindProperty("speaker").objectReferenceValue = audio;
            serialized.FindProperty("motion").objectReferenceValue = replay;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.companyName = "YourPark"; PlayerSettings.productName = "YourPark Capture Demo";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PlayerData demo scene authored: " + ScenePath);
        }
        public static void BuildWindows() => BuildWindowsInternal(false);
        public static void BuildWindowsDevelopment() => BuildWindowsInternal(true);
        public static void BuildAllWindows() { BuildWindows(); BuildWindowsDevelopment(); }
        private static void BuildWindowsInternal(bool development)
        {
            CreateScene();
            string folder = development ? "Builds/WindowsDevelopment" : "Builds/Windows";
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = folder + "/YourParkCapture.exe",
                target = BuildTarget.StandaloneWindows64, options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Windows demo build failed.");
        }
    }
}
