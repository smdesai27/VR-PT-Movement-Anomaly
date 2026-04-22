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

        void Start()
        {
            LoadMostRecentRecording();
        }

        void Update()
        {
            if (!_loaded) return;

            HandleInput();
            UpdateUI();
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
            // Frame counter
            if (frameCounter != null)
            {
                frameCounter.text = $"Frame {playback.CurrentFrame + 1}/{playback.TotalFrames}" +
                                    $"  |  {playback.PlaybackTime:F2}s / {playback.TotalDuration:F1}s" +
                                    $"  |  Speed: {_speeds[_speedIndex]}x" +
                                    $"  |  {(playback.IsPlaying ? "PLAYING" : "PAUSED")}";
            }

            // Data panel with current frame's angle data
            if (dataPanel != null && _analysis != null)
            {
                int frame = playback.CurrentFrame;
                string data = "";

                if (frame < _analysis.kneeAngles.Count)
                {
                    var knee = _analysis.kneeAngles[frame];
                    data += $"KNEE ANGLES\n";
                    data += $"  Left:  {knee.leftAngle:F1}°\n";
                    data += $"  Right: {knee.rightAngle:F1}°\n";
                    data += $"  Asymmetry: {knee.asymmetry:F1}° [{knee.level}]\n\n";
                }

                if (frame < _analysis.hipAngles.Count)
                {
                    var hip = _analysis.hipAngles[frame];
                    data += $"HIP ANGLES\n";
                    data += $"  Left:  {hip.leftAngle:F1}°\n";
                    data += $"  Right: {hip.rightAngle:F1}°\n";
                    data += $"  Asymmetry: {hip.asymmetry:F1}° [{hip.level}]\n\n";
                }

                if (frame < _analysis.trunkLeanPerFrame.Count)
                {
                    float lean = _analysis.trunkLeanPerFrame[frame];
                    var leanLevel = JointAngleCalculator.ClassifyTrunkLean(lean);
                    float fwdLean = frame < _analysis.trunkForwardLeanPerFrame.Count
                        ? _analysis.trunkForwardLeanPerFrame[frame] : 0f;
                    var fwdLevel = frame < _analysis.trunkForwardLeanLevels.Count
                        ? _analysis.trunkForwardLeanLevels[frame] : AnomalyLevel.Normal;
                    data += $"TRUNK LEAN\n";
                    data += $"  Lateral: {lean:F1}° [{leanLevel}]\n";
                    data += $"  Forward: {fwdLean:F1}° [{fwdLevel}]\n\n";
                }

                if (frame < _analysis.fppaLeft.Count)
                {
                    data += $"FPPA (Knee Valgus)\n";
                    data += $"  Left:  {_analysis.fppaLeft[frame]:F1}° [{_analysis.fppaLevelsLeft[frame]}]\n";
                    data += $"  Right: {_analysis.fppaRight[frame]:F1}° [{_analysis.fppaLevelsRight[frame]}]\n\n";
                }

                if (frame < _analysis.frameLevels.Count)
                {
                    data += $"OVERALL: {_analysis.frameLevels[frame]}\n";
                    data += $"Anomaly frames: {_analysis.anomalyFrames}/{_analysis.totalFrames}";
                }

                dataPanel.text = data;
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

            string filename = Path.GetFileName(pathToLoad);
            SetStatus($"Loaded: {filename}\n" +
                      $"{recording.frameCount} frames, {recording.totalDuration:F1}s\n" +
                      $"Max knee asymmetry: {_analysis.maxKneeAsymmetry:F1}°\n\n" +
                      $"Controls:\n" +
                      $"  A = Play/Pause\n" +
                      $"  Right stick L/R = Step frames\n" +
                      $"  B = Cycle speed\n" +
                      $"  Left stick L/R = Rotate skeleton\n" +
                      $"  X = Reset rotation\n" +
                      $"  [Q/E/R in editor]");
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
            Debug.Log($"[PTReview] {text}");
        }
    }
}
