using System.Collections.Generic;
using UnityEngine;

namespace VRMovementTracker
{
    /// <summary>
    /// Replays a recorded movement and visualizes anomalies on a skeleton.
    /// 
    /// Setup: Attach to a humanoid character in the PT Review scene.
    /// The character should have:
    /// - A SkeletonResolver component
    /// - Joint indicator spheres as children of each tracked bone (created at runtime or as prefabs)
    /// 
    /// The PT can walk around the skeleton, pause, and scrub through the recording.
    /// </summary>
    public class SkeletonPlayback : MonoBehaviour
    {
        [Header("References")]
        public SkeletonResolver skeletonResolver;

        [Header("Joint Visualization")]
        [Tooltip("Prefab for joint indicator spheres. If null, creates simple spheres at runtime.")]
        public GameObject jointIndicatorPrefab;
        [Tooltip("Size of joint indicator spheres.")]
        public float jointSphereRadius = 0.06f;

        [Header("Colors")]
        public Color normalColor = new Color(0.2f, 0.9f, 0.3f, 1.0f);   // Green
        public Color mildColor = new Color(1.0f, 0.85f, 0.1f, 1.0f);    // Yellow
        public Color severeColor = new Color(1.0f, 0.15f, 0.15f, 1.0f);  // Red
        public Color boneColor = new Color(0.9f, 0.9f, 0.9f, 1.0f);     // Light gray for bone lines

        [Header("Playback State")]
        [SerializeField] private bool _isPlaying = false;
        [SerializeField] private float _playbackTime = 0f;
        [SerializeField] private int _currentFrame = 0;
        [SerializeField] private float _playbackSpeed = 1f;

        private MovementRecording _recording;
        private SquatAnalysisResult _analysis;
        private Dictionary<string, GameObject> _jointSpheres = new Dictionary<string, GameObject>();
        private Dictionary<string, LineRenderer> _boneLines = new Dictionary<string, LineRenderer>();
        private bool _isLoaded = false;

        // Offset applied to every recorded joint position so the skeleton renders
        // at this GameObject's transform position (rather than at the patient's
        // world coordinates from when the recording was made).
        private Vector3 _positionOffset = Vector3.zero;

        // Bone connections to draw lines between
        private static readonly string[][] BoneConnections = new string[][]
        {
            new[] { SquatJoints.LeftHip, SquatJoints.LeftKnee },
            new[] { SquatJoints.LeftKnee, SquatJoints.LeftAnkle },
            new[] { SquatJoints.RightHip, SquatJoints.RightKnee },
            new[] { SquatJoints.RightKnee, SquatJoints.RightAnkle },
            new[] { SquatJoints.Hips, SquatJoints.Spine },
            new[] { SquatJoints.Spine, SquatJoints.Neck },
            new[] { SquatJoints.LeftHip, SquatJoints.Hips },
            new[] { SquatJoints.RightHip, SquatJoints.Hips },
            new[] { SquatJoints.LeftShoulder, SquatJoints.Neck },
            new[] { SquatJoints.RightShoulder, SquatJoints.Neck },
        };

        public bool IsPlaying => _isPlaying;
        public float PlaybackTime => _playbackTime;
        public int CurrentFrame => _currentFrame;
        public int TotalFrames => _recording?.frames.Count ?? 0;
        public float TotalDuration => _recording?.totalDuration ?? 0f;
        public SquatAnalysisResult Analysis => _analysis;

