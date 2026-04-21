using System;
using System.IO;
using UnityEngine;

namespace VRMovementTracker
{
    /// <summary>
    /// Records body tracking joint data during patient exercise.
    /// Attach to the same GameObject as SkeletonResolver (the tracked character).
    /// 
    /// Usage flow:
    /// 1. Patient puts on headset, scene loads
    /// 2. Patient presses trigger → StartRecording()
    /// 3. Patient performs squats
    /// 4. Patient presses trigger again → StopRecording()
    /// 5. Recording is saved to persistent data path as JSON
    /// </summary>
    public class BodyTrackingRecorder : MonoBehaviour
    {
        [Header("References")]
        public SkeletonResolver skeletonResolver;

        [Header("Settings")]
        [Tooltip("Target capture rate in frames per second. 30 is sufficient for movement analysis.")]
        public int targetFPS = 30;

        [Header("State (read-only)")]
        [SerializeField] private bool _isRecording = false;
        [SerializeField] private float _recordingTime = 0f;
        [SerializeField] private int _framesCaptured = 0;

        private MovementRecording _currentRecording;
        private float _captureInterval;
        private float _timeSinceLastCapture;
        private string _lastSavedPath;

        public bool IsRecording => _isRecording;
        public float RecordingTime => _recordingTime;
        public int FramesCaptured => _framesCaptured;
        public string LastSavedPath => _lastSavedPath;

        void Start()
        {
            if (skeletonResolver == null)
                skeletonResolver = GetComponent<SkeletonResolver>();

            _captureInterval = 1f / targetFPS;
        }

        void Update()
        {
            if (!_isRecording) return;

            _recordingTime += Time.deltaTime;
            _timeSinceLastCapture += Time.deltaTime;

            if (_timeSinceLastCapture >= _captureInterval)
            {
                CaptureFrame();
                _timeSinceLastCapture = 0f;
            }
        }

        /// <summary>
        /// Start recording joint data. Call this when the patient is ready.
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording)
            {
                Debug.LogWarning("[Recorder] Already recording.");
                return;
            }

            if (skeletonResolver == null || !skeletonResolver.IsResolved)
            {
                Debug.LogError("[Recorder] SkeletonResolver not ready. Cannot start recording.");
                return;
            }

            _currentRecording = new MovementRecording
            {
                exerciseType = "squat",
                recordedAt = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
            };

            _recordingTime = 0f;
            _framesCaptured = 0;
            _timeSinceLastCapture = 0f;
            _isRecording = true;

            Debug.Log("[Recorder] Recording started.");
        }

        /// <summary>
        /// Stop recording and save to disk.
        /// Returns the file path where the recording was saved.
        /// </summary>
        public string StopRecording()
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[Recorder] Not currently recording.");
                return null;
            }

            _isRecording = false;

            _currentRecording.totalDuration = _recordingTime;
            _currentRecording.frameCount = _framesCaptured;

            string path = SaveRecording(_currentRecording);
            _lastSavedPath = path;

            Debug.Log($"[Recorder] Recording stopped. {_framesCaptured} frames over {_recordingTime:F1}s. Saved to: {path}");

            return path;
        }

        private void CaptureFrame()
        {
            FrameSnapshot frame = skeletonResolver.CaptureFrame(_recordingTime);
            _currentRecording.frames.Add(frame);
            _framesCaptured++;
        }

        /// <summary>
        /// Save the recording as a JSON file in the app's persistent data directory.
        /// On Quest 3 this is: /sdcard/Android/data/com.yourcompany.app/files/
        /// </summary>
        private string SaveRecording(MovementRecording recording)
        {
            string directory = Path.Combine(Application.persistentDataPath, "Recordings");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string filename = $"recording_{recording.recordedAt}.json";
            string fullPath = Path.Combine(directory, filename);

            string json = JsonUtility.ToJson(recording, true);
            File.WriteAllText(fullPath, json);

            return fullPath;
        }

        /// <summary>
        /// Load a recording from a JSON file path.
        /// </summary>
        public static MovementRecording LoadRecording(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[Recorder] File not found: {filePath}");
                return null;
            }

            string json = File.ReadAllText(filePath);
            return JsonUtility.FromJson<MovementRecording>(json);
        }

        /// <summary>
        /// Get a list of all saved recording file paths.
        /// </summary>
        public static string[] GetAllRecordings()
        {
            string directory = Path.Combine(Application.persistentDataPath, "Recordings");
            if (!Directory.Exists(directory))
                return new string[0];

            return Directory.GetFiles(directory, "recording_*.json");
        }
    }
}
