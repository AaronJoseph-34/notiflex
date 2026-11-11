# Notiflex notification placement and delivery rules

Version 0.3 | November 11 2026 | Technical specification and experiment manual

Notiflex chooses a visual notification location by evaluating interruption, occlusion, and social disengagement costs, then chooses a delivery channel separately. This specification defines the implemented rules, their units, their precedence, and their limitations. It is intended for the project investigator, implementers, and experiment operators. The implementation uses head orientation as a proxy for attention and never requires eye tracking.

The supplied Notiflex paper establishes the three cost dimensions, normalization to a common 0–1 scale, context and urgency dependent weights, separate modality selection, and continuous adaptation. The paper does not provide fitted equations, exact weights, classifier thresholds, or reliability estimates. The numerical choices below are auditable research hypotheses. Passing software tests establishes consistency with these rules, not that the rules predict human interruption or social engagement accurately.

## 1 Scope and architecture

The active Unity project contains Desk, Kitchen, LivingRoom, and Passthrough scenes. The first three are controlled simulations. Their objects and protected regions are authored ground truth. They provide varied geometry and camera views for testing the spatial algorithm. They are not evidence of successful recognition from camera pixels.

The Passthrough scene is the real-room target. The OS composites live surroundings behind stereo world-space notifications through Meta XR. Task and partner positions are supplied explicitly through controller annotations. The app does not automatically recognize arbitrary objects, conversations, faces, workload, or hazards in the live camera feed. No microphone-based conversational inference or eye tracking is implemented.

The architecture separates PlacementPolicy (deterministic context and channel rules), SpatialEvaluator (candidate geometry and costs), ContextDwell (temporal filtering), NotificationView (world-space rendering), NotiflexRuntime (input and lifecycle integration), and EnvironmentBindings/ProtectedRegion (scene annotations). PlatformBuilder authors scenes and configures pinned SDKs. Checks provides policy tests, a spatial sweep, and offscreen evidence rendering.

The optimization is exhaustive over a small finite candidate set. This finds its exact weighted minimum without an iterative nonlinear optimizer or a learned black box. It does not prove an optimum over every possible position in continuous space. Temporal stability can deliberately retain an incumbent that is slightly more costly than the instantaneous minimum.

## 2 Coordinate systems and input contract

R01 All world positions and dimensions use Unity meters. Vertical is world up. Head forward, right, and up come from the center-head camera transform. Angles are unsigned degrees in [0,180]. The projected viewport uses normalized coordinates with bottom-left origin. Eye directions and fixation points are not measured.

R02 ContextSnapshot supplies poseValid, age in seconds, workload in [0,1], movement in [0,1], taskAngle, partnerAngle, handsBusy, conversation, audioEnabled, speaking, and the active activity label. The runtime queries XR head validity and isTracked on device; the desktop camera is explicitly simulated. The age field is zero for the current synchronous runtime sample. Adapters that supply asynchronous observations must set the actual age.

R03 A snapshot older than 2 seconds, with a negative or nonfinite age, or with invalid pose, cannot support delivery. Activity inference returns Unknown and the delivery channel becomes Deferred. Existing visible content is hidden on the next evaluation. The nominal evaluation interval is 0.1 seconds; display rendering continues between evaluations. A hardware test must measure actual tracking-loss response latency.

R04 Task and partner angles are the angles between head forward and the vector from the head to the respective anchor. A missing anchor produces 180 degrees, not a fabricated location. Regions are explicit oriented rectangles with world dimensions. Their corners are calculated from their transforms, not detected in camera imagery.

R05 The current experiment runtime supplies activity states through an operator/controller mode. Focus supplies low movement (0.04), workload 0.8, free hands, and no conversation. Physical supplies movement 0.65, workload 0.8, occupied hands, and no conversation. Conversation supplies a conversation signal when a partner anchor exists. Unknown supplies workload 0.1 and conservative defaults. Head alignment still participates in inference. These presets must be labeled experiment ground truth, not automatic activity recognition. The userSpeaking field can be supplied by an experiment controller; it defaults false and is not speech detection.

## 3 Context inference and stability

