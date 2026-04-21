# Movement Anomaly Detection: Methodology & Thresholds

*Project 2 — VR Movement Tracker (PT Detective)*
*Research writeup for Spring 2026*

This document reviews the scientific basis for the anomaly-detection thresholds currently used in the VR Movement Tracker, compares them against the published literature on squat biomechanics and movement-quality assessment, and proposes an extensible architecture for future exercises.

---

## 1. Current Implementation

The system currently classifies each recorded frame into three anomaly levels — **normal** (green), **mild** (yellow), **severe** (red) — based on three metrics computed from the body-tracked skeleton:

| Metric | Computation | Thresholds (current) |
|---|---|---|
| Knee asymmetry | \|left knee angle − right knee angle\| | ≥10° mild, ≥20° severe |
| Hip asymmetry | \|left hip angle − right hip angle\| | ≥10° mild, ≥20° severe |
| Trunk lateral lean | Angle of hips→neck vector from vertical in the frontal plane | ≥10° mild, ≥20° severe |

The color-coded joint spheres in the PT Review scene are driven by these classifications. From the most recent test recording the analyzer reports `max knee asymmetry: 9.8°, max hip asymmetry: 10.4°` over 137 frames of bodyweight squats.

**Strengths of the current approach.** Per-frame, per-joint color coding is visually intuitive, the metrics are all derivable from standard skeletal data (no force plates or EMG required), and the three-level classification maps cleanly onto clinical "fine / watch this / intervene" reasoning.

**Weaknesses that this document addresses.**
1. The 10° / 20° thresholds were chosen heuristically and have no published citation. Section 3 derives evidence-based values.
2. The three metrics we compute are a small subset of the established squat-movement-quality literature. Section 2 identifies what is missing.
3. The system uses fixed thresholds per metric regardless of the individual patient's baseline. Section 4 discusses why this matters and how to extend toward a normative-data approach.
4. There is no architectural separation between "what is being measured" and "how thresholds are applied," making extension to other exercises painful. Section 5 proposes a refactor.

---

## 2. Validated Metrics from the Literature

The gold-standard frameworks for assessing squat movement quality identify a larger metric set than our current three. The most-cited are the Functional Movement Screen (FMS) deep-squat scoring, the NASM Overhead Squat Assessment, and 3D motion-capture studies of dynamic knee valgus.

### 2.1 Dynamic Knee Valgus (DKV) — the highest-value metric we are missing

Dynamic knee valgus is the inward collapse of the knees during weight-bearing knee flexion. It is the single most-studied squat-anomaly metric because it is strongly linked to non-contact ACL injury, patellofemoral pain syndrome, and chronic knee pathology.