        /// <summary>
        /// Load a recording and its analysis, create visual indicators.
        /// </summary>
        public void LoadRecording(MovementRecording recording, SquatAnalysisResult analysis)
        {
            _recording = recording;
            _analysis = analysis;
            _playbackTime = 0f;
            _currentFrame = 0;
            _isPlaying = false;

            // Compute XZ offset so the skeleton renders at this GameObject's position.
            // Keep the recorded Y intact so vertical motion (squats) is preserved.
            _positionOffset = Vector3.zero;
            if (recording.frames.Count > 0)
            {
                Vector3 hipPos = Vector3.zero;
                bool foundHip = false;
                foreach (var joint in recording.frames[0].joints)
                {
                    if (joint.jointName == SquatJoints.Hips)
                    {
                        hipPos = joint.GetPosition();
                        foundHip = true;
                        break;
                    }
                }

                if (foundHip)
                {
                    // Place hip at ~1m above the PlaybackSkeleton GameObject (roughly
                    // standing hip-height for a ~1.7m human). Full 3D offset so the
                    // skeleton lands in front of the PT at normal viewing height,
                    // regardless of where the patient's character was in the recording
                    // scene. Relative vertical motion (squats) is preserved because all
                    // frames get the same constant offset.
                    const float hipTargetHeight = 1.0f;
                    _positionOffset = new Vector3(
                        transform.position.x - hipPos.x,
                        (transform.position.y + hipTargetHeight) - hipPos.y,
                        transform.position.z - hipPos.z
                    );
                    Debug.Log($"[Playback] Recorded hip at frame 0: {hipPos}. " +
                              $"Target position: {transform.position}. " +
                              $"Offset applied: {_positionOffset}. " +
                              $"Hip will render at: {hipPos + _positionOffset}");
                }
                else
                {
                    Debug.LogWarning("[Playback] No Hips joint in recording. Using zero offset.");
                }

                // Log a sanity check: where will the joints render this frame?
                var sampleJoints = recording.frames[0].joints;
                Debug.Log($"[Playback] Frame 0 has {sampleJoints.Count} joints. " +
                          $"First joint '{sampleJoints[0].jointName}' recorded at {sampleJoints[0].GetPosition()}, " +
                          $"will render at {sampleJoints[0].GetPosition() + _positionOffset}.");
            }

            CreateJointVisuals();
            ApplyFrame(0);

            _isLoaded = true;
            Debug.Log($"[Playback] Loaded recording: {recording.frameCount} frames, {recording.totalDuration:F1}s");
        }

        void Update()
        {
            if (!_isLoaded || !_isPlaying) return;

            _playbackTime += Time.deltaTime * _playbackSpeed;

            if (_playbackTime >= _recording.totalDuration)
            {
                _playbackTime = 0f; // Loop
            }

            // Find the frame closest to current playback time
            int targetFrame = FindFrameAtTime(_playbackTime);
            if (targetFrame != _currentFrame)
            {
                _currentFrame = targetFrame;
                ApplyFrame(_currentFrame);
            }
        }

        /// <summary>
        /// Apply a specific frame's joint positions to the visual skeleton,
        /// and color-code based on analysis results.
        /// </summary>
        public void ApplyFrame(int frameIndex)
        {
            if (_recording == null || frameIndex < 0 || frameIndex >= _recording.frames.Count)
                return;

            FrameSnapshot frame = _recording.frames[frameIndex];

            // Position joint spheres (apply offset to anchor to PlaybackSkeleton's transform)
            foreach (var joint in frame.joints)
            {
                if (_jointSpheres.TryGetValue(joint.jointName, out GameObject sphere))
                {
                    sphere.transform.position = joint.GetPosition() + _positionOffset;
                    sphere.transform.rotation = joint.GetRotation();
                }
            }

            // Update bone lines (also offset)
            foreach (var connection in BoneConnections)
            {
                string key = connection[0] + "_" + connection[1];
                if (_boneLines.TryGetValue(key, out LineRenderer line))
                {
                    Vector3 startPos = Vector3.zero;
                    Vector3 endPos = Vector3.zero;

                    foreach (var joint in frame.joints)
                    {
                        if (joint.jointName == connection[0]) startPos = joint.GetPosition() + _positionOffset;
                        if (joint.jointName == connection[1]) endPos = joint.GetPosition() + _positionOffset;
                    }

                    line.SetPosition(0, startPos);
                    line.SetPosition(1, endPos);
                }
            }

            // Color-code joints based on anomaly data
            if (_analysis != null && frameIndex < _analysis.frameLevels.Count)
            {
                AnomalyLevel frameLevel = _analysis.frameLevels[frameIndex];
                Color frameColor = GetColorForLevel(frameLevel);

                // Color knee joints individually based on their specific asymmetry
                if (frameIndex < _analysis.kneeAngles.Count)
                {
                    var kneeData = _analysis.kneeAngles[frameIndex];
                    ColorJoint(SquatJoints.LeftKnee, GetColorForLevel(kneeData.level));
                    ColorJoint(SquatJoints.RightKnee, GetColorForLevel(kneeData.level));
                }

                if (frameIndex < _analysis.hipAngles.Count)
                {
                    var hipData = _analysis.hipAngles[frameIndex];
                    ColorJoint(SquatJoints.LeftHip, GetColorForLevel(hipData.level));
                    ColorJoint(SquatJoints.RightHip, GetColorForLevel(hipData.level));
                }

                // Trunk lean indicator on spine/hips
                if (frameIndex < _analysis.trunkLeanPerFrame.Count)
                {
                    float lean = _analysis.trunkLeanPerFrame[frameIndex];
                    AnomalyLevel leanLevel = JointAngleCalculator.ClassifyTrunkLean(lean);
                    ColorJoint(SquatJoints.Hips, GetColorForLevel(leanLevel));
                    ColorJoint(SquatJoints.Spine, GetColorForLevel(leanLevel));
                }

                // Remaining joints get the default frame-level color
                ColorJoint(SquatJoints.LeftAnkle, normalColor);
                ColorJoint(SquatJoints.RightAnkle, normalColor);
                ColorJoint(SquatJoints.Neck, normalColor);
                ColorJoint(SquatJoints.LeftShoulder, normalColor);
                ColorJoint(SquatJoints.RightShoulder, normalColor);
            }
        }

