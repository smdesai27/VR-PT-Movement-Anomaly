using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VRMovementTracker
{
    /// <summary>
    /// Controls the Patient Recording scene flow.
    /// 
    /// Scene setup:
    /// - OVRCameraRig with passthrough enabled
    /// - A body-tracked character (invisible mesh, just tracking data)
    ///   with SkeletonResolver + BodyTrackingRecorder
    /// - A world-space Canvas with status text
    /// 
    /// Flow:
    /// 1. "Press right trigger to start recording"
    /// 2. 3-2-1 countdown
    /// 3. "Recording... Perform 5 squats" (with timer)
    /// 4. Press trigger again OR auto-stop after maxDuration
    /// 5. "Recording saved! Hand headset to PT."
    /// </summary>
    public class PatientSceneController : MonoBehaviour
    {
        [Header("References")]
        public BodyTrackingRecorder recorder;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI timerText;

        [Header("Settings")]
        [Tooltip("Maximum recording duration in seconds before auto-stop.")]
        public float maxRecordingDuration = 60f;
        [Tooltip("Countdown seconds before recording starts.")]
        public int countdownSeconds = 3;

        private enum State { WaitingToStart, Countdown, Recording, Saved }
        private State _state = State.WaitingToStart;

        void Start()
        {
            SetStatus("Press RIGHT TRIGGER to start recording");
            if (timerText != null) timerText.text = "";
        }

        void Update()
        {
            // Check for right index trigger press (OVRInput)
            bool triggerPressed = false;

            // Try OVRInput first (Quest controllers)
            try
            {
                triggerPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            }
            catch
            {
                // Fallback: spacebar for editor testing
                triggerPressed = Input.GetKeyDown(KeyCode.Space);
            }

            switch (_state)
            {
                case State.WaitingToStart:
                    if (triggerPressed)
                        StartCoroutine(CountdownAndRecord());
                    break;

                case State.Recording:
                    // Update timer display
                    if (timerText != null)
                        timerText.text = $"{recorder.RecordingTime:F1}s  |  {recorder.FramesCaptured} frames";

                    // Stop on trigger press or max duration
                    if (triggerPressed || recorder.RecordingTime >= maxRecordingDuration)
                    {
                        StopAndSave();
                    }
                    break;

                case State.Saved:
                    // Could add "press trigger to record again" here
                    break;
            }
        }

        private IEnumerator CountdownAndRecord()
        {
            _state = State.Countdown;

            for (int i = countdownSeconds; i > 0; i--)
            {
                SetStatus($"Starting in {i}...");
                yield return new WaitForSeconds(1f);
            }

            SetStatus("RECORDING — Perform 5 squats\nPress RIGHT TRIGGER to stop");
            recorder.StartRecording();
            _state = State.Recording;
        }

        private void StopAndSave()
        {
            string path = recorder.StopRecording();
            _state = State.Saved;

            SetStatus($"Recording saved!\n{recorder.FramesCaptured} frames captured\n\nHand headset to PT for review.");
            if (timerText != null)
                timerText.text = $"Duration: {recorder.RecordingTime:F1}s";
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
            Debug.Log($"[PatientScene] {text}");
        }
    }
}
