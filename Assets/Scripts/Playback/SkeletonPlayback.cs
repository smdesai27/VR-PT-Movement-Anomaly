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
        private Dictionary<string, GameObject> _jointSpheres  = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> _jointOutlines = new Dictionary<string, GameObject>(); // [Task 2e]
        private Dictionary<string, LineRenderer> _boneLines   = new Dictionary<string, LineRenderer>();
        private bool _isLoaded = false;

        // View rotation — driven by left thumbstick via PTReviewController. [Task 1]
        private float _viewYaw = 0f;

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
            if (!_isLoaded) return;

            if (_isPlaying)
            {
                _playbackTime += Time.deltaTime * _playbackSpeed;
                if (_playbackTime >= _recording.totalDuration)
                    _playbackTime = 0f; // Loop

                int targetFrame = FindFrameAtTime(_playbackTime);
                if (targetFrame != _currentFrame)
                    _currentFrame = targetFrame;
            }

            // Always redraw so rotation updates are visible at headset refresh rate,
            // both during active playback and while paused.
            ApplyFrame(_currentFrame);
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

            // Position joint spheres with offset + yaw rotation around the pivot. [Task 1]
            foreach (var joint in frame.joints)
            {
                if (_jointSpheres.TryGetValue(joint.jointName, out GameObject sphere))
                {
                    sphere.transform.position = ApplyYaw(joint.GetPosition() + _positionOffset);
                    sphere.transform.rotation = joint.GetRotation();
                }
            }

            // Update bone lines with the same offset + yaw. [Task 1]
            foreach (var connection in BoneConnections)
            {
                string key = connection[0] + "_" + connection[1];
                if (_boneLines.TryGetValue(key, out LineRenderer line))
                {
                    Vector3 startPos = Vector3.zero;
                    Vector3 endPos   = Vector3.zero;

                    foreach (var joint in frame.joints)
                    {
                        if (joint.jointName == connection[0])
                            startPos = ApplyYaw(joint.GetPosition() + _positionOffset);
                        if (joint.jointName == connection[1])
                            endPos = ApplyYaw(joint.GetPosition() + _positionOffset);
                    }

                    line.SetPosition(0, startPos);
                    line.SetPosition(1, endPos);
                }
            }

            // Color-code joints based on anomaly data.
            if (_analysis != null && frameIndex < _analysis.frameLevels.Count)
            {
                // Knee joints: worst of bilateral asymmetry and per-side FPPA. [Tasks 2a, 2c]
                if (frameIndex < _analysis.kneeAngles.Count)
                {
                    AnomalyLevel kneeAsym = _analysis.kneeAngles[frameIndex].level;
                    AnomalyLevel fppaL = frameIndex < _analysis.fppaLevelsLeft.Count
                        ? _analysis.fppaLevelsLeft[frameIndex]  : AnomalyLevel.Normal;
                    AnomalyLevel fppaR = frameIndex < _analysis.fppaLevelsRight.Count
                        ? _analysis.fppaLevelsRight[frameIndex] : AnomalyLevel.Normal;

                    ColorJoint(SquatJoints.LeftKnee,  JointAngleCalculator.WorstLevel(kneeAsym, fppaL));
                    ColorJoint(SquatJoints.RightKnee, JointAngleCalculator.WorstLevel(kneeAsym, fppaR));
                }

                // Hip joints: bilateral asymmetry only.
                if (frameIndex < _analysis.hipAngles.Count)
                {
                    AnomalyLevel hipLevel = _analysis.hipAngles[frameIndex].level;
                    ColorJoint(SquatJoints.LeftHip,  hipLevel);
                    ColorJoint(SquatJoints.RightHip, hipLevel);
                }

                // Trunk: worst of lateral lean and forward lean. [Task 2d]
                // Both use pre-computed, persistence-filtered level lists — not on-the-fly.
                if (frameIndex < _analysis.trunkLateralLeanLevels.Count)
                {
                    AnomalyLevel lateralLevel = _analysis.trunkLateralLeanLevels[frameIndex];
                    AnomalyLevel forwardLevel = frameIndex < _analysis.trunkForwardLeanLevels.Count
                        ? _analysis.trunkForwardLeanLevels[frameIndex] : AnomalyLevel.Normal;
                    AnomalyLevel trunkLevel = JointAngleCalculator.WorstLevel(lateralLevel, forwardLevel);
                    ColorJoint(SquatJoints.Hips,  trunkLevel);
                    ColorJoint(SquatJoints.Spine, trunkLevel);
                }

                // Remaining joints are diagnostic-neutral.
                ColorJoint(SquatJoints.LeftAnkle,     AnomalyLevel.Normal);
                ColorJoint(SquatJoints.RightAnkle,    AnomalyLevel.Normal);
                ColorJoint(SquatJoints.Neck,           AnomalyLevel.Normal);
                ColorJoint(SquatJoints.LeftShoulder,  AnomalyLevel.Normal);
                ColorJoint(SquatJoints.RightShoulder, AnomalyLevel.Normal);
            }
        }

        /// <summary>
        /// Rotate worldPos around the PlaybackSkeleton pivot by the current view yaw.
        /// Returns worldPos unchanged when _viewYaw == 0.
        /// </summary>
        private Vector3 ApplyYaw(Vector3 worldPos)
        {
            return transform.position + Quaternion.Euler(0, _viewYaw, 0) * (worldPos - transform.position);
        }

        // --- Playback controls ---

        public void Play() { _isPlaying = true; }
        public void Pause() { _isPlaying = false; }
        public void TogglePlayPause() { _isPlaying = !_isPlaying; }

        // --- View rotation (Task 1) ---
        // Update() calls ApplyFrame every frame, so rotation changes are picked up
        // automatically at headset refresh rate without extra ApplyFrame calls here.

        /// <summary>Add degrees to the skeleton's yaw rotation around the PlaybackSkeleton pivot.</summary>
        public void AddYaw(float degrees) { _viewYaw += degrees; }

        /// <summary>Reset view yaw to the original forward-facing orientation.</summary>
        public void ResetView() { _viewYaw = 0f; }

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
            _jointOutlines.Clear(); // children are destroyed with their parents above

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

                    var collider = sphere.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                }

                sphere.name = $"Joint_{jointName}";
                SetupMaterial(sphere, normalColor);
                _jointSpheres[jointName] = sphere;

                // Outline child sphere for severe-level accessibility. [Task 2e]
                // Rendered at 1.7× the joint sphere (in local space), white unlit.
                // Disabled by default; enabled only when the joint is Severe.
                GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                outline.name = "SevereOutline";
                outline.transform.SetParent(sphere.transform, false);
                outline.transform.localPosition = Vector3.zero;
                outline.transform.localScale    = Vector3.one * 1.7f;
                var outlineCollider = outline.GetComponent<Collider>();
                if (outlineCollider != null) Destroy(outlineCollider);
                var outlineRenderer = outline.GetComponent<Renderer>();
                if (outlineRenderer != null)
                {
                    outlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    outlineRenderer.material.color = Color.white;
                }
                outline.SetActive(false);
                _jointOutlines[jointName] = outline;
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

        /// <summary>
        /// Color a joint sphere by anomaly level, and apply severity-scaled size
        /// and white outline for colorblind accessibility. [Task 2e]
        /// Severe: 1.5× scale + outline enabled.
        /// Mild/Normal: base scale + outline disabled.
        /// </summary>
        private void ColorJoint(string jointName, AnomalyLevel level)
        {
            if (!_jointSpheres.TryGetValue(jointName, out GameObject sphere)) return;

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = GetColorForLevel(level);

            float baseScale = jointSphereRadius * 2f;
            bool isSevere   = level == AnomalyLevel.Severe;

            sphere.transform.localScale = isSevere
                ? Vector3.one * baseScale * 1.5f
                : Vector3.one * baseScale;

            if (_jointOutlines.TryGetValue(jointName, out GameObject outline))
                outline.SetActive(isSevere);
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