        // --- Playback controls ---

        public void Play() { _isPlaying = true; }
        public void Pause() { _isPlaying = false; }
        public void TogglePlayPause() { _isPlaying = !_isPlaying; }

        public void SetPlaybackSpeed(float speed) { _playbackSpeed = speed; }

        public void SeekToFrame(int frame)
        {
            _currentFrame = Mathf.Clamp(frame, 0, _recording.frames.Count - 1);
            _playbackTime = _recording.frames[_currentFrame].timestamp;
            ApplyFrame(_currentFrame);
        }

        public void StepForward()
        {
            _isPlaying = false;
            SeekToFrame(_currentFrame + 1);
        }

        public void StepBackward()
        {
            _isPlaying = false;
            SeekToFrame(_currentFrame - 1);
        }

        // --- Visual creation ---

        private void CreateJointVisuals()
        {
            // Clear existing
            foreach (var sphere in _jointSpheres.Values)
                if (sphere != null) Destroy(sphere);
            _jointSpheres.Clear();

            foreach (var line in _boneLines.Values)
                if (line != null) Destroy(line.gameObject);
            _boneLines.Clear();

            // Create joint spheres
            foreach (string jointName in SquatJoints.AllTrackedJoints)
            {
                GameObject sphere;
                if (jointIndicatorPrefab != null)
                {
                    sphere = Instantiate(jointIndicatorPrefab, transform);
                }
                else
                {
                    sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.SetParent(transform);
                    sphere.transform.localScale = Vector3.one * jointSphereRadius * 2f;

                    // Remove collider (we don't need physics on indicators)
                    var collider = sphere.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                }

                sphere.name = $"Joint_{jointName}";
                SetupMaterial(sphere, normalColor);
                _jointSpheres[jointName] = sphere;
            }

            // Create bone lines
            foreach (var connection in BoneConnections)
            {
                string key = connection[0] + "_" + connection[1];
                GameObject lineObj = new GameObject($"Bone_{key}");
                lineObj.transform.SetParent(transform);

                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.02f;
                lr.endWidth = 0.02f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = boneColor;
                lr.endColor = boneColor;
                lr.useWorldSpace = true;

                _boneLines[key] = lr;
            }
        }

        private void ColorJoint(string jointName, Color color)
        {
            if (_jointSpheres.TryGetValue(jointName, out GameObject sphere))
            {
                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = color;
            }
        }

        private Color GetColorForLevel(AnomalyLevel level)
        {
            switch (level)
            {
                case AnomalyLevel.Mild: return mildColor;
                case AnomalyLevel.Severe: return severeColor;
                default: return normalColor;
            }
        }

        private void SetupMaterial(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Use unlit shader so colors are visible without lighting setup
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.material.color = color;
            }
        }

        private int FindFrameAtTime(float time)
        {
            if (_recording == null || _recording.frames.Count == 0)
                return 0;

            // Binary search for closest frame
            int lo = 0, hi = _recording.frames.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_recording.frames[mid].timestamp < time)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }
}
