# VR Movement Tracker — Project Context

This document is a handoff from a prior Claude.ai chat session to a fresh Claude Code session. It captures the project's state, architecture, recent work, and pending tasks so a new agent can pick up where we left off.

---

## Project Overview

**Course:** Brown University CSCI 1951T (Augmented Data Visualization for Research, Spring 2026)
**Project:** VR Movement Tracker ("PT Detective") — Project 2
**Concept:** A Meta Quest 3 VR application that records a patient performing squats using inside-out body tracking, then lets a physical therapist (PT) review the recorded movement in a second scene with color-coded joint anomaly highlights.

**Hardware:** Meta Quest 3
**Engine:** Unity 2022.3.62f3
**SDKs:** Meta Movement SDK (body tracking via Inside-Out Body Tracking / IOBT)
**Build target:** Android, deployed via `adb` over USB-C
**Dev machine:** macOS (Apple Silicon)

---

## Architecture

### Scenes

1. **`PatientRecording.unity`** — patient wears headset, presses right trigger to start/stop recording squats. Uses passthrough so patient can see the real room. Recording is saved as JSON to `Application.persistentDataPath/Recordings/recording_<timestamp>.json` (on Quest: `/storage/emulated/0/Android/data/com.Sanil.VRMovementTracker/files/Recordings/`).

2. **`PTReview.unity`** — no passthrough, virtual environment. The PT sees a color-coded playback skeleton rendered from the most recent recording. Scene has a `PlaybackSkeleton` GameObject with a `SkeletonPlayback` component that auto-loads the latest recording on `Start()` and renders joint spheres + bone lines.

**Scene switching:** handled by a `SceneSwitcher` component, triggered by **left grip + left trigger**.

### Scripts (Assets/Scripts/)

