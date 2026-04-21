using UnityEngine;

namespace VRMovementTracker
{
    /// <summary>
    /// Computes angles between joint triplets using Vector3 math.
    /// The angle is measured at the middle joint (vertex).
    /// Example: for knee angle, pass hip, knee, ankle — returns the angle at the knee.
    /// </summary>
    public static class JointAngleCalculator
    {
        /// <summary>
        /// Compute the angle (in degrees) at joint B, formed by points A-B-C.
        /// B is the vertex of the angle.
        /// Returns 180 when the limb is fully extended (straight line).
        /// Returns smaller values as the joint bends.
        /// </summary>
        public static float ComputeAngle(Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {
            Vector3 boneBA = (pointA - pointB).normalized;
            Vector3 boneBC = (pointC - pointB).normalized;

            if (boneBA.sqrMagnitude < 0.0001f || boneBC.sqrMagnitude < 0.0001f)
                return 0f;

            float dot = Vector3.Dot(boneBA, boneBC);
            dot = Mathf.Clamp(dot, -1f, 1f);
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Compute knee flexion angle.
        /// 180 = fully extended, ~90 = deep squat.
        /// </summary>
        public static float ComputeKneeAngle(Vector3 hip, Vector3 knee, Vector3 ankle)
        {
            return ComputeAngle(hip, knee, ankle);
        }

        /// <summary>
        /// Compute hip flexion angle.
        /// 180 = standing straight, decreases as you hinge forward.
        /// </summary>
        public static float ComputeHipAngle(Vector3 shoulder, Vector3 hip, Vector3 knee)
        {
            return ComputeAngle(shoulder, hip, knee);
        }

        /// <summary>
        /// Compute lateral trunk lean by measuring the horizontal offset
        /// of the spine/neck midpoint relative to the hips midpoint.
        /// Positive = leaning right, negative = leaning left (from the skeleton's perspective).
        /// Returns value in degrees of lean.
        /// </summary>
        public static float ComputeTrunkLateralLean(Vector3 hips, Vector3 neck)
        {
            Vector3 trunkVector = neck - hips;
            // Project onto the frontal plane (X-Y from skeleton's perspective)
            // Lean angle = atan2(horizontal offset, vertical height)
            float lateralOffset = trunkVector.x;
            float verticalHeight = trunkVector.y;

            if (Mathf.Abs(verticalHeight) < 0.01f)
                return 0f;

            return Mathf.Atan2(lateralOffset, verticalHeight) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Determine anomaly level based on the absolute asymmetry between
        /// left and right side angles.
        /// </summary>
        public static AnomalyLevel ClassifyAsymmetry(float leftAngle, float rightAngle)
        {
            float asymmetry = Mathf.Abs(leftAngle - rightAngle);

            if (asymmetry < 10f)
                return AnomalyLevel.Normal;
            else if (asymmetry < 20f)
                return AnomalyLevel.Mild;
            else
                return AnomalyLevel.Severe;
        }

        /// <summary>
        /// Classify trunk lean severity.
        /// </summary>
        public static AnomalyLevel ClassifyTrunkLean(float leanDegrees)
        {
            float absLean = Mathf.Abs(leanDegrees);

            if (absLean < 5f)
                return AnomalyLevel.Normal;
            else if (absLean < 12f)
                return AnomalyLevel.Mild;
            else
                return AnomalyLevel.Severe;
        }

        /// <summary>
        /// Get the worst (most severe) anomaly level from multiple checks.
        /// </summary>
        public static AnomalyLevel WorstLevel(params AnomalyLevel[] levels)
        {
            AnomalyLevel worst = AnomalyLevel.Normal;
            foreach (var level in levels)
            {
                if (level > worst)
                    worst = level;
            }
            return worst;
        }
    }
}
