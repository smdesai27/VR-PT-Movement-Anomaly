using System.Collections.Generic;
using UnityEngine;

namespace VRMovementTracker
{
    /// <summary>
    /// Resolves bone transforms on a Unity Humanoid character.
    /// Attach this to the root of your retargeted body-tracking character.
    /// It searches the hierarchy for bones matching the names in SquatJoints.
    /// </summary>
    public class SkeletonResolver : MonoBehaviour
    {
        [Tooltip("If empty, will search this GameObject's children for bones by name.")]
        public Animator targetAnimator;

        private Dictionary<string, Transform> _boneMap = new Dictionary<string, Transform>();
        private bool _resolved = false;

        void Awake()
        {
            ResolveBones();
        }

        /// <summary>
        /// Search the character hierarchy and map bone names to transforms.
        /// Tries Animator.GetBoneTransform first (most reliable for Humanoid rigs),
        /// then falls back to recursive name search with multi-convention aliases.
        /// </summary>
        public void ResolveBones()
        {
            _boneMap.Clear();

            if (targetAnimator == null)
                targetAnimator = GetComponentInChildren<Animator>();

            if (targetAnimator != null && targetAnimator.isHuman)
            {
                // Use Mecanim humanoid bone mapping (most reliable)
                MapHumanoidBone(SquatJoints.Hips, HumanBodyBones.Hips);
                MapHumanoidBone(SquatJoints.Spine, HumanBodyBones.Spine);
                MapHumanoidBone(SquatJoints.Neck, HumanBodyBones.Neck);
                MapHumanoidBone(SquatJoints.LeftHip, HumanBodyBones.LeftUpperLeg);
                MapHumanoidBone(SquatJoints.LeftKnee, HumanBodyBones.LeftLowerLeg);
                MapHumanoidBone(SquatJoints.LeftAnkle, HumanBodyBones.LeftFoot);
                MapHumanoidBone(SquatJoints.RightHip, HumanBodyBones.RightUpperLeg);
                MapHumanoidBone(SquatJoints.RightKnee, HumanBodyBones.RightLowerLeg);
                MapHumanoidBone(SquatJoints.RightAnkle, HumanBodyBones.RightFoot);
                MapHumanoidBone(SquatJoints.LeftShoulder, HumanBodyBones.LeftUpperArm);
                MapHumanoidBone(SquatJoints.RightShoulder, HumanBodyBones.RightUpperArm);
            }
            else
            {
                // Fallback: search hierarchy by name, trying multiple naming conventions.
                // Supports Unity Humanoid, Meta Movement SDK, and Mixamo rigs.
                Debug.LogWarning("[SkeletonResolver] No Humanoid Animator found. Falling back to name search.");
                foreach (string jointName in SquatJoints.AllTrackedJoints)
                {
                    Transform bone = FindBoneByAliases(jointName);
                    if (bone != null)
                    {
                        _boneMap[jointName] = bone;
                        Debug.Log($"[SkeletonResolver] Mapped {jointName} -> {bone.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"[SkeletonResolver] Could not find bone: {jointName}");
                    }
                }
            }

            _resolved = true;
            Debug.Log($"[SkeletonResolver] Resolved {_boneMap.Count}/{SquatJoints.AllTrackedJoints.Length} bones.");
        }

        private void MapHumanoidBone(string key, HumanBodyBones humanBone)
        {
            Transform t = targetAnimator.GetBoneTransform(humanBone);
            if (t != null)
                _boneMap[key] = t;
            else
                Debug.LogWarning($"[SkeletonResolver] Humanoid bone not found: {humanBone}");
        }

        /// <summary>
        /// Try each alias for a SquatJoints key. Returns the first exact-name match found.
        /// Prefers exact name equality over substring to avoid e.g. "LeftLeg" matching "LeftLegUpper"
        /// when we really wanted "LeftLegLower".
        /// </summary>
        private Transform FindBoneByAliases(string jointName)
        {
            string[] aliases = GetBoneNameAliases(jointName);

            // First pass: exact name match (most reliable)
            foreach (string alias in aliases)
            {
                Transform bone = FindBoneExact(transform, alias);
                if (bone != null) return bone;
            }

            // Second pass: substring match (fallback, broader)
            foreach (string alias in aliases)
            {
                Transform bone = FindBoneRecursive(transform, alias);
                if (bone != null) return bone;
            }

            return null;
        }

        /// <summary>
        /// Returns all known name variants for a given SquatJoints key,
        /// covering Unity Humanoid, Meta Movement SDK, and Mixamo rigs.
        /// </summary>
        private static string[] GetBoneNameAliases(string jointName)
        {
            // Meta Movement SDK uses segment-first naming: LegUpper, ArmUpper, etc.
            // Unity Humanoid uses qualifier-first: UpperLeg, UpperArm, etc.
            // Mixamo uses mixamorig: prefix and UpLeg/Arm naming.
            switch (jointName)
            {
                case SquatJoints.Hips:
                    return new[] { "Hips", "mixamorig:Hips", "Pelvis", "Root" };
                case SquatJoints.Spine:
                    return new[] { "SpineMiddle", "Spine1", "Spine", "SpineLower", "mixamorig:Spine" };
                case SquatJoints.Neck:
                    return new[] { "Neck", "mixamorig:Neck", "HeadTop", "Head" };

                case SquatJoints.LeftHip:
                    return new[] { "LeftLegUpper", "LeftUpperLeg", "LeftUpLeg", "mixamorig:LeftUpLeg", "L_UpperLeg" };
                case SquatJoints.LeftKnee:
                    return new[] { "LeftLegLower", "LeftLowerLeg", "mixamorig:LeftLeg", "L_LowerLeg", "LeftLeg" };
                case SquatJoints.LeftAnkle:
                    return new[] { "LeftFoot", "LeftFootAnkle", "LeftAnkle", "mixamorig:LeftFoot", "L_Foot" };

                case SquatJoints.RightHip:
                    return new[] { "RightLegUpper", "RightUpperLeg", "RightUpLeg", "mixamorig:RightUpLeg", "R_UpperLeg" };
                case SquatJoints.RightKnee:
                    return new[] { "RightLegLower", "RightLowerLeg", "mixamorig:RightLeg", "R_LowerLeg", "RightLeg" };
                case SquatJoints.RightAnkle:
                    return new[] { "RightFoot", "RightFootAnkle", "RightAnkle", "mixamorig:RightFoot", "R_Foot" };

                case SquatJoints.LeftShoulder:
                    return new[] { "LeftArmUpper", "LeftUpperArm", "mixamorig:LeftArm", "L_UpperArm", "LeftShoulder" };
                case SquatJoints.RightShoulder:
                    return new[] { "RightArmUpper", "RightUpperArm", "mixamorig:RightArm", "R_UpperArm", "RightShoulder" };

                default:
                    return new[] { jointName };
            }
        }

        /// <summary>
        /// Find a child whose name is EXACTLY equal to the target.
        /// </summary>
        private Transform FindBoneExact(Transform parent, string boneName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == boneName)
                    return child;
                Transform found = FindBoneExact(child, boneName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(boneName))
                    return child;
                Transform found = FindBoneRecursive(child, boneName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Get the world-space position of a named joint. Returns Vector3.zero if not found.
        /// </summary>
        public Vector3 GetJointPosition(string jointName)
        {
            if (_boneMap.TryGetValue(jointName, out Transform bone))
                return bone.position;
            return Vector3.zero;
        }

        /// <summary>
        /// Get the world-space rotation of a named joint.
        /// </summary>
        public Quaternion GetJointRotation(string jointName)
        {
            if (_boneMap.TryGetValue(jointName, out Transform bone))
                return bone.rotation;
            return Quaternion.identity;
        }

        /// <summary>
        /// Get the Transform for a named joint. Returns null if not found.
        /// </summary>
        public Transform GetJointTransform(string jointName)
        {
            _boneMap.TryGetValue(jointName, out Transform bone);
            return bone;
        }

        /// <summary>
        /// Capture a snapshot of all tracked joints at the current frame.
        /// </summary>
        public FrameSnapshot CaptureFrame(float timestamp)
        {
            var frame = new FrameSnapshot { timestamp = timestamp };

            foreach (string jointName in SquatJoints.AllTrackedJoints)
            {
                if (_boneMap.TryGetValue(jointName, out Transform bone))
                {
                    frame.joints.Add(new JointSnapshot(jointName, bone.position, bone.rotation));
                }
            }

            return frame;
        }

        public bool IsResolved => _resolved && _boneMap.Count > 0;
    }
}
