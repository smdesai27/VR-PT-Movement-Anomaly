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
        /// Thresholds (6°/12°) derived from Nae et al., BMC Musculoskeletal Disorders (2017):
        /// valgus-positive group showed mean FPPA 11.6° vs 5.0° in the knee-over-foot group.
        /// 6° mild threshold ≈ 1 SD of healthy-population variability at Quest 3 IOBT resolution.
        /// </summary>
        public static AnomalyLevel ClassifyAsymmetry(float leftAngle, float rightAngle)
        {
            float asymmetry = Mathf.Abs(leftAngle - rightAngle);

            if (asymmetry < 6f)
                return AnomalyLevel.Normal;
            else if (asymmetry < 12f)
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

        // --- FPPA (Frontal Plane Projection Angle) — Task 2c ---

        /// <summary>
        /// Compute the Frontal Plane Projection Angle (FPPA) at the knee.
        /// Projects hip, knee, and ankle into the frontal plane (perpendicular to
        /// sagittalNormal) and returns the signed angle between the hip→knee and
        /// knee→ankle vectors measured around sagittalNormal.
        ///
        /// Sign convention for the LEFT knee: positive = valgus (knee medial to foot).
        /// For the RIGHT knee, negate the result before storing so both sides use
        /// the same positive-valgus convention. Classify on Mathf.Abs.
        ///
        /// sagittalNormal: unit vector pointing forward along the patient's sagittal
        /// axis, computed once from frame-0 hip positions as
        ///   normalize(cross(Vector3.up, rightHip - leftHip)).
        /// </summary>
        public static float ComputeFPPA(Vector3 hip, Vector3 knee, Vector3 ankle, Vector3 sagittalNormal)
        {
            Vector3 projHip   = ProjectOntoFrontalPlane(hip,   sagittalNormal);
            Vector3 projKnee  = ProjectOntoFrontalPlane(knee,  sagittalNormal);
            Vector3 projAnkle = ProjectOntoFrontalPlane(ankle, sagittalNormal);

            Vector3 hipToKnee   = projKnee  - projHip;
            Vector3 kneeToAnkle = projAnkle - projKnee;

            if (hipToKnee.sqrMagnitude < 0.0001f || kneeToAnkle.sqrMagnitude < 0.0001f)
                return 0f;

            return Vector3.SignedAngle(hipToKnee, kneeToAnkle, sagittalNormal);
        }

        /// <summary>
        /// Classify FPPA for dynamic knee valgus.
        /// Thresholds from Nae et al. 2017 and Sahabuddin et al. 2021.
        /// Normal: |FPPA| < 6°, Mild: 6–10°, Severe: ≥10°.
        /// </summary>
        public static AnomalyLevel ClassifyFPPA(float fppaDegrees)
        {
            float abs = Mathf.Abs(fppaDegrees);
            if (abs < 6f)  return AnomalyLevel.Normal;
            if (abs < 10f) return AnomalyLevel.Mild;
            return AnomalyLevel.Severe;
        }

        private static Vector3 ProjectOntoFrontalPlane(Vector3 point, Vector3 sagittalNormal)
        {
            // Remove the sagittal component, leaving the frontal-plane component.
            return point - Vector3.Dot(point, sagittalNormal) * sagittalNormal;
        }

        // --- Trunk forward lean — Task 2d ---

        /// <summary>
        /// Compute trunk forward lean: the angle (degrees) between the hip→neck vector
        /// and vertical, projected into the sagittal plane.
        /// Returns 0° when standing straight; grows as the trunk inclines forward.
        ///
        /// sagittalNormal: same forward-pointing unit vector used for FPPA.
        /// </summary>
        public static float ComputeTrunkForwardLean(Vector3 hips, Vector3 neck, Vector3 sagittalNormal)
        {
            Vector3 trunkVec = neck - hips;

            // Remove the lateral component to isolate the sagittal-plane projection.
            Vector3 lateralAxis = Vector3.Cross(Vector3.up, sagittalNormal).normalized;
            Vector3 sagittalProjection = trunkVec - Vector3.Dot(trunkVec, lateralAxis) * lateralAxis;

            if (sagittalProjection.sqrMagnitude < 0.0001f)
                return 0f;

            float forwardComponent  = Vector3.Dot(sagittalProjection, sagittalNormal);
            float verticalComponent = sagittalProjection.y;
            return Mathf.Atan2(Mathf.Abs(forwardComponent), verticalComponent) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Classify trunk forward lean severity.
        /// Threshold: Pandit (Int J Sports Phys Ther, 2023) cites >45° as excessive;
        /// 30–45° is mild compensation; <30° is normal.
        /// </summary>
        public static AnomalyLevel ClassifyTrunkForwardLean(float degrees)
        {
            float abs = Mathf.Abs(degrees);
            if (abs < 30f) return AnomalyLevel.Normal;
            if (abs < 45f) return AnomalyLevel.Mild;
            return AnomalyLevel.Severe;
        }
    }
}