R06 Evaluate context evidence in priority order. First, conversation=true and partnerAngle<65 selects Conversation. Second, handsBusy=true and movement>=0.25 selects Physical. Third, taskAngle<=24, movement<0.25, and workload>=0.30 selects Focus. Otherwise select Unknown. Invalid/stale observations take precedence over every activity rule. Room names never directly determine activity.

R07 A proposed activity must remain unchanged for 1.2 seconds before ContextDwell replaces the active label. A different proposal restarts the dwell. Brief oscillations therefore do not immediately change the active context. The runtime resets dwell after loading an environment and forces a new placement optimization when the active activity changes.

R08 The filter does not delay pose invalidation. Even if the active activity label still says Focus or Conversation during its dwell, the delivery layer independently checks current pose validity and age. There are no probability or model-confidence claims: these are explicit deterministic rules.

## 4 Candidate generation

R09 For candidate distance d, define scale s = max(1, d/1.8). The scored full visual envelope is (0.62*s) m wide and (0.29*s) m high, centered at the candidate and aligned with head camera rotation. Its corners use camera right/up. The full panel uses a 620 by 290 canvas at world scale 0.001*s. The cue uses a 330 by 130 canvas at that same scale, a conservative subset of the scored envelope. Distance compensation preserves apparent size along a fixed viewing direction beyond 1.8 m; nearer content retains minimum world size. Viewport, coverage and collider checks all use the scaled full envelope. Headset readability still requires device evaluation.

R10 Generate candidates in stable order. View center is head-local (0,0,1.8). View left is (-0.85,0.08,1.8). View right is (0.85,0.08,1.8). View upper is (0,0.55,1.8). These positions are re-evaluated from the current head transform.

R11 If a referent/task anchor exists, generate two candidates. Referent left is anchor position minus 0.65 times head right plus 0.35 times head up, then 0.30 m toward the head along the anchor direction. Referent right substitutes plus 0.65 times head right. These offsets are world positions calculated from the current relative geometry; they are not persistent spatial anchors or object-tracking estimates.

R12 When a partner anchor exists and the conversation signal is true, generate partner left/right at plus or minus 0.60 times head right, plus 0.35 times head up, and 0.35 m toward the head. The signal, not merely the active filtered activity, controls generation of these candidates. There are four, six, or eight candidates depending on available anchors/signals. Stable list order breaks exact ties.

## 5 Hard eligibility constraints

R13 Reject a candidate when pose/context validity fails. Reject reading distances below 0.8 m or above 3.5 m. Distances are Euclidean head-to-panel-center distances. These bounds are prototype readability choices. They require on-headset evaluation, including prescription, text size, and device optics effects.

R14 Project all four panel corners through the camera. Reject if any corner is at or behind the near clip plane or if the projected bounds leave the viewport inset of 0.04 on any edge. In stereo mode, independently test all corners using the left and right eye viewport projections with the same inset. A desktop monocular check cannot validate actual headset field of view.

R15 Urgent candidates must also have head-to-candidate angle <=20 degrees. Urgency never disables face, hazard, reading-distance, viewport, or geometry constraints. If no urgent candidate is eligible, use the explicit no-placement delivery rules. There is no forced central visual fallback.

R16 Protected faces permit at most 0.02 projected coverage of a supplied face region. Protected hazards permit at most 0.01 projected coverage. These hard limits are additional conservative prototype constraints; they are not thresholds supplied or validated by the paper. Task/referent regions otherwise contribute a weighted occlusion cost rather than a universal hard prohibition. Coverage limits apply to all supplied, active regions, regardless of relative depth, because live passthrough has no environment-depth occlusion configured.

R17 A CheckBox first rejects initial overlap at the head. A BoxCast then sweeps a head-oriented rectangular prism with half extents (0.31*s,0.145*s,0.015), using the same distance scale s from the head toward the candidate, stopping 0.03 m short of its center. Any non-trigger collider hit makes the candidate ineligible. This prism conservatively contains head-to-panel rays and avoids the gaps of a few sparse ray samples. It can reject visually clear placements because it is wider than the exact visibility pyramid near the head. It cannot account for unmodeled real-world objects, absent colliders, or incorrect supplied geometry.

