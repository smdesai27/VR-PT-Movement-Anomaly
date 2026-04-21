using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

namespace VRMovementTracker.Editor
{
    /// <summary>
    /// Automates scene setup for the VR Movement Tracker project.
    /// Run from Unity menu: VRMovementTracker → Setup Patient Scene / Setup PT Review Scene
    /// </summary>
    public class SceneSetupEditor : UnityEditor.Editor
    {
        // =====================================================================
        // PATIENT RECORDING SCENE
        // =====================================================================
        [MenuItem("VRMovementTracker/1. Setup Patient Recording Scene")]
        public static void SetupPatientScene()
        {
            // Create a new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Lighting ---
            var light = new GameObject("Directional Light");
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            // --- OVRCameraRig ---
            GameObject cameraRig = FindAndInstantiatePrefab("OVRCameraRig");
            if (cameraRig == null)
            {
                EditorUtility.DisplayDialog("Missing OVRCameraRig",
                    "Could not find OVRCameraRig prefab.\n\n" +
                    "Make sure the Meta XR All-in-One SDK is installed:\n" +
                    "Package Manager → Add by name → com.meta.xr.sdk.all\n\n" +
                    "Then run this setup again.",
                    "OK");
                return;
            }

            // Configure OVRManager
            var ovrManager = cameraRig.GetComponent<OVRManager>();
            if (ovrManager != null)
            {
                ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                // Note: Body tracking, passthrough, and permission settings
                // need to be configured via OVRManager Inspector or OculusProjectConfig.
                // The script below logs what the user needs to manually verify.
            }

            // --- Body Tracking Character (invisible, data-only) ---
            // We create a minimal placeholder. The user will replace this with
            // a real humanoid character from Movement SDK samples or Mixamo.
            GameObject trackingTarget = new GameObject("BodyTrackingTarget");
            trackingTarget.transform.position = Vector3.zero;

            // Add our scripts
            var resolver = trackingTarget.AddComponent<SkeletonResolver>();
            var recorder = trackingTarget.AddComponent<BodyTrackingRecorder>();
            recorder.skeletonResolver = resolver;
            recorder.targetFPS = 30;

            // Add a note component so the user knows to replace this
            AddInfoTag(trackingTarget,
                "REPLACE THIS with a real humanoid character.\n" +
                "1. Import Movement SDK body tracking samples, OR\n" +
                "2. Drag a Mixamo character here\n" +
                "3. Right-click it → Movement SDK → Body Tracking → Add Character Retargeter\n" +
                "4. Disable its mesh renderer (patient shouldn't see avatar)\n" +
                "5. Move SkeletonResolver + BodyTrackingRecorder to the new character");

            // --- World-Space UI Canvas ---
            GameObject canvasObj = CreateWorldSpaceCanvas("PatientUI",
                new Vector3(0, 1.5f, 2f), // 2m in front, eye height
                new Vector2(0.8f, 0.5f));  // 80cm x 50cm

            // Status text (large, centered)
            GameObject statusTextObj = CreateTMPText(canvasObj, "StatusText",
                new Vector2(0, 40), new Vector2(700, 300),
                "Press RIGHT TRIGGER to start recording",
                36, TextAlignmentOptions.Center);

            // Timer text (smaller, below)
            GameObject timerTextObj = CreateTMPText(canvasObj, "TimerText",
                new Vector2(0, -120), new Vector2(700, 80),
                "",
                28, TextAlignmentOptions.Center);

            // --- Scene Controller ---
            GameObject controller = new GameObject("PatientSceneController");
            var patientController = controller.AddComponent<PatientSceneController>();
            patientController.recorder = recorder;
            patientController.statusText = statusTextObj.GetComponent<TextMeshProUGUI>();
            patientController.timerText = timerTextObj.GetComponent<TextMeshProUGUI>();
            patientController.maxRecordingDuration = 60f;
            patientController.countdownSeconds = 3;

            // --- Scene Switcher (left grip + trigger to switch scenes) ---
            var switcherObj = new GameObject("SceneSwitcher");
            switcherObj.AddComponent<SceneSwitcher>();

            // --- Save scene ---
            EnsureDirectoryExists("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/PatientRecording.unity");

            // --- Log setup instructions ---
            Debug.Log("=== PATIENT RECORDING SCENE CREATED ===");
            Debug.Log("IMPORTANT: You still need to do these manual steps:");
            Debug.Log("1. Select OVRCameraRig → OVRManager Inspector:");
            Debug.Log("   - Quest Features → Body Tracking Support: Required");
            Debug.Log("   - Quest Features → Hand Tracking Support: Controllers and Hands");
            Debug.Log("   - Permission Requests On Startup → Body Tracking: ✓");
            Debug.Log("   - Movement Tracking → Body Tracking Fidelity: High");
            Debug.Log("   - Movement Tracking → Body Tracking Joint Set: Full Body");
            Debug.Log("   - Insight Passthrough → Enable Passthrough: ✓");
            Debug.Log("2. On OVRCameraRig, add component: OVR Passthrough Layer (Placement: Underlay)");
            Debug.Log("3. On CenterEyeAnchor camera: Clear Flags = Solid Color, Background = black (alpha 0)");
            Debug.Log("4. Replace 'BodyTrackingTarget' with a real humanoid character (see the tag on it)");
            Debug.Log("=========================================");

            EditorUtility.DisplayDialog("Patient Scene Created",
                "Scene saved to Assets/Scenes/PatientRecording.unity\n\n" +
                "Check the Console (Window → General → Console) for remaining manual steps.\n\n" +
                "Most important: replace 'BodyTrackingTarget' with a real humanoid character.",
                "OK");
        }