- **Core/**
  - `SkeletonResolver.cs` — maps SquatJoints constant names (Unity Humanoid convention: `LeftUpperLeg`, `LeftLowerLeg`, etc.) to the actual bone transforms on the `RealisticCharacter` rig (which uses Meta SDK convention: `LeftLegUpper`, `LeftLegLower`). Has a Humanoid-Animator fast path and a name-alias fallback.
  - `BodyTrackingRecorder.cs` — captures `FrameSnapshot`s at 30 FPS via `SkeletonResolver.CaptureFrame`, serializes a `MovementRecording` to JSON on stop.
  - `SquatJoints.cs` — string constants for 11 tracked joints (Hips, Spine, Neck, LeftHip, LeftKnee, LeftAnkle, RightHip, RightKnee, RightAnkle, LeftShoulder, RightShoulder).
- **Analysis/**
  - `SquatAnalyzer.cs` — computes per-frame knee-asymmetry, hip-asymmetry, and trunk-lateral-lean; classifies into `AnomalyLevel.Normal | Mild | Severe`.
  - `JointAngleCalculator.cs` — geometric helpers for knee angle, hip angle, trunk lean. Current thresholds: 10° mild, 20° severe.
- **Playback/**
  - `SkeletonPlayback.cs` — renders the recorded skeleton in the PT scene. Creates sphere primitives and LineRenderers at runtime. Recently updated to fix position anchoring and add view-yaw rotation (see "Recent Changes" below).
- **UI/**
  - `PatientSceneController.cs` — countdown + trigger handling for patient scene.
  - `PTReviewController.cs` — playback controls for PT scene (play/pause, step frames, speed).

### Key data classes

- `MovementRecording` — list of `FrameSnapshot`s + metadata (exerciseType, recordedAt, totalDuration, frameCount).
- `FrameSnapshot` — timestamp + list of `JointSnapshot`s.
- `JointSnapshot` — jointName + serialized position (Vector3) + rotation (Quaternion). Has `GetPosition()` and `GetRotation()` accessors.
- `SquatAnalysisResult` — frame-level anomaly classifications, kneeAngles, hipAngles, trunkLeanPerFrame, frameLevels, summary stats.
- `AnomalyLevel` enum: Normal, Mild, Severe.

### Character rig

The patient character `RealisticCharacter` in the Patient scene has:
- A Meta Movement SDK `CharacterRetargeter` (maps body tracking data to rig bones)
- A `MetaSourceDataProvider` (body tracking source)
- `SkeletonResolver` + `BodyTrackingRecorder` (our custom components)
- Transform position is (0.003, -2.80, -0.031) with scale (4.39, 4.39, 4.54). This unusual transform is part of the sample character's prefab — it affects recorded joint world positions, which is why playback needs an explicit offset to render at viewable height.
- Bone names use Meta SDK convention: `LeftLegUpper`, `LeftLegLower`, `LeftArmUpper`, `SpineMiddle`, etc. — NOT Unity Humanoid convention.

---

## Recent Changes (most recent first — these are the fixes that got playback working)

### 1. Skeleton rendering at correct world position — `SkeletonPlayback.cs`
The recorded joint positions were in the patient character's world space, which put them ~2m below the floor in the PT scene. Fix: on `LoadRecording`, compute `_positionOffset` such that the hip joint from frame 0 renders at approximately 1m above the `PlaybackSkeleton` GameObject's position (typical standing hip height). Every joint and bone endpoint in `ApplyFrame` now has this offset added.

### 2. Bone name alias support — `SkeletonResolver.cs`
The rig uses Meta SDK naming (`LeftLegUpper`) but `SquatJoints` constants are Unity Humanoid names (`LeftUpperLeg`). Added `GetBoneNameAliases(string jointName)` that returns multiple name variants per joint (Unity Humanoid, Meta SDK, Mixamo). Fallback search tries exact-match across all aliases first, then substring match, which prevents false matches like "LeftLeg" matching "LeftLegUpper" when the target is "LeftLegLower".

### 3. View rotation support — `SkeletonPlayback.cs`
Added `_viewYaw` field, `AddYaw(float degrees)` public method, and `ResetView()` public method. `ApplyFrame` now applies `Quaternion.Euler(0, _viewYaw, 0)` rotation around the `PlaybackSkeleton` transform pivot to every joint and bone endpoint. `Update()` now calls `ApplyFrame(_currentFrame)` every frame (not just on frame change) so rotation updates are visible during pause.

### 4. Visibility improvements — `SkeletonPlayback.cs`
Default joint sphere radius bumped from 0.03 to 0.06. Bone line width bumped from 0.01 to 0.02. Colors made fully opaque (alpha 1.0).

### 5. Debug logging — `SkeletonPlayback.cs`
Added log lines on recording load that print the recorded hip position, target position, computed offset, and where the first joint will render. Also logs every bone mapping in `SkeletonResolver` (useful when verifying via `adb logcat -s Unity`).

---

## Known Issues (not blocking, but worth flagging)

### `CharacterRetargeter` errors during patient recording
`adb logcat` shows `Failed to retarget source frame data!` flooding at ~70Hz during the patient scene. This means the Meta Movement SDK retargeter isn't successfully driving the character's bones from body tracking data. The recording still captures SOMETHING (joint asymmetries of 5-10° are reported), but the data may reflect the character's near-static rest pose rather than the patient's real body motion. The fix is to run the Meta Movement SDK's retargeting editor wizard on the `RealisticCharacter` prefab (the Building Block instructions reference this but it wasn't completed).

---

## Build & Test Workflow

From Unity on Mac:
1. File → Build and Run (pushes APK to Quest via USB-C, ~1-2 min)
2. Put on headset, app auto-launches
3. In separate Mac terminal: `adb logcat -c && adb logcat -s Unity` to watch Unity logs
4. Test flow: patient scene → right trigger → 3-second countdown → record squats → right trigger to stop → left grip + left trigger to switch to PT scene

Package name: `com.Sanil.VRMovementTracker`

---

## Current Control Scheme

| Input | Action |
|---|---|
| Right A button | Play / Pause |
| Right B button | Cycle playback speed (1x / 0.5x / 0.25x) |
| Right thumbstick L/R | Step ±1 frame |
| Left thumbstick L/R | Rotate skeleton yaw (recently added — may not yet be wired into PTReviewController) |
| Left X button | Reset view rotation (recently added) |
| Physical walking | PT walks around in room-scale to view from different angles |
| Left grip + left trigger | Switch scenes |

**Note:** The SkeletonPlayback.cs file now exposes `AddYaw()` and `ResetView()` public methods, but these are not yet called from PTReviewController. This is part of the pending work (see next section).

---

## Pending Work

Two tasks to implement. See `HANDOFF_PROMPT.md` for the full implementation prompt.

1. **Wire up left-stick rotation controls in `PTReviewController.cs`** — call the existing `SkeletonPlayback.AddYaw()` and `ResetView()` methods from the left thumbstick and X button.
2. **Anomaly detection improvements** — tighten thresholds, add Frontal Plane Projection Angle (FPPA) for dynamic knee valgus detection, add trunk forward-lean, add 3-frame persistence filter, and improve visual accessibility (scale severe joints, add outlines).

Both tasks are described in detail in the companion file `Project2_AnomalyDetection_Research.md` which should be in the project root alongside this file.