R18 A rejected candidate remains in diagnostic records with its cost and rejection reason. Reasons include invalid_or_stale_pose, reading_distance, urgent_noticeability, outside_view, outside_stereo_view, face_coverage, hazard_coverage, blocked_by_geometry, and nonfinite_cost. A later rejection can replace an earlier reason; the recorded face/hazard coverage and other measurements remain available. Rejected candidates do not participate in minimum selection.

## 6 Cost definitions and normalization

R19 Each cost uses a fixed theoretical 0–1 normalization, not candidate-set min/max rescaling. This preserves comparability when an unrelated candidate is added or removed. Let h be head-to-candidate angle, p be candidate-to-partner angular separation as seen from the head, and L be workload clamped to [0,1]. Define clamp(x)=max(0,min(1,x)).

Centrality = exp(-0.5 * (h / 18)^2)

For Focus, Conversation, and Unknown:

I = clamp(L) * (0.75 * Centrality + 0.25 * clamp(h / 90))

For Physical:

I = clamp(L) * (0.20 * Centrality + 0.80 * clamp(h / 90))

This interruption surrogate trades central visual intrusion against reorientation. The physical-task variant places more emphasis on turning away from work. It is neither measured cognitive workload nor an interruption probability. The difference is a documented implementation hypothesis.

R20 Project each active protected region to a viewport bounding rectangle. Ignore a region only when its corners cannot be projected in front of the camera. Include nearer regions too: without live environment-depth occlusion, a virtually distant card can still cover a nearby person in the composited image. For each remaining region, coverage is intersection area of panel/region rectangles divided by region rectangle area. A degenerate region has zero coverage. Occlusion O is the maximum region coverage. This is worst-region projected obstruction, not total pixel-union area, mesh-level visibility, or inferred depth. Rectangle bounds can conservatively overestimate oblique-region coverage. Face and hazard coverage are recorded independently for hard-limit checks.

R21 Estimate full-visual reading duration T = 1.5 + max(0,wordCount)/3.3 seconds. During Conversation:

S = clamp((0.4 * clamp(h/90) + 0.6 * clamp(p/90)) * (0.5 + 0.5 * clamp(T/8)))

Outside Conversation, S=0. Head turn and partner-relative separation contribute to risk, with a bounded duration multiplier. Direction is represented by angular separation, not measured eye contact or left/right conversational conventions. Reading duration is a fixed visual-stage estimate because modality is chosen afterward. Speech, individualized reading rate, presentation-specific duration, and partner perceptions are not inferred.

R22 Combined cost C = wI*I + wO*O + wS*S. Weights are nonnegative and sum to one. A nonfinite combined result makes the candidate ineligible and replaces the stored cost components and total with 1 so the log remains numerically bounded. Normal runtime angles are calculated from valid geometry and lie in [0,180].

## 7 Context and urgency profiles

| Activity | Priority | wI | wO | wS |
| --- | --- | ---: | ---: | ---: |
| Focus | Low | 0.70 | 0.30 | 0.00 |
| Focus | Normal | 0.55 | 0.45 | 0.00 |
| Focus | Urgent | 0.20 | 0.80 | 0.00 |
| Physical | Low | 0.30 | 0.70 | 0.00 |
| Physical | Normal | 0.25 | 0.75 | 0.00 |
| Physical | Urgent | 0.15 | 0.85 | 0.00 |
| Conversation | Low | 0.15 | 0.25 | 0.60 |
| Conversation | Normal | 0.20 | 0.25 | 0.55 |
| Conversation | Urgent | 0.10 | 0.35 | 0.55 |
| Unknown | Any | 0.40 | 0.60 | 0.00 |

R23 These profiles are explicit experiment parameters. Higher urgency prioritizes noticeability through the eligibility constraint and full-visual delivery, while reducing the interruption weight. They have not been fitted to participants. A study should preregister a profile version and compare it with a fixed-placement baseline before tuning against outcomes. Software correctness alone cannot identify scientifically optimal weights.

## 8 Selection and temporal behavior

R24 Among eligible candidates, choose the lowest combined cost. An exactly tied result keeps the first candidate in generation order. If the candidate set is empty, the selected visual location is null. Selection does not minimize an audio cost and does not score joint placement-modality pairs.

