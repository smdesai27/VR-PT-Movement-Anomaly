using System.IO;
using UnityEngine;
using TMPro;

namespace VRMovementTracker
{
    /// <summary>
    /// Controls the PT Review scene.
    /// 
    /// Scene setup:
    /// - OVRCameraRig (PT can walk around freely)
    /// - A playback skeleton positioned ~2m in front of start position
    ///   with SkeletonPlayback component
    /// - World-space Canvas with playback controls and data panel
    /// 
    /// Flow:
    /// 1. Auto-loads the most recent recording on scene start
    /// 2. PT sees frozen skeleton at frame 0
    /// 3. Press A to play/pause, thumbstick left/right to step frames
    /// 4. Data panel shows current frame's angle data
    /// 5. PT walks around to view from different angles
    /// </summary>
    public class PTReviewController : MonoBehaviour
    {
        [Header("References")]
        public SkeletonPlayback playback;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI dataPanel;
        public TextMeshProUGUI frameCounter;

        [Header("Settings")]
        [Tooltip("If set, load this specific file. Otherwise loads most recent recording.")]
        public string specificRecordingPath = "";

        [Header("Rotation")]
        [Tooltip("Degrees per second at full left-stick deflection.")]
        public float rotationSpeed = 90f;
        [Tooltip("Left-stick deadzone to prevent drift.")]
        public float stickDeadzone = 0.15f;

        private bool _loaded = false;
        private SquatAnalysisResult _analysis;
        private float _statusClearTimer = 0f;
        private const float StatusClearDelay = 5f;

        void Start()
        {
            LoadMostRecentRecording();
        }

        void Update()
        {
            if (!_loaded) return;

            HandleInput();
            UpdateUI();

            // Clear status label after delay so it doesn't persist in the PT's view.
            if (_statusClearTimer > 0f)
            {
                _statusClearTimer -= Time.deltaTime;
                if (_statusClearTimer <= 0f && statusText != null)
                    statusText.text = "";
            }
        }

        private void HandleInput()
        {
            bool useOVR = true;

            try
            {
                // A button: play/pause
                if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
                    playback.TogglePlayPause();

                // Right thumbstick left/right: step frames
                Vector2 thumbstick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
                if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.RTouch))
                    playback.StepBackward();
                if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.RTouch))
                    playback.StepForward();

                // B button: toggle speed (1x / 0.5x / 0.25x)
                if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                    CycleSpeed();

                // Left thumbstick X-axis: rotate skeleton yaw
                Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
                if (Mathf.Abs(leftStick.x) > stickDeadzone)
                    playback.AddYaw(leftStick.x * rotationSpeed * Time.deltaTime);

                // X button (Button.Three on left controller): reset view rotation
                if (OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch))
                    playback.ResetView();
            }
            catch
            {
                useOVR = false;
            }

            if (!useOVR)
            {
                // Keyboard fallback for editor testing
                if (Input.GetKeyDown(KeyCode.Space))
                    playback.TogglePlayPause();
                if (Input.GetKeyDown(KeyCode.RightArrow))
                    playback.StepForward();
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                    playback.StepBackward();
                if (Input.GetKeyDown(KeyCode.S))
                    CycleSpeed();
                if (Input.GetKey(KeyCode.Q))
                    playback.AddYaw(-rotationSpeed * Time.deltaTime);
                if (Input.GetKey(KeyCode.E))
                    playback.AddYaw(rotationSpeed * Time.deltaTime);
                if (Input.GetKeyDown(KeyCode.R))
                    playback.ResetView();
            }
        }

        private float[] _speeds = new float[] { 1f, 0.5f, 0.25f };
        private int _speedIndex = 0;

        private void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % _speeds.Length;
            playback.SetPlaybackSpeed(_speeds[_speedIndex]);
        }

        private void UpdateUI()
        {
            if (frameCounter != null)
            {
                string state = playback.IsPlaying ? "PLAYING" : "PAUSED";
                frameCounter.text =
                    $"FRAME {playback.CurrentFrame + 1} / {playback.TotalFrames}" +
                    $"  \u00b7  {_speeds[_speedIndex]}x  \u00b7  {state}";
            }
        }

        private void LoadMostRecentRecording()
        {
            string pathToLoad = specificRecordingPath;

            if (string.IsNullOrEmpty(pathToLoad))
            {
                // Find most recent recording
                string[] recordings = BodyTrackingRecorder.GetAllRecordings();
                if (recordings.Length == 0)
                {
                    SetStatus("No recordings found.\nRecord a session in the Patient scene first.");
                    return;
                }

                // Sort by filename (which includes timestamp) and pick the latest
                System.Array.Sort(recordings);
                pathToLoad = recordings[recordings.Length - 1];
            }

            // Load and analyze
            MovementRecording recording = BodyTrackingRecorder.LoadRecording(pathToLoad);
            if (recording == null)
            {
                SetStatus($"Failed to load recording:\n{pathToLoad}");
                return;
            }

            _analysis = SquatAnalyzer.Analyze(recording);
            playback.LoadRecording(recording, _analysis);
            _loaded = true;

            // Set frame counter font size to 36pt for VR legibility.
            if (frameCounter != null)
                frameCounter.fontSize = 36f;

            // Show a brief summary; auto-clears after StatusClearDelay seconds
            // so it doesn't clutter the PT's view of the skeleton.
            string filename = Path.GetFileName(pathToLoad);
            SetStatus($"Loaded: {filename}  |  {recording.frameCount} frames, {recording.totalDuration:F1}s");
            _statusClearTimer = StatusClearDelay;
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
            Debug.Log($"[PTReview] {text}");
        }
    }
}
