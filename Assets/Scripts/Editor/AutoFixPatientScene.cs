using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace VRMovementTracker.Editor
{
    /// <summary>
    /// One-click fix for the Patient Recording scene.
    /// Run: VRMovementTracker → Fix Patient Scene (auto-wire everything)
    /// 
    /// This script:
    /// 1. Finds RealisticCharacter and adds/wires SkeletonResolver + BodyTrackingRecorder
    /// 2. Disables the visible mesh on the character
    /// 3. Wires PatientSceneController to the recorder
    /// 4. Configures OVRCameraRig (body tracking, passthrough, floor level)
    /// 5. Removes the old BodyTrackingTarget placeholder if it still exists
    /// 6. Saves the scene
    /// </summary>
    public class AutoFixPatientScene : UnityEditor.Editor
    {
        [MenuItem("VRMovementTracker/Fix Patient Scene (auto-wire everything)")]
        public static void FixPatientScene()
        {
            int fixCount = 0;
            string log = "=== AUTO-FIX PATIENT SCENE ===\n\n";

            // -------------------------------------------------------
            // 1. Find the RealisticCharacter
            // -------------------------------------------------------
            GameObject character = null;

            // Search for any object with "Realistic" or "Character" in name
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go.name.Contains("RealisticCh") || go.name.Contains("StylizedCh"))
                {
                    character = go;
                    break;
                }
            }

            // Also try finding by CharacterRetargeter component
            if (character == null)
            {
                var retargeters = GameObject.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in retargeters)
                {
                    if (mb.GetType().Name.Contains("CharacterRetargeter") ||
                        mb.GetType().Name.Contains("RetargetingLayer"))
                    {
                        character = mb.gameObject;
                        break;
                    }
                }
            }

            if (character == null)
            {
                EditorUtility.DisplayDialog("Character Not Found",
                    "Could not find the body tracking character in the scene.\n\n" +
                    "Make sure you've dragged a character prefab (RealisticCharacter or StylizedCharacter) " +
                    "from Samples/Meta XR Movement SDK/.../Prefabs into the Hierarchy.\n\n" +
                    "Then run this fix again.",
                    "OK");
                return;
            }

            log += $"Found character: {character.name}\n";

            // -------------------------------------------------------
            // 2. Add SkeletonResolver if missing
            // -------------------------------------------------------
            var resolver = character.GetComponent<SkeletonResolver>();
            if (resolver == null)
            {
                resolver = character.AddComponent<SkeletonResolver>();
                log += "  + Added SkeletonResolver\n";
                fixCount++;
            }
            else
            {
                log += "  ✓ SkeletonResolver already exists\n";
            }

            // -------------------------------------------------------
            // 3. Add BodyTrackingRecorder if missing, wire to resolver
            // -------------------------------------------------------
            var recorder = character.GetComponent<BodyTrackingRecorder>();
            if (recorder == null)
            {
                recorder = character.AddComponent<BodyTrackingRecorder>();
                log += "  + Added BodyTrackingRecorder\n";
                fixCount++;
            }
            else
            {
                log += "  ✓ BodyTrackingRecorder already exists\n";
            }

            if (recorder.skeletonResolver == null)
            {
                recorder.skeletonResolver = resolver;
                log += "  + Wired SkeletonResolver → BodyTrackingRecorder\n";
                fixCount++;
            }

            // -------------------------------------------------------
            // 4. Disable visible mesh (SkinnedMeshRenderer on children)
            // -------------------------------------------------------
            int meshesDisabled = 0;
            var skinRenderers = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinRenderers)
            {
                if (smr.enabled)
                {
                    smr.enabled = false;
                    meshesDisabled++;
                }
            }
            // Also disable regular MeshRenderers on the character (not on joint indicators)
            var meshRenderers = character.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                // Only disable if it's a child of the character, not a separate object
                if (mr.transform.IsChildOf(character.transform))
                {
                    mr.enabled = false;
                    meshesDisabled++;
                }
            }

            if (meshesDisabled > 0)
            {
                log += $"  + Disabled {meshesDisabled} mesh renderer(s) (patient won't see avatar)\n";
                fixCount++;
            }
            else
            {
                log += "  ✓ Meshes already disabled\n";
            }

            // -------------------------------------------------------
            // 5. Wire PatientSceneController → recorder
            // -------------------------------------------------------
            var sceneController = GameObject.FindObjectOfType<PatientSceneController>();
            if (sceneController != null)
            {
                if (sceneController.recorder != recorder)
                {
                    sceneController.recorder = recorder;
                    log += "  + Wired PatientSceneController.recorder → character's BodyTrackingRecorder\n";
                    fixCount++;
                }
                else
                {
                    log += "  ✓ PatientSceneController already wired\n";
                }
            }
            else
            {
                log += "  ! PatientSceneController not found in scene\n";
            }

            // -------------------------------------------------------
            // 6. Configure OVRCameraRig / OVRManager
            // -------------------------------------------------------
            var ovrManager = GameObject.FindObjectOfType<OVRManager>();
            if (ovrManager != null)
            {
                // Floor level tracking
                if (ovrManager.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel)
                {
                    ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                    log += "  + Set Tracking Origin: Floor Level\n";
                    fixCount++;
                }

                // Passthrough
                if (!ovrManager.isInsightPassthroughEnabled)
                {
                    ovrManager.isInsightPassthroughEnabled = true;
                    log += "  + Enabled Insight Passthrough\n";
                    fixCount++;
                }

                // Body tracking permissions — these are in OVRManager but some require
                // OVRProjectConfig. We set what we can programmatically.
                log += "\n  NOTE: Some OVRManager settings must be verified manually:\n";
                log += "    - Quest Features → Body Tracking Support: Required\n";
                log += "    - Quest Features → Hand Tracking Support: Controllers and Hands\n";
                log += "    - Movement Tracking → Body Tracking Fidelity: High\n";
                log += "    - Movement Tracking → Body Tracking Joint Set: Full Body\n";
                log += "    Select OVRCameraRig and check these in Inspector.\n";

                // Try to configure OVRProjectConfig programmatically
                try
                {
                    var projectConfig = OVRProjectConfig.CachedProjectConfig;
                    if (projectConfig != null)
                    {
                        // Body tracking
                        projectConfig.bodyTrackingSupport = OVRProjectConfig.FeatureSupport.Required;
                        // Hand tracking
                        projectConfig.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;

                        EditorUtility.SetDirty(projectConfig);
                        log += "  + Set OVRProjectConfig: Body Tracking Required, Hand Tracking Controllers+Hands\n";
                        fixCount++;
                    }
                }
                catch (System.Exception e)
                {
                    log += $"  ! Could not auto-set OVRProjectConfig: {e.Message}\n";
                }

                // Add OVRPassthroughLayer if missing
                var cameraRigObj = ovrManager.gameObject;
                var passthroughLayer = cameraRigObj.GetComponent<OVRPassthroughLayer>();
                if (passthroughLayer == null)
                {
                    passthroughLayer = cameraRigObj.AddComponent<OVRPassthroughLayer>();
                    passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
                    log += "  + Added OVRPassthroughLayer (Underlay)\n";
                    fixCount++;
                }
                else
                {
                    log += "  ✓ OVRPassthroughLayer already exists\n";
                }

                // Fix CenterEyeAnchor camera for passthrough
                var centerEye = cameraRigObj.transform.Find("TrackingSpace/CenterEyeAnchor");
                if (centerEye != null)
                {
                    var cam = centerEye.GetComponent<Camera>();
                    if (cam != null)
                    {
                        if (cam.clearFlags != CameraClearFlags.SolidColor)
                        {
                            cam.clearFlags = CameraClearFlags.SolidColor;
                            cam.backgroundColor = new Color(0, 0, 0, 0);
                            log += "  + Set CenterEyeAnchor camera: SolidColor, transparent black\n";
                            fixCount++;
                        }
                        else
                        {
                            log += "  ✓ CenterEyeAnchor camera already configured\n";
                        }
                    }
                }
                else
                {
                    log += "  ! Could not find CenterEyeAnchor (check OVRCameraRig hierarchy)\n";
                }
            }
            else
            {
                log += "  ! OVRManager not found in scene\n";
            }

            // -------------------------------------------------------
            // 7. Delete old BodyTrackingTarget placeholder
            // -------------------------------------------------------
            var oldTarget = GameObject.Find("BodyTrackingTarget");
            if (oldTarget != null)
            {
                DestroyImmediate(oldTarget);
                log += "\n  + Deleted old BodyTrackingTarget placeholder\n";
                fixCount++;
            }

            // -------------------------------------------------------
            // 8. Mark dirty and save
            // -------------------------------------------------------
            EditorUtility.SetDirty(character);
            if (sceneController != null) EditorUtility.SetDirty(sceneController.gameObject);
            if (ovrManager != null) EditorUtility.SetDirty(ovrManager.gameObject);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            log += $"\n=== DONE: {fixCount} fixes applied. Scene saved. ===";
            Debug.Log(log);

            EditorUtility.DisplayDialog("Patient Scene Fixed",
                $"{fixCount} fixes applied.\n\n" +
                "Check the Console (Window → General → Console) for the full log.\n\n" +
                "Remaining manual check:\n" +
                "Select OVRCameraRig → Inspector → verify:\n" +
                "• Body Tracking Fidelity: High\n" +
                "• Body Tracking Joint Set: Full Body\n" +
                "• Permission Requests → Body Tracking: ✓",
                "OK");
        }
    }
}