R25 Evaluate nominally every 0.1 seconds. Keep an eligible incumbent unless another candidate improves cost by more than 0.04 and the incumbent has been held for at least one second. This is equivalent to a switching penalty of 0.04 after the hold interval, with stricter continuity during the hold. An invalid incumbent is replaced immediately. Scene/context/priority changes and explicit send/read actions can request a fresh minimum. View-relative positions continue to follow the current head; referent/partner positions are recomputed from current relative geometry. The renderer does not apply an additional untested smooth motion trajectory through the environment.

R26 Context invalidity can remove the entire eligible set. The current card is hidden and delivery is recalculated. Holding an incumbent is never allowed to preserve an ineligible location. The full result list is recalculated before presentation so the log reflects current geometry rather than stale score arrays.

## 9 Separate delivery policy

R27 First check pose and context age; invalid data means Deferred. If no eligible visual location exists, allow a positional chime only when audio is enabled, the supplied conversation flag is false, and speaking is false. Otherwise defer. The absence of a valid visual candidate is a normal handled state, not an exception.

R28 When a visual location exists, Urgent selects Visual. Low priority, Unknown activity, or Conversation selects Peripheral. A Normal Physical task with occupied hands, audio enabled, and speaking=false selects SpatialAudio. Remaining cases select Visual. Conversation priority is thus unobtrusive by default but urgent content still uses a constrained visual location.

R29 A peripheral cue is a compact subset of the scored visual envelope. Right trigger or desktop E explicitly requests reading; an eligible location then presents the full card and grants another 16-second reading window. The user's reading request does not override tracking or geometric eligibility. The current implementation uses a rounded high-contrast translucent world-space panel; it does not blur the composited passthrough feed. The polished photo demo's background frosting is a separate desktop rendering technique.

R30 SpatialAudio is a generated mono chime with Unity 3D panning and attenuation, placed at the supplied task/referent or two meters in front of the head when no task exists. It signals availability; it does not speak message content. Audio localization, noise masking, headset routing, HRTF behavior, and conversational acceptability remain hardware/user-study questions. A decision change into audio or a new audio notification triggers the chime. An unchanged decision does not replay it each evaluation.

## 10 Notification lifecycle and operator controls

R31 A new request replaces the active demonstration notification, clears its expanded state, requests a fresh decision, and starts a 16-second lifetime. There is one active notification, no queue, batching, real message account integration, or multi-card collision optimization. Expiry and acknowledgement hide the view. Deferred notifications retain their original lifetime rather than waiting indefinitely. Reading expansion restarts a 16-second window. All message text in this build is demonstration content.

R32 On the headset, right A sends, right B dismisses, right trigger reads, left X cycles scenes, left Y cycles urgency, and left stick click cycles supplied activity modes. In Passthrough, right grip replaces the supplied task annotation and left grip replaces the supplied partner annotation. Right stick up/down changes the annotation ray distance from 0.8 to 3.2 m. A grip annotation uses the tracked controller position/orientation and selected ray distance; it is not a surface hit, depth measurement, or object detector. An operator must supply the target distance deliberately. Replacing an annotation removes its previous protected region.

R33 The Android package starts in Passthrough; X then cycles Desk, Kitchen and LivingRoom. The desktop simulation uses 1–4 to choose Desk/Kitchen/LivingRoom/Passthrough, arrows to rotate the simulated head, WASD to translate it, Space to send, Enter to dismiss, E to read, C to cycle supplied activity, U to cycle urgency, and Tab for the desktop inspector. Headset mode omits the desktop HUD and inspector. Scene switching clears the active notification and resets context dwell. It preserves a single tracked rig and notification renderer rather than spawning duplicate rigs per scene.

## 11 Environments and camera simulation

Desk contains a work surface, monitor, keyboard, bookshelf, window, lighting, partner proxy, and plant. Its monitor is the protected task rectangle. Kitchen contains a counter, cabinets, preparation area, bowl, stove, pot, refrigerator, lighting, and partner proxy. Its task region covers the preparation area; a separate hazard rectangle covers the hot-surface region. LivingRoom contains sofa geometry, coffee table, book, rug, lighting, and partner proxy. Its reading area is the protected task region. Every simulation includes walls/floor and colliders, allowing geometric obstruction tests.

