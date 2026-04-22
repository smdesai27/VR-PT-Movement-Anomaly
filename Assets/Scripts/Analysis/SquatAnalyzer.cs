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

            // Derive a stable sagittal-plane normal from frame 0 hip positions.
            // sagittalNormal = normalize(cross(up, rightHip - leftHip)) points forward
            // along the patient's sagittal axis and is used for FPPA and forward-lean.
            Vector3 sagittalNormal = Vector3.forward;
            if (recording.frames.Count > 0)
            {
                var f0 = BuildPositionMap(recording.frames[0]);
                if (f0.ContainsKey(SquatJoints.LeftHip) && f0.ContainsKey(SquatJoints.RightHip))
                {
                    Vector3 candidate = Vector3.Cross(
                        Vector3.up,
                        f0[SquatJoints.RightHip] - f0[SquatJoints.LeftHip]).normalized;
                    if (candidate.sqrMagnitude > 0.01f)
                        sagittalNormal = candidate;
                }
            }

            // --- Diagnostic 3: log sagittalNormal so we can verify it in logcat ---
            // Expected: near-horizontal unit vector (|Y| ≈ 0), magnitude ≈ 1.
            // For a patient facing +Z the result is (0,0,−1); facing +X gives (−1,0,0).
            Debug.Log($"[SquatAnalyzer] SagittalNormal: {sagittalNormal}  " +
                      $"(|Y|={Mathf.Abs(sagittalNormal.y):F3} should be ≈0, " +
                      $"mag={sagittalNormal.magnitude:F3} should be ≈1)");

            // --- Diagnostic 1 & 2: FPPA synthetic unit test ---
            // Validates ComputeFPPA math with known geometry before processing real data.
            // Positions are WORLD SPACE (SkeletonResolver.CaptureFrame stores bone.position,
            // which is Unity world-space regardless of the character's local scale/offset).
            // sagittalNormal = (0,0,−1) is what the formula produces for a patient facing +Z.
            {
                var sn       = new Vector3(0f, 0f, -1f);
                var hipPos   = new Vector3(0f, 1f, 0f);
                var anklePos = new Vector3(0f, 0f, 0f);

                float straight = JointAngleCalculator.ComputeFPPA(hipPos, new Vector3( 0f,   0.5f, 0f), anklePos, sn);
                float valgus   = JointAngleCalculator.ComputeFPPA(hipPos, new Vector3( 0.2f, 0.5f, 0f), anklePos, sn);
                float varus    = JointAngleCalculator.ComputeFPPA(hipPos, new Vector3(-0.2f, 0.5f, 0f), anklePos, sn);

                Debug.Log(
                    $"[SquatAnalyzer] FPPA unit test  (sagittalNormal={sn}):\n" +
                    $"  Straight leg (knee on axis):       {straight:F1}°  (expect ≈  0°)\n" +
                    $"  Valgus  (knee +0.20 m lateral):   {valgus:F1}°  (expect ≈+22.6°, positive)\n" +
                    $"  Varus   (knee -0.20 m lateral):   {varus:F1}°  (expect ≈-22.6°, negative)\n" +
                    $"  Positions: WORLD SPACE (bone.position from SkeletonResolver.CaptureFrame)");
            }

            float maxKneeAsym = 0f, sumKneeAsym = 0f;
            float maxHipAsym  = 0f, sumHipAsym  = 0f;
            float maxTrunkLateral = 0f;
            float maxTrunkForward = 0f;
            float maxFppaAbsL = 0f, maxFppaValgusL = 0f, maxFppaVarusL = 0f;
            float maxFppaAbsR = 0f, maxFppaValgusR = 0f, maxFppaVarusR = 0f;

            foreach (var frame in recording.frames)
            {
                var positions = BuildPositionMap(frame);

                // Existing bilateral asymmetry metrics
                AngleData kneeData = ComputeKneeAngles(frame.timestamp, positions);
                result.kneeAngles.Add(kneeData);

                AngleData hipData = ComputeHipAngles(frame.timestamp, positions);
                result.hipAngles.Add(hipData);

                float trunkLean = ComputeTrunkLean(positions);
                result.trunkLeanPerFrame.Add(trunkLean);
                result.trunkLateralLeanLevels.Add(JointAngleCalculator.ClassifyTrunkLean(trunkLean));

                // FPPA — dynamic knee valgus per side [Task 2c]
                float fppaL = 0f, fppaR = 0f;
                bool hasL = positions.ContainsKey(SquatJoints.LeftHip)  &&
                            positions.ContainsKey(SquatJoints.LeftKnee) &&
                            positions.ContainsKey(SquatJoints.LeftAnkle);
                bool hasR = positions.ContainsKey(SquatJoints.RightHip)  &&
                            positions.ContainsKey(SquatJoints.RightKnee) &&
                            positions.ContainsKey(SquatJoints.RightAnkle);

                if (hasL)
                    fppaL = JointAngleCalculator.ComputeFPPA(
                        positions[SquatJoints.LeftHip],
                        positions[SquatJoints.LeftKnee],
                        positions[SquatJoints.LeftAnkle],
                        sagittalNormal);

                if (hasR)
                    // Negate right side: SignedAngle returns opposite sign for valgus
                    // on the right leg with the same sagittalNormal. Negating restores
                    // the positive-valgus convention for both sides.
                    fppaR = -JointAngleCalculator.ComputeFPPA(
                        positions[SquatJoints.RightHip],
                        positions[SquatJoints.RightKnee],
                        positions[SquatJoints.RightAnkle],
                        sagittalNormal);

                result.fppaLeft.Add(fppaL);
                result.fppaRight.Add(fppaR);
                result.fppaLevelsLeft.Add(JointAngleCalculator.ClassifyFPPA(fppaL));
                result.fppaLevelsRight.Add(JointAngleCalculator.ClassifyFPPA(fppaR));

                // Trunk forward lean [Task 2d]
                float forwardLean = 0f;
                if (positions.ContainsKey(SquatJoints.Hips) && positions.ContainsKey(SquatJoints.Neck))
                    forwardLean = JointAngleCalculator.ComputeTrunkForwardLean(
                        positions[SquatJoints.Hips],
                        positions[SquatJoints.Neck],
                        sagittalNormal);
                result.trunkForwardLeanPerFrame.Add(forwardLean);
                result.trunkForwardLeanLevels.Add(JointAngleCalculator.ClassifyTrunkForwardLean(forwardLean));

                // Frame-level classification: worst across all active metrics
                AnomalyLevel frameLevel = JointAngleCalculator.WorstLevel(
                    kneeData.level,
                    hipData.level,
                    JointAngleCalculator.ClassifyTrunkLean(trunkLean),
                    JointAngleCalculator.ClassifyFPPA(fppaL),
                    JointAngleCalculator.ClassifyFPPA(fppaR),
                    JointAngleCalculator.ClassifyTrunkForwardLean(forwardLean)
                );
                result.frameLevels.Add(frameLevel);

                if (kneeData.asymmetry > maxKneeAsym) maxKneeAsym = kneeData.asymmetry;
                sumKneeAsym += kneeData.asymmetry;
                if (hipData.asymmetry  > maxHipAsym)  maxHipAsym  = hipData.asymmetry;
                sumHipAsym  += hipData.asymmetry;

                float absLateral = Mathf.Abs(trunkLean);
                if (absLateral > maxTrunkLateral) maxTrunkLateral = absLateral;
                if (Mathf.Abs(forwardLean) > maxTrunkForward) maxTrunkForward = Mathf.Abs(forwardLean);

                float absL = Mathf.Abs(fppaL), absR = Mathf.Abs(fppaR);
                if (absL > maxFppaAbsL) maxFppaAbsL = absL;
                if (absR > maxFppaAbsR) maxFppaAbsR = absR;
                if (fppaL > maxFppaValgusL) maxFppaValgusL = fppaL;
                if (-fppaL > maxFppaVarusL) maxFppaVarusL = -fppaL;
                if (fppaR > maxFppaValgusR) maxFppaValgusR = fppaR;
                if (-fppaR > maxFppaVarusR) maxFppaVarusR = -fppaR;
            }

            result.maxKneeAsymmetry = maxKneeAsym;
            result.maxHipAsymmetry  = maxHipAsym;

            // 3-frame persistence filter [Task 2b]: downgrade any Severe frame to Mild
            // unless 3 consecutive frames (including the current one, looking backward)
            // are all Severe in the raw data. 30 FPS → 3 frames ≈ 100 ms.
            // Snapshot frameLevels before filtering so we can count downgrades.
            var rawFrameLevels = new List<AnomalyLevel>(result.frameLevels);
            ApplyPersistenceFilter(result.frameLevels);
            ApplyPersistenceFilter(result.fppaLevelsLeft);
            ApplyPersistenceFilter(result.fppaLevelsRight);
            ApplyPersistenceFilter(result.trunkLateralLeanLevels);
            ApplyPersistenceFilter(result.trunkForwardLeanLevels);
            ApplyPersistenceFilterToAngleData(result.kneeAngles);
            ApplyPersistenceFilterToAngleData(result.hipAngles);

            int persistenceDowngrades = 0;
            for (int i = 0; i < rawFrameLevels.Count; i++)
                if (rawFrameLevels[i] == AnomalyLevel.Severe && result.frameLevels[i] == AnomalyLevel.Mild)
                    persistenceDowngrades++;

            // Re-count anomaly frames after filtering
            int anomalyCount = 0;
            foreach (var level in result.frameLevels)
                if (level != AnomalyLevel.Normal) anomalyCount++;
            result.anomalyFrames = anomalyCount;

            float n = result.totalFrames > 0 ? result.totalFrames : 1f;
            Debug.Log(
                $"[SquatAnalyzer] Analysis complete:\n" +
                $"  Frames: {result.totalFrames}, Anomaly frames: {result.anomalyFrames} ({(100f * result.anomalyFrames / n):F1}%)\n" +
                $"  Knee asymmetry:   max {result.maxKneeAsymmetry:F1}°, mean {(sumKneeAsym / n):F1}°\n" +
                $"  Hip asymmetry:    max {result.maxHipAsymmetry:F1}°, mean {(sumHipAsym / n):F1}°\n" +
                $"  FPPA left:        max |{maxFppaAbsL:F1}°|  (valgus max {maxFppaValgusL:F1}°, varus max {maxFppaVarusL:F1}°)\n" +
                $"  FPPA right:       max |{maxFppaAbsR:F1}°|  (valgus max {maxFppaValgusR:F1}°, varus max {maxFppaVarusR:F1}°)\n" +
                $"  Trunk lateral:    max {maxTrunkLateral:F1}°\n" +
                $"  Trunk forward:    max {maxTrunkForward:F1}°\n" +
                $"  Persistence filter: downgraded {persistenceDowngrades} severe frames to mild");

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

        private static Dictionary<string, Vector3> BuildPositionMap(FrameSnapshot frame)
        {
            var positions = new Dictionary<string, Vector3>();
            foreach (var joint in frame.joints)
                positions[joint.jointName] = joint.GetPosition();
            return positions;
        }

        /// <summary>
        /// 3-frame persistence filter. Downgrades a Severe entry to Mild unless the two
        /// preceding entries in the *raw* snapshot are also Severe. Operates on a snapshot
        /// of the original values to avoid cascade effects.
        /// </summary>
        private static void ApplyPersistenceFilter(List<AnomalyLevel> levels)
        {
            var raw = new List<AnomalyLevel>(levels);
            for (int i = 0; i < levels.Count; i++)
            {
                if (raw[i] == AnomalyLevel.Severe)
                {
                    bool persist = i >= 2 &&
                                   raw[i - 1] == AnomalyLevel.Severe &&
                                   raw[i - 2] == AnomalyLevel.Severe;
                    if (!persist)
                        levels[i] = AnomalyLevel.Mild;
                }
            }
        }

        private static void ApplyPersistenceFilterToAngleData(List<AngleData> angles)
        {
            // Snapshot raw levels before modifying
            var raw = new List<AnomalyLevel>(angles.Count);
            foreach (var a in angles) raw.Add(a.level);

            for (int i = 0; i < angles.Count; i++)
            {
                if (raw[i] == AnomalyLevel.Severe)
                {
                    bool persist = i >= 2 &&
                                   raw[i - 1] == AnomalyLevel.Severe &&
                                   raw[i - 2] == AnomalyLevel.Severe;
                    if (!persist)
                        angles[i].level = AnomalyLevel.Mild;
                }
            }
        }
    }
}