DKV is typically quantified via the **Frontal Plane Projection Angle (FPPA)** — the 2D angle formed at the knee by the hip→knee and knee→ankle vectors projected into the frontal plane (the plane parallel to the patient's body facing forward).

In a validation study of single-limb mini-squats, participants whose knee drifted medial to the foot showed a mean FPPA of **11.6° valgus versus 5.0°** in the knee-over-foot group (Nae et al., *BMC Musculoskeletal Disorders*, 2017). A separate study of female athletes used FPPA thresholds during drop-landing to stratify patients into "normal DKV" and "excessive DKV" cohorts, confirming that ~2D FPPA tracks closely with 3D motion-capture knee-abduction angle in symptomatic populations (Sahabuddin et al., *Int J Sports Phys Ther*, 2021).

**Clinical interpretation:** an FPPA of roughly 0–6° is considered normal alignment; 6–10° is borderline; >10° is excessive valgus and warrants investigation.

**Implementation note:** computing FPPA requires the hip, knee, and ankle 3D positions per side, which we already capture. The only additional work is projecting the three-point angle into the frontal plane (the plane orthogonal to the patient's sagittal axis — trivially, the plane defined by the line between the two hips and the world vertical). This should be the next metric added to `SquatAnalyzer`.

### 2.2 Trunk Forward Lean

Excessive forward lean during a squat is a documented compensation pattern indicating weak hip extensors, tight hip flexors, or limited ankle dorsiflexion (Pandit, *Int J Sports Phys Ther*, 2023). Our current system measures lateral lean but not sagittal forward lean.

In the NASM Overhead Squat Assessment, the key observation is whether the torso inclines forward more than the tibia does. The ratio of trunk angle to tibia angle (both measured from vertical) is stable in well-executed squats; a ratio substantially greater than ~1.0 indicates hip-dominant compensation and potential kinetic-chain dysfunction.

**Implementation note:** compute trunk angle from vertical using the hip→neck vector in the sagittal plane. Normative data is less consistent here than for DKV, but a trunk inclination >45° from vertical at the bottom of a squat is frequently cited as a threshold for concern.

### 2.3 Bilateral Asymmetry (our current approach — corrected thresholds)

Our knee and hip asymmetry metrics are well-established. The most-cited threshold framework is the **Limb Symmetry Index (LSI)**, computed as `(weaker limb / stronger limb) × 100%`. The canonical clinical threshold from ACL-reconstruction return-to-sport literature is LSI ≥ 90% (i.e. asymmetry ≤ 10%) as a minimum clearance criterion, with LSI ≥ 85% historically used in older protocols.

Recent work has sharpened these numbers:

- **10% asymmetry** in countermovement-jump peak force predicts hamstring injury in professional soccer players with 86% sensitivity (Bishop et al., *J Strength Cond Res*, 2021).
- **Asymmetry >15%** in lower-limb joint extension moments during squats is the most-cited "injury risk threshold" across multiple studies (Sato & Heise, *J Strength Cond Res*, 2012; Helme et al., 2019).
- Wellsandt et al. (*JOSPT*, 2017) demonstrated that LSI can *overestimate* function because the "uninjured" reference limb may be deconditioned — meaning a 10% asymmetry is a **lower bound** on true deficit, not an upper bound on acceptable function.

**However: these thresholds are for strength/force asymmetry, not joint-angle asymmetry during a bodyweight movement.** Our metric (instantaneous joint-angle difference during a squat) is measuring something narrower. In bodyweight squats without external load, healthy individuals show small joint-angle asymmetries that are noise-dominated at the resolution of inside-out body tracking.

**Recommended thresholds for joint-angle asymmetry** (bodyweight squat, Quest 3 IOBT body tracking):

| Level | Knee asymmetry | Hip asymmetry | Reasoning |
|---|---|---|---|
| Normal (green) | <6° | <6° | ~1 SD of healthy-population variability |
| Mild (yellow) | 6°–12° | 6°–12° | 1–2 SD; clinically noteworthy |
| Severe (red) | ≥12° | ≥12° | >2 SD; consistent with published DKV/asymmetry thresholds |

The 12° severe threshold aligns with the 11.6° valgus-positive figure from Nae et al. The 6° mild threshold aligns with the difference between knee-over-foot and knee-medial-to-foot groups in the same study. Both are conservative enough to respect Quest 3 IOBT noise (~2° joint-angle RMSE under favorable conditions per Meta Movement SDK documentation) while being tight enough to flag real deviation.

### 2.4 Squat Depth and Temporal Symmetry (secondary metrics)

Two additional metrics are worth noting even if not implemented in this iteration:

- **Squat depth symmetry** — difference in minimum hip height between left-side-dominant and right-side-dominant reps in a multi-rep recording. Mismatches here indicate weight-shift compensation that can be missed by instantaneous-angle metrics.
- **Descent/ascent time asymmetry** — the eccentric (descent) and concentric (ascent) phases of a symmetric squat should have similar durations. A notably faster descent or ascent on one side suggests asymmetric load tolerance.

Both can be computed post-hoc from the existing recording data and would be worth adding once FPPA is in place.

---

## 3. Threshold Derivation: A Defensible Framework

The current 10°/20° thresholds are not wrong, but they are not *defensible* in the sense that a reviewer or instructor could ask "why these numbers?" and get a cited answer. Three approaches to threshold setting are used in the literature:

**(a) Absolute thresholds from clinical studies.** Pick numbers from validated populations (e.g. the 11.6° FPPA from Nae et al.). Pro: simple, cites well. Con: populations differ (age, sex, training status), so one number rarely transfers cleanly.

**(b) Normative-data Z-scores.** Collect N healthy baseline recordings, compute per-metric mean and SD, classify as mild (|z|>2, ~95th percentile) or severe (|z|>3, ~99.7th percentile). Pro: individualizes to the measurement system and population. Con: requires baseline data collection before the study runs.

**(c) Within-subject paired comparison.** Each patient's "good" side or "good" rep serves as their own reference; deviations are measured against self. Pro: bypasses population-normative-data problem. Con: doesn't work when the patient is impaired bilaterally.

**Recommendation for Project 2:** use **approach (a)** with the cited thresholds in Section 2.3 for the in-class activity, and flag in the results-writeup that moving to **approach (b)** — collecting ~8–10 baseline recordings from unaffected classmates and deriving Z-score thresholds — would be the natural next-iteration improvement. Modified Z-scores using median absolute deviation (MAD) rather than mean/SD should be used if the baseline sample is small (n<20), as MAD-based scores are robust to outliers that would otherwise skew a small sample's SD (Iglewicz & Hoaglin, 1993; Leys et al., 2013).

---

## 4. Visualization — What the Literature Says About PT-Facing Feedback

Our current "green/yellow/red joint sphere" scheme is a reasonable default but is only one of several studied approaches. The systematic review by Høeg et al. (*BioMedical Engineering Online*, 2017) categorizes movement visualization in VR rehabilitation into four types: abstract symbolic, realistic avatar, instructive overlay, and biofeedback-augmented. Color-coded skeletons fall into the "instructive overlay" category.

Evidence-based design principles from this literature:

**Use unambiguous, high-saturation colors.** Red-yellow-green maps onto universal "traffic light" semantics and doesn't require a legend. Good.

**Couple the color with a non-color cue for accessibility.** ~8% of males have some form of red-green colorblindness. Adding a secondary signal — pulsing, size change, or an outline ring — makes the classification robust. **Specific recommendation:** scale the severe-red spheres up by 1.5× and add a thin white outline; leave mild-yellow at default size.

**Show the source metric, not just the classification.** A PT will trust the system more if the "why" is surfaced. Hovering the controller at a red joint should display the underlying number ("Right knee: 14.2° valgus"). This is straightforward to add: on controller-ray intersection with a joint sphere, read the per-frame analysis data and show it in a small world-space label.

**Time-series visualization is underused in VR but valuable.** A small "sparkline" panel showing knee-angle-over-time for both legs, with the current frame highlighted, gives the PT temporal context that a single-frame color can't. Consider adding this to the DataPanel as a future iteration.

**Avoid over-alerting.** Every frame classified as "severe" dilutes the signal. A common convention in clinical motion-analysis software is to require the anomaly to persist for at least ~100ms (3+ frames at 30Hz) before flagging. The current implementation classifies per-frame and can produce single-frame red flashes that are sensor noise. Adding a 3-frame persistence requirement would improve signal quality without code complexity.

---

## 5. Proposed Architecture for Multi-Exercise Extensibility

The current `SquatAnalyzer` computes squat-specific metrics inline. To extend to other exercises (deadlifts, lunges, step-ups) without a rewrite, the following separation of concerns is proposed:

```
IMovementAnalyzer (interface)
├── SquatAnalyzer          — computes squat metrics
├── LungeAnalyzer          — computes lunge metrics (future)
└── DeadliftAnalyzer       — future

IAnomalyClassifier (interface)
├── FixedThresholdClassifier     — current approach with cited values
├── ZScoreClassifier             — baseline-relative (future)
└── ModifiedZScoreClassifier     — MAD-based for small samples (future)

MovementMetric (data class)
├── name: string
├── value: float
├── side: {Left, Right, Bilateral}
├── anomalyLevel: {Normal, Mild, Severe}
└── thresholdSource: string   — for auditability
```

With this structure, the PT Review scene's visualization code doesn't care what exercise it is — it just renders whatever `MovementMetric` list comes back. This is the minimum refactor needed to make Project 2 a reusable framework rather than a squat-only one-off.

For this iteration, the refactor can be deferred; it is mentioned here so that the wiki captures the architectural debt and the path out of it.

---

## 6. Summary of Recommended Changes

In approximate priority order:

1. **Add Frontal Plane Projection Angle (FPPA) computation per knee.** This is the highest-value missing metric and brings the system into alignment with published DKV literature. Threshold: normal <6°, mild 6–10°, severe >10°.
2. **Tighten asymmetry thresholds** to 6°/12° with Section 2.3 citations.
3. **Add 3-frame persistence filtering** before classifying as severe.
4. **Add trunk forward-lean metric** alongside the existing lateral-lean.
5. **Size-scale severe joints and add outline** for colorblind accessibility.
6. **Surface the underlying number on controller hover** for PT trust/transparency.
7. **Collect ~8 baseline recordings from classmates** to enable Z-score / MAD-based thresholding in a future iteration.
8. **Refactor** `SquatAnalyzer` behind an `IMovementAnalyzer` interface for future exercises (deferred).

---

## References

Bishop, C., Turner, A., & Read, P. (2018). Effects of inter-limb asymmetries on physical and sports performance: a systematic review. *Journal of Sports Sciences*, 36(10), 1135–1144.

Helme, M., Tee, J., Emmonds, S., & Low, C. (2021). Does lower-limb asymmetry increase injury risk in sport? A systematic review. *Physical Therapy in Sport*, 49, 204–213.

Høeg, E. R., Povlsen, T. M., Bruun-Pedersen, J. R., Lange, B., Nilsson, N. C., Haugstvedt, K. B., Birch, S., Brandt, J., & Serafin, S. (2021). System Immersion in Virtual Reality-Based Rehabilitation of Motor Function in Older Adults: A Systematic Review and Meta-Analysis. *Frontiers in Virtual Reality*.

Iglewicz, B., & Hoaglin, D. C. (1993). *How to detect and handle outliers*. ASQC Quality Press.

Leys, C., Ley, C., Klein, O., Bernard, P., & Licata, L. (2013). Detecting outliers: Do not use standard deviation around the mean, use absolute deviation around the median. *Journal of Experimental Social Psychology*, 49(4), 764–766.

Nae, J., Creaby, M. W., Nilsson, G., Crossley, K. M., & Ageberg, E. (2017). Measurement properties of a test battery to assess postural orientation during functional tasks in patients undergoing anterior cruciate ligament injury rehabilitation. *Journal of Orthopaedic & Sports Physical Therapy*, 47(11), 863–873.

Pandit, R. K. (2023). A biomechanical review of the squat exercise: implications for clinical practice. *International Journal of Sports Physical Therapy*, 19(4), 529–540.

Sahabuddin, F. N. A., Jamaludin, N. I., Amir, N. H., & Shaharudin, S. (2021). The concurrent validity and reliability of single-leg squat among physically active females with and without dynamic knee valgus. *International Journal of Sports Physical Therapy*, 16(5), 1248–1258.

Sato, K., & Heise, G. D. (2012). Influence of weight distribution asymmetry on the biomechanics of a barbell back squat. *Journal of Strength and Conditioning Research*, 26(2), 342–349.

Wellsandt, E., Failla, M. J., & Snyder-Mackler, L. (2017). Limb symmetry indexes can overestimate knee function after anterior cruciate ligament injury. *Journal of Orthopaedic & Sports Physical Therapy*, 47(5), 334–338.