The partner is a stylized geometric proxy with a protected face rectangle, not a photorealistic human or face detector. Simulation scene rendering mimics a view into a room; scene annotations supply ground truth. It does not reproduce Quest camera intrinsics, stereo calibration, exposure, motion blur, low-light noise, depth estimation, or passthrough latency. Those factors require separate sensor simulation or on-device capture.

Offscreen camera renders are saved without reading or unlocking the physical desktop. Diagnostic full-card illustrations show the selected visual candidate even in conditions where actual delivery would be audio or a compact cue; they must be labeled as placement illustrations. Decision records retain the actual channel rules. Saved rendering tests validate appearance of known scenes, not raw-camera understanding.

## 12 Logging and verification

R34 Each session writes JSON Lines in Application.persistentDataPath. Events include decision changes, notification requests, acknowledgement, expiry, reading requests, and manual annotations. Records contain UTC time, event type, environment, input-source provenance, active context, urgency, channel, request ID, pose validity, selected candidate, and all candidate measurements/costs/rejection reasons. The provenance distinguishes simulation ground truth from tracked head plus manual real-room annotations and supplied activity. Logs do not include real camera or microphone recordings. They are research diagnostics, not behavioral study results.

R35 Verification includes bounded-score/property examples, weight sums, inference precedence, age/tracking gates, context dwell, channel fallbacks, coverage examples, social direction/duration relationships, and a spatial sweep. The sweep combines three rooms, three contexts, three priorities, six yaw directions, and three seated/standing head positions: 486 scenarios. Every eligible result must satisfy distance, urgency, face, and hazard conditions. Failure-case tests separately cover tracking loss, stale poses, a fully blocking wall, initial collider overlap, and protected face/hazard coverage. A built desktop player audit exercises all three contexts in each room, notification delivery, reading, dismissal, scene switching, and the absence of simulated room renderers in Passthrough. Scene render captures and Android build results are separate evidence. The actual report, rather than planned counts, is authoritative.

Software acceptance requires a successful import/compile; scenes with valid bindings; passing policy/spatial tests; readable offscreen rendered panels; configured Android OpenXR/Meta features; and a built APK. Device acceptance additionally requires installing/launching on Quest 3S, tracked stereo presentation, input mapping, passthrough initialization and recovery, readable field of view, tracking-loss handling, real-room annotation, audio, and measured performance. Passing desktop tests never substitutes for these gates.

## 13 Research provenance and future calibration

The supplied paper's Sections 3.1–3.3 motivate contextual sensing, head orientation, three competing visual costs, normalization/weighting, and a separate modality policy. Its walkthrough's joint placement-modality phrasing is resolved here in favor of the more specific two-stage description. Section 3.4 describes Apple Vision Pro and does not demonstrate a Quest implementation.

Lindlbauer, Feit, and Hilliges (UIST 2019) provide relevant precedent for rule-based contextual decisions combined with combinatorial optimization. This prototype uses finite enumeration for transient notification positions rather than claiming to reproduce their entire optimizer. The Notiflex paper's social-risk motivation and cited conversational-engagement literature support investigation of partner-relative shifts; they do not validate the coefficients used here.

Before calling a model optimal for people, collect ground-truth task performance, noticeability, acknowledgement times, perceived interruption, and partner-rated engagement. Evaluate a fixed strategy, the predefined profiles, and controlled ablations of social cost and hysteresis. Tune on training participants, report held-out outcomes, quantify uncertainty, and preserve a versioned rules/configuration record. A learned classifier can replace a supplied context adapter later without changing the visual-scoring contract, but its accuracy must be assessed separately.

References

- Supplied manuscript: Joseph and colleagues, Notiflex Adaptive Notification Presentation Across XR Contexts, Sections 3.1–3.4, provided by the user.
- Lindlbauer, Feit and Hilliges. Context-Aware Online Adaptation of Mixed Reality Interfaces. UIST 2019. https://doi.org/10.1145/3332165.3347945 . Author project: https://ait.ethz.ch/computationalmr .
- Meta. Get Started with Passthrough. https://developers.meta.com/vr/documentation/unity/unity-passthrough-gs/ .
- Meta. XR Plugin Management for Meta VR. https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/ .
- Unity. OpenXR package documentation. https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.19/manual/index.html .
