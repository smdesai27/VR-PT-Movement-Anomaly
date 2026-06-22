# VR-PT-Movement-Anomaly

Unity source for **PT Detective**, a VR tool for reviewing squat biomechanics on Meta
Quest. The headset records body motion, computes geometric biomechanics metrics, and
replays a severity-coded 3D skeleton that a reviewer can walk around and view from any
angle.

📄 **Write-up, results, and demo:** https://smdesai27.github.io/vr-movement-anomaly-review/
· **Project repo:** [vr-movement-anomaly-review](https://github.com/smdesai27/vr-movement-anomaly-review)

## Stack

- Unity 2022.3, built for Meta Quest (Quest 3 pilot; Quest 2 supported)
- Meta Movement SDK for body tracking and retargeting
- C# for logic, ShaderLab + HLSL for skeleton and severity rendering

## Authored work

Distinct from vendor and sample assets (Meta Movement SDK, TextMesh Pro):

- **Recording** — passthrough capture flow, joint-transform sampling at a 30 FPS target,
  JSON session persistence, and bone-name resolution across Unity Humanoid and Meta
  Movement SDK rigs.
- **Analysis** — five geometric metrics (knee and hip asymmetry, frontal-plane knee
  angle, trunk lateral lean, trunk forward lean), per-frame severity, and a 3-frame
  persistence filter.
- **PT review** — recording load and skeleton playback (play, pause, slow, step, rotate,
  walk-around), severity coloring, and accessibility beyond color (enlarge and white
  outline for severe joints).

## Repository layout

- `Assets/` — Unity project assets and authored scripts
- `Packages/`, `ProjectSettings/` — Unity package manifest and project settings
- `Final_PT_Detective_Poster.pdf` — project poster

## License

The original scripts and assets authored for this project are released under the MIT
License. Third-party packages (Meta Movement SDK, TextMesh Pro, and other Unity
packages) remain under their respective licenses.
