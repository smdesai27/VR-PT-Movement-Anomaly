using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRMovementTracker
{
    /// <summary>
    /// Serializable joint data for a single joint at a single frame.
    /// </summary>
    [Serializable]
    public class JointSnapshot
    {
        public string jointName;
        public float[] position; // x, y, z
        public float[] rotation; // x, y, z, w (quaternion)

        public JointSnapshot() { }

        public JointSnapshot(string name, Vector3 pos, Quaternion rot)
        {
            jointName = name;
            position = new float[] { pos.x, pos.y, pos.z };
            rotation = new float[] { rot.x, rot.y, rot.z, rot.w };
        }

        public Vector3 GetPosition()
        {
            return new Vector3(position[0], position[1], position[2]);
        }

        public Quaternion GetRotation()
        {
            return new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
        }
    }

    /// <summary>
    /// All joints captured at a single point in time.
    /// </summary>
    [Serializable]
    public class FrameSnapshot
    {
        public float timestamp;
        public List<JointSnapshot> joints = new List<JointSnapshot>();
    }

    /// <summary>
    /// A complete recording session.
    /// </summary>
    [Serializable]
    public class MovementRecording
    {
        public string exerciseType = "squat";
        public string recordedAt;
        public float totalDuration;
        public int frameCount;
        public List<FrameSnapshot> frames = new List<FrameSnapshot>();
    }

    /// <summary>
    /// Per-frame analysis results for a single joint angle.
    /// </summary>
    [Serializable]
    public class AngleData
    {
        public float timestamp;
        public float leftAngle;
        public float rightAngle;
        public float asymmetry; // absolute difference
        public AnomalyLevel level;
    }

    /// <summary>
    /// Full analysis results for a recording.
    /// </summary>
    [Serializable]
    public class SquatAnalysisResult
    {
        public List<AngleData> kneeAngles = new List<AngleData>();
        public List<AngleData> hipAngles = new List<AngleData>();
        public List<float> trunkLeanPerFrame = new List<float>();
        public List<AnomalyLevel> frameLevels = new List<AnomalyLevel>();

        // FPPA (Frontal Plane Projection Angle) — dynamic knee valgus per side. [Task 2c]
        // Sign convention: positive = valgus for both sides. Classify on Mathf.Abs.
        public List<float> fppaLeft = new List<float>();
        public List<float> fppaRight = new List<float>();
        public List<AnomalyLevel> fppaLevelsLeft = new List<AnomalyLevel>();
        public List<AnomalyLevel> fppaLevelsRight = new List<AnomalyLevel>();

        // Trunk forward lean in the sagittal plane. [Task 2d]
        public List<float> trunkForwardLeanPerFrame = new List<float>();
        public List<AnomalyLevel> trunkForwardLeanLevels = new List<AnomalyLevel>();

        // Trunk lateral lean levels — stored so the persistence filter can be applied.
        // Computed from trunkLeanPerFrame via ClassifyTrunkLean; use these in playback
        // instead of calling ClassifyTrunkLean() per-frame so filtering is respected.
        public List<AnomalyLevel> trunkLateralLeanLevels = new List<AnomalyLevel>();

        public int totalFrames;
        public int anomalyFrames;
        public float maxKneeAsymmetry;
        public float maxHipAsymmetry;
    }

    public enum AnomalyLevel
    {
        Normal,   // < 6 degrees (tightened per Nae et al. 2017)
        Mild,     // 6-12 degrees
        Severe    // >= 12 degrees
    }

    /// <summary>
    /// Maps the tracked skeleton bones we care about for squat analysis.
    /// These names must match the bone names on your retargeted character.
    /// Unity Humanoid rig uses standard names; we map to those.
    /// </summary>
    public static class SquatJoints
    {
        // Humanoid bone names (Unity Mecanim standard)
        public const string LeftHip = "LeftUpperLeg";
        public const string LeftKnee = "LeftLowerLeg";
        public const string LeftAnkle = "LeftFoot";
        public const string RightHip = "RightUpperLeg";
        public const string RightKnee = "RightLowerLeg";
        public const string RightAnkle = "RightFoot";
        public const string LeftShoulder = "LeftUpperArm";
        public const string RightShoulder = "RightUpperArm";
        public const string Hips = "Hips";
        public const string Spine = "Spine";
        public const string Neck = "Neck";

        /// <summary>
        /// All joint names we need to record for squat analysis.
        /// </summary>
        public static readonly string[] AllTrackedJoints = new string[]
        {
            LeftHip, LeftKnee, LeftAnkle,
            RightHip, RightKnee, RightAnkle,
            LeftShoulder, RightShoulder,
            Hips, Spine, Neck
        };
    }
}
