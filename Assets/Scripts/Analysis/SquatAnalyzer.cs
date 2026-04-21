using System.Collections.Generic;
using UnityEngine;

namespace VRMovementTracker
{
    /// <summary>
    /// Analyzes a MovementRecording to compute joint angles and detect asymmetries.
    /// This runs after recording is complete — it processes the saved data.
    /// </summary>
    public static class SquatAnalyzer
    {
        /// <summary>
        /// Run full analysis on a recording. Returns per-frame angle data
        /// and anomaly classifications.
        /// </summary>
        public static SquatAnalysisResult Analyze(MovementRecording recording)
        {
            var result = new SquatAnalysisResult();
            result.totalFrames = recording.frames.Count;

            float maxKneeAsym = 0f;
            float maxHipAsym = 0f;
            int anomalyCount = 0;

            foreach (var frame in recording.frames)
            {
                // Build a position lookup for this frame
                var positions = new Dictionary<string, Vector3>();
                foreach (var joint in frame.joints)
                {
                    positions[joint.jointName] = joint.GetPosition();
                }

                // Compute knee angles
                AngleData kneeData = ComputeKneeAngles(frame.timestamp, positions);
                result.kneeAngles.Add(kneeData);

                // Compute hip angles
                AngleData hipData = ComputeHipAngles(frame.timestamp, positions);
                result.hipAngles.Add(hipData);

                // Compute trunk lean
                float trunkLean = ComputeTrunkLean(positions);
                result.trunkLeanPerFrame.Add(trunkLean);

                // Classify this frame's overall anomaly level
                AnomalyLevel frameLevel = JointAngleCalculator.WorstLevel(
                    kneeData.level,
                    hipData.level,
                    JointAngleCalculator.ClassifyTrunkLean(trunkLean)
                );
                result.frameLevels.Add(frameLevel);

                if (frameLevel != AnomalyLevel.Normal)
                    anomalyCount++;

                if (kneeData.asymmetry > maxKneeAsym)
                    maxKneeAsym = kneeData.asymmetry;
                if (hipData.asymmetry > maxHipAsym)
                    maxHipAsym = hipData.asymmetry;
            }

            result.anomalyFrames = anomalyCount;
            result.maxKneeAsymmetry = maxKneeAsym;
            result.maxHipAsymmetry = maxHipAsym;

            Debug.Log($"[SquatAnalyzer] Analysis complete: {result.totalFrames} frames, " +
                      $"{result.anomalyFrames} anomaly frames ({(100f * result.anomalyFrames / result.totalFrames):F1}%), " +
                      $"max knee asymmetry: {result.maxKneeAsymmetry:F1}°, max hip asymmetry: {result.maxHipAsymmetry:F1}°");

            return result;
        }

        private static AngleData ComputeKneeAngles(float timestamp, Dictionary<string, Vector3> positions)
        {
            var data = new AngleData { timestamp = timestamp };

            bool hasLeft = positions.ContainsKey(SquatJoints.LeftHip) &&
                           positions.ContainsKey(SquatJoints.LeftKnee) &&
                           positions.ContainsKey(SquatJoints.LeftAnkle);

            bool hasRight = positions.ContainsKey(SquatJoints.RightHip) &&
                            positions.ContainsKey(SquatJoints.RightKnee) &&
                            positions.ContainsKey(SquatJoints.RightAnkle);

            if (hasLeft)
            {
                data.leftAngle = JointAngleCalculator.ComputeKneeAngle(
                    positions[SquatJoints.LeftHip],
                    positions[SquatJoints.LeftKnee],
                    positions[SquatJoints.LeftAnkle]);
            }

            if (hasRight)
            {
                data.rightAngle = JointAngleCalculator.ComputeKneeAngle(
                    positions[SquatJoints.RightHip],
                    positions[SquatJoints.RightKnee],
                    positions[SquatJoints.RightAnkle]);
            }

            if (hasLeft && hasRight)
            {
                data.asymmetry = Mathf.Abs(data.leftAngle - data.rightAngle);
                data.level = JointAngleCalculator.ClassifyAsymmetry(data.leftAngle, data.rightAngle);
            }

            return data;
        }

        private static AngleData ComputeHipAngles(float timestamp, Dictionary<string, Vector3> positions)
        {
            var data = new AngleData { timestamp = timestamp };

            bool hasLeft = positions.ContainsKey(SquatJoints.LeftShoulder) &&
                           positions.ContainsKey(SquatJoints.LeftHip) &&
                           positions.ContainsKey(SquatJoints.LeftKnee);

            bool hasRight = positions.ContainsKey(SquatJoints.RightShoulder) &&
                            positions.ContainsKey(SquatJoints.RightHip) &&
                            positions.ContainsKey(SquatJoints.RightKnee);

            if (hasLeft)
            {
                data.leftAngle = JointAngleCalculator.ComputeHipAngle(
                    positions[SquatJoints.LeftShoulder],
                    positions[SquatJoints.LeftHip],
                    positions[SquatJoints.LeftKnee]);
            }

            if (hasRight)
            {
                data.rightAngle = JointAngleCalculator.ComputeHipAngle(
                    positions[SquatJoints.RightShoulder],
                    positions[SquatJoints.RightHip],
                    positions[SquatJoints.RightKnee]);
            }

            if (hasLeft && hasRight)
            {
                data.asymmetry = Mathf.Abs(data.leftAngle - data.rightAngle);
                data.level = JointAngleCalculator.ClassifyAsymmetry(data.leftAngle, data.rightAngle);
            }

            return data;
        }

        private static float ComputeTrunkLean(Dictionary<string, Vector3> positions)
        {
            if (positions.ContainsKey(SquatJoints.Hips) && positions.ContainsKey(SquatJoints.Neck))
            {
                return JointAngleCalculator.ComputeTrunkLateralLean(
                    positions[SquatJoints.Hips],
                    positions[SquatJoints.Neck]);
            }
            return 0f;
        }
    }
}
