using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRMovementTracker
{
    /// <summary>
    /// Simple scene switcher. Add to both scenes.
    /// Left trigger + grip simultaneously = switch to the other scene.
    /// This avoids needing to rebuild to switch scenes on-device.
    /// </summary>
    public class SceneSwitcher : MonoBehaviour
    {
        [Header("Scene Indices (set in Build Settings)")]
        [Tooltip("Patient Recording scene index")]
        public int patientSceneIndex = 0;
        [Tooltip("PT Review scene index")]
        public int ptReviewSceneIndex = 1;

        private bool _switchReady = false;

        void Update()
        {
            bool switchInput = false;

            try
            {
                // Left hand: grip + trigger simultaneously = scene switch
                bool leftGrip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
                bool leftTrigger = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
                switchInput = leftGrip && leftTrigger;
            }
            catch
            {
                // Editor fallback: Tab key
                switchInput = Input.GetKeyDown(KeyCode.Tab);
            }

            // Require release before next switch (prevent rapid toggling)
            if (switchInput && !_switchReady)
                return;

            if (!switchInput)
            {
                _switchReady = true;
                return;
            }

            if (switchInput && _switchReady)
            {
                _switchReady = false;
                int current = SceneManager.GetActiveScene().buildIndex;
                int target = (current == patientSceneIndex) ? ptReviewSceneIndex : patientSceneIndex;

                Debug.Log($"[SceneSwitcher] Switching from scene {current} to {target}");
                SceneManager.LoadScene(target);
            }
        }
    }
}