        // =====================================================================
        // PT REVIEW SCENE
        // =====================================================================
        [MenuItem("VRMovementTracker/2. Setup PT Review Scene")]
        public static void SetupPTReviewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Lighting ---
            var light = new GameObject("Directional Light");
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            lightComp.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Ambient light so skeleton spheres are visible from all angles
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.45f);

            // --- OVRCameraRig ---
            GameObject cameraRig = FindAndInstantiatePrefab("OVRCameraRig");
            if (cameraRig == null)
            {
                EditorUtility.DisplayDialog("Missing OVRCameraRig",
                    "Could not find OVRCameraRig prefab. Install Meta XR SDK first.", "OK");
                return;
            }

            var ovrManager = cameraRig.GetComponent<OVRManager>();
            if (ovrManager != null)
            {
                ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            }

            // --- Floor ---
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3, 1, 3);
            var floorRenderer = floor.GetComponent<Renderer>();
            floorRenderer.material = new Material(Shader.Find("Standard"));
            floorRenderer.material.color = new Color(0.15f, 0.15f, 0.18f);

            // --- Grid lines on floor (visual reference for PT) ---
            CreateFloorGrid();

            // --- Playback Skeleton ---
            // Positioned 2m in front of where the PT starts
            GameObject playbackSkeleton = new GameObject("PlaybackSkeleton");
            playbackSkeleton.transform.position = new Vector3(0, 0, 2f);
            var playbackComp = playbackSkeleton.AddComponent<SkeletonPlayback>();

            // --- Data Panel Canvas (to the PT's right) ---
            GameObject dataCanvas = CreateWorldSpaceCanvas("DataPanelUI",
                new Vector3(1.5f, 1.3f, 2f),  // To the right of the skeleton
                new Vector2(0.6f, 0.8f));
            dataCanvas.transform.rotation = Quaternion.Euler(0, -30, 0); // Angled toward PT

            // Dark semi-transparent background for readability
            var canvasBg = dataCanvas.GetComponentInChildren<Canvas>().gameObject;
            var bgImage = canvasBg.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            GameObject statusText = CreateTMPText(canvasObj: dataCanvas, "StatusText",
                new Vector2(0, 260), new Vector2(520, 200),
                "Loading recording...",
                20, TextAlignmentOptions.TopLeft);

            GameObject dataPanel = CreateTMPText(canvasObj: dataCanvas, "DataPanel",
                new Vector2(0, 0), new Vector2(520, 350),
                "",
                18, TextAlignmentOptions.TopLeft);

            // Use monospace for data readability
            var dataTMP = dataPanel.GetComponent<TextMeshProUGUI>();
            dataTMP.font = TMP_Settings.defaultFontAsset; // Will use default; user can swap to mono

            GameObject frameCounter = CreateTMPText(canvasObj: dataCanvas, "FrameCounter",
                new Vector2(0, -280), new Vector2(520, 60),
                "Frame 0/0",
                16, TextAlignmentOptions.BottomLeft);

            // --- Controls hint panel (to the PT's left) ---
            GameObject controlsCanvas = CreateWorldSpaceCanvas("ControlsUI",
                new Vector3(-1.5f, 1.3f, 2f),
                new Vector2(0.5f, 0.4f));
            controlsCanvas.transform.rotation = Quaternion.Euler(0, 30, 0);

            var ctrlBg = controlsCanvas.GetComponentInChildren<Canvas>().gameObject;
            var ctrlBgImage = ctrlBg.AddComponent<UnityEngine.UI.Image>();
            ctrlBgImage.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            CreateTMPText(controlsCanvas, "ControlsText",
                new Vector2(0, 0), new Vector2(440, 320),
                "CONTROLS\n\n" +
                "A Button — Play / Pause\n" +
                "Thumbstick ← → — Step frames\n" +
                "B Button — Cycle speed\n\n" +
                "Walk around to view\nfrom different angles",
                20, TextAlignmentOptions.Center);

            // --- Review Controller ---
            GameObject reviewObj = new GameObject("PTReviewController");
            var reviewController = reviewObj.AddComponent<PTReviewController>();
            reviewController.playback = playbackComp;
            reviewController.statusText = statusText.GetComponent<TextMeshProUGUI>();
            reviewController.dataPanel = dataPanel.GetComponent<TextMeshProUGUI>();
            reviewController.frameCounter = frameCounter.GetComponent<TextMeshProUGUI>();

            // --- Scene Switcher ---
            var switcherObj = new GameObject("SceneSwitcher");
            switcherObj.AddComponent<SceneSwitcher>();

            // --- Save ---
            EnsureDirectoryExists("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/PTReview.unity");

            Debug.Log("=== PT REVIEW SCENE CREATED ===");
            Debug.Log("Manual steps:");
            Debug.Log("1. Select OVRCameraRig → OVRManager → same body tracking settings as Patient scene");
            Debug.Log("2. (Optional) No passthrough needed for this scene — PT is in VR");
            Debug.Log("3. Add both scenes to Build Settings (File → Build Settings → Add Open Scenes)");
            Debug.Log("================================");

            EditorUtility.DisplayDialog("PT Review Scene Created",
                "Scene saved to Assets/Scenes/PTReview.unity\n\n" +
                "Check Console for remaining manual steps.\n\n" +
                "The playback skeleton will auto-create joint spheres and bone lines at runtime.",
                "OK");
        }

        // =====================================================================
        // BUILD SETTINGS HELPER
        // =====================================================================
        [MenuItem("VRMovementTracker/3. Add Scenes to Build Settings")]
        public static void AddScenesToBuild()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/PatientRecording.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/PTReview.unity", true),
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("Build settings updated: PatientRecording (0), PTReview (1)");
            EditorUtility.DisplayDialog("Build Settings Updated",
                "Both scenes added to build settings.\n\n" +
                "PatientRecording = Scene 0 (launches first)\n" +
                "PTReview = Scene 1",
                "OK");
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static GameObject FindAndInstantiatePrefab(string prefabName)
        {
            // Search for the prefab in the project
            string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains(prefabName))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        instance.transform.position = Vector3.zero;
                        return instance;
                    }
                }
            }
            return null;
        }

        private static GameObject CreateWorldSpaceCanvas(string name, Vector3 position, Vector2 sizeMeters)
        {
            GameObject canvasObj = new GameObject(name);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var rectTransform = canvasObj.GetComponent<RectTransform>();
            rectTransform.position = position;
            // Convert meters to canvas units (1 unit = 1 pixel at scale)
            // We use a scale factor to make the canvas world-sized
            float pixelsPerMeter = 1000f;
            rectTransform.sizeDelta = new Vector2(
                sizeMeters.x * pixelsPerMeter,
                sizeMeters.y * pixelsPerMeter);
            rectTransform.localScale = Vector3.one / pixelsPerMeter;

            return canvasObj;
        }

        private static GameObject CreateTMPText(GameObject canvasObj, string name,
            Vector2 anchoredPosition, Vector2 sizeDelta,
            string defaultText, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(canvasObj.transform, false);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;

            var rect = textObj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            return textObj;
        }

        private static void CreateFloorGrid()
        {
            // Simple grid lines using thin cubes
            var gridParent = new GameObject("FloorGrid");
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);

            for (int i = -5; i <= 5; i++)
            {
                // X lines
                var lineX = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lineX.transform.SetParent(gridParent.transform);
                lineX.transform.position = new Vector3(0, 0.001f, i * 0.5f);
                lineX.transform.localScale = new Vector3(5, 0.002f, 0.005f);
                lineX.GetComponent<Renderer>().material = mat;
                Object.DestroyImmediate(lineX.GetComponent<Collider>());

                // Z lines
                var lineZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lineZ.transform.SetParent(gridParent.transform);
                lineZ.transform.position = new Vector3(i * 0.5f, 0.001f, 0);
                lineZ.transform.localScale = new Vector3(0.005f, 0.002f, 5);
                lineZ.GetComponent<Renderer>().material = mat;
                Object.DestroyImmediate(lineZ.GetComponent<Collider>());
            }
        }

        private static void AddInfoTag(GameObject obj, string message)
        {
            // We can't add arbitrary text to GameObjects in the editor,
            // so we add it as a disabled MonoBehaviour with a header.
            // The user will see it in the Inspector.
            var tag = obj.AddComponent<InfoTag>();
            tag.message = message;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path);
                string folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
