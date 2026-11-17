# Notiflex 0.6.0: implemented method and exact operating rules

This is the current method specification. It supersedes conflicting descriptions in the original Word manual and 0.3–0.5 documents, which remain historical records. The new software paths are implemented and tested offline; **new 0.6.0 sensor behavior and performance await Quest testing**. Earlier headset trials apply only to their recorded versions. No calibrated social probability or established research novelty is claimed.

## Pipeline and evidence

On Quest 3/3S, the Passthrough scene runs local RGB object detection, tracked head pose, genuine bare-hand tracking, saved-room geometry, live environment raycasts, and permission-gated microphone processing. No cloud AI is involved. The headset renders only the notification. Authored Desk, Kitchen and LivingRoom scenes use known geometry and supplied activity; they are controlled simulations rather than camera reconstruction of a real room.

Order: acquire fresh evidence → estimate context → apply temporal dwell → resolve the request's explicit referent → generate finite visual candidates → reject invalid candidates → compute normalized visual costs and weighted totals → retain/select a placement with hysteresis → independently choose presentation modality → render/play/defer. Actual channel exposure and social score are logged separately from the full-card visual-placement score.

Head orientation is an attention proxy. Quest 3S eye tracking is neither supported nor claimed. The estimates recognize bounded combinations of object, movement, hand and speech evidence; they do not identify arbitrary activities or room types.

## Notification identity and referents

`NotificationRequest` carries `id`, `source`, `title`, `body`, urgency (`Low=0`, `Normal=1`, `Urgent=2`), optional `referentClassId`, `referentTrackId`, and `referentSceneId`. Class/track IDs default to −1. A scene UUID takes precedence. Generic requests have no invented referent; pressing A creates an explicitly marked generic demo. The producer, not the placement optimizer, decides what a message refers to.

Bodies must contain 1–600 characters; messages are paginated into at most 100-character chunks, with long tokens split. Heading display is limited to 22 characters. Scores estimate the whole request's reading duration, rather than pretending a multi-page message is read instantly. The public Unity `Send(NotificationRequest)` API supports producers; a launch-time `notiflex_notification` JSON extra supports reproducible integration tests. This is not OS-wide interception of other applications' notifications.

RGB detection supplies class, confidence, projected box, captured origin, bearing and affine ray basis. For each box, five depth rays sample center and ±20% of box width/height. At least three valid depths in [0.2, 8] m are required, including a valid center ray agreeing with the fused median within `0.15 + min(0.1, sigma)` m. This prevents neighboring background samples from simply replacing an inconsistent foreground center. Their median supplies distance; median absolute deviation (MAD) must be ≤0.25 m, with ≥3 distances within max(0.08 m, 3 MAD). The bearing gives a metric position and normals give an approximate local surface normal. Depth is queried after inference, not synchronously with the captured RGB frame: lag must be ≤0.55 s and increases uncertainty.

Measurement standard deviation is `0.035 + 1.4826 MAD + 0.5 lag` metres. This is a conservative engineering uncertainty proxy, not calibrated sensor covariance. Missing/inconsistent depth remains bearing-only. The implementation estimates a surface-supported object location; it does **not** recover semantic object orientation, exact object center, articulated pose, or guaranteed full 6DoF pose.

A constant-velocity Kalman filter smooths positions, predicts at most 0.5 s ahead, and propagates covariance for eligibility. Measurement updates use a 3D squared-innovation gate of 11.345, the 99% chi-square threshold under the assumed Gaussian model. Long gaps >1 s reinitialize; nonfinite data are rejected. Process-noise intensity is 0.5 in the implemented discrete model; velocity is capped at 3 m/s. This gating threshold does not imply measured 99% tracking accuracy.

A Hungarian assignment finds the global minimum of class-consistent association costs, with dummy assignments permitting unmatched observations/tracks. Metric association uses normalized innovation (80%) and box IoU (20%); bearing-only association uses IoU (60%) and angle (40%), requiring angle ≤18° and IoU ≥0.02. Pair cost must be <1.5. Up to 32 observations and 64 existing tracks bound work. Tracks expire after 1.5 s unseen; IDs are session-local and not persistent object identity.

Referent resolution requires ≥2 observations, a last-seen and last-metric age ≤0.8 s, and predicted variance ≤0.16 m². Explicit IDs/classes must match. Class-only resolution ranks confidence minus angular and uncertainty penalties; best-versus-second margin <0.12 is ambiguous and yields no referent. The exact score is `confidence − 0.35 clamp(angle/90) − 0.1 sigma`. Scene UUIDs resolve through the actual saved room. Unavailable/ambiguous referents fall back to unrelated eligible placements while logging the reason; they are never silently replaced with a different object. Identical objects, occlusion and relocalization can break association. Tracking-space changes >2 cm or 0.5° clear object tracks.

## Context rules

The original object/head/hand estimator is preserved as an input to multimodal fusion. Camera observations must be playing, focused, head-tracked, unpaused, ≤1.5 s old, and captured within 0.10 m of the current head position. Capture poses transform bearings into world space. These gates do not guarantee perfect alignment during motion.

Work classes (COCO IDs) are TV/monitor 62, laptop 63, mouse 64, keyboard 66 and book 73. Manipulation classes are bottle 39, wine glass 40, cup 41, fork 42, knife 43, spoon 44, bowl 45, scissors 76, hair dryer 78 and toothbrush 79. Detections require score ≥0.5 and valid finite in-image boxes/bearings; same-class NMS uses IoU >0.5, retaining at most 24 detections. No general hazard detector is trained.

Focused work requires work-object angle ≤24°, head speed <0.12 m/s, angular speed <15°/s and absent or slow hands (<0.07 m/s). Its evidence is confidence × `exp(−0.5(angle/24)²)`. Active physical hands require valid tracking, working-area presence, ≥1 s accumulated activity and speed ≥0.10 m/s. Bimanual evidence is `0.85 clamp(speed/0.16)`; object-associated evidence is object confidence × `clamp(speed/0.16)`, requiring supported object angle ≤70° and hand/object bearing separation ≤25°. Evidence ≥0.55 supports the corresponding context. Speech/person fusion supersedes the old person-presence-only social proposal, recomputing work/manipulation evidence when a silent person would otherwise hide it. Conversation evidence takes precedence when supported, then Physical, then Focus.

The hand working area is 0.15–1.25 m from the head, 0.10–0.95 m below it and at least 0.08 m forward along head direction. Bimanual hands must be within 0.65 m of each other. Activity integrates while speed ≥0.07 m/s, up to 2 s, and decays twice as fast at rest. Frame integration is capped at 0.1 s. These thresholds describe a manipulation proxy, not a universal action recognizer. Physical activity uses sustained actual bare-hand movement with an aligned manipulation object or two hands working together. Held/inferred controller hand poses are rejected; high-confidence tracked wrists and fingers supply motion. Position smoothing precedes differentiation; reacquisition and finger-confidence transitions reset rather than creating motion spikes. A single isolated wave does not establish a physical task. The exact original thresholds and work/manipulation class lists remain in `AutomaticContext.cs` and the historical bare-hand method.

New fine-activity refinement: with valid hands, wrist speed <0.07 m/s and head angular speed <15°/s, a grounded work object within 0.5 m of a working hand, confidence ≥0.7 and head angle ≤24° supports Focus instead of Physical. This distinguishes some desk/finger activity from larger manipulation; it does not reliably recognize typing, cooking, repair or every fine task by name.

Workload is a proxy: Focus 0.65; Physical `clamp(0.5 + 0.3 handSpeed, 0.5, 0.9)`; Conversation 0.6; Unknown 0.1. Existing physical movement/head-motion evidence remains separately available. No mental-load sensor is claimed.

Automatic dwell collects a trailing 2 s evidence window and requires 1.2 s support. Invalid evidence or update gaps >0.25 s reset support. This allows natural intermittent hand motion without accepting isolated spikes. The manual/simulation dwell requires 1.2 s uninterrupted proposed context. While Physical is latched, brief hand occlusion retains the busy-hands assumption. Context freshness and pose validity are still checked independently for delivery.

## Conversation and partner localization

The microphone is enabled with the user's approval and Android microphone permission, only in automatic live mode while the app has VR focus. `notiflex_local_speech=false` disables it. Permission denial yields a visible-person estimate without a conversation claim. Raw waveforms, speech features and anonymous acoustic embeddings exist in memory only; they are cleared on sensing stop/focus loss. No recordings, transcripts, voiceprints or named identities are saved/uploaded. Derived counts/probabilities and processing timings/backlog are logged. Sensor permission requests are serialized, so simultaneous camera/scene/microphone requests cannot replace one another. OS/SDK behavior remains separate from application file logging.

Silero VAD runs locally on 512-sample 16 kHz blocks with 64 context samples and recurrent state. Start threshold 0.65; stop threshold 0.35 with a 0.24 s hangover. A 5 s window estimates speech fraction. Processing evidence older than 0.5 s is unavailable; queued blocks older than 0.3 s are discarded. A four-block queue bounds backlog and resets stale recurrent state. The resampler uses an anti-aliasing 64-tap windowed-sinc filter when resampling; 16 kHz passes through. Own chimes suppress speech processing for 0.6 s to avoid counting the notification as speech.

WeSpeaker ResNet34 embeds approximately 2.015 s of voiced audio, at most once per 2 s, using Kaldi-compatible 80-bin log-mel features and mean normalization. Up to four anonymous session clusters are held for 45 s. Cosine match ≥0.72 and margin ≥0.08 updates an existing cluster; similarity <0.35 can create one; intermediate/mixed evidence remains unattributed. Segments <1.5 s are insufficient. Switch counts use a trailing 30 s window and consecutive different cluster assignments no more than 10 s apart. These are uncalibrated acoustic heuristics; overlapping voices and mixed windows can defeat them. They are not certified diarization or wearer/partner identification.

A partner candidate is a person track with ≥2 observations, last-seen age ≤0.8 s, projected area ≥0.06, head angle ≤35°, and, if metric, distance 0.4–3.5 m. Ranking uses confidence × a Gaussian angular factor (40° scale), with a 0.05 incumbent bonus. A best-versus-second margin <0.08 leaves multiple people ambiguous. Grounded partner position is used when available; otherwise only direction is claimed. The whole person box is protected, not a detected face.

A person whose projected box is ≥80% contained within a tracked TV/laptop, whose combined position standard deviation is ≤0.2 m, and whose metric position is within 0.12 m of the screen is excluded as a possible screen depiction. This reduces some TV/video-call false positives, without proving every visible person is physically present.

Recent speech (≤2 s) and speech fraction ≥0.12 combine with a visible partner. Interaction support is the clamped visual partner score ×0.72, or ×0.95 with ≥2 anonymous speakers and ≥2 recent switches. Support ≥0.55 proposes Conversation. Person-only evidence is no longer called conversation. Logs distinguish person presence, microphone unavailable, speech with visible person, anonymous turn-taking, screen depiction and ambiguous groups. A nearby person plus speech may still be a call, TV, unrelated speech or one-sided address: this is an interaction estimate, not confirmed mutual conversation. Silence and short utterances can reduce support. Visible-person protection and conservative audio suppression apply even without conversational confirmation.

## Room geometry and candidate generation

MRUK 209 loads the headset's actual saved room with spatial-data permission, requesting a system scan if none exists. It uses the supported V1 scene model; this version does not claim unavailable high-fidelity V2 reconstruction. Planes, bounded objects and available global mesh create invisible collision geometry; no authored test-room prefab substitutes for missing device data. Environment raycasts supplement the map with current depth when supported/ready. Quest 3S support does not imply the presence of a dedicated depth sensor.

Candidates comprise four head-relative locations in metres: center `(0,0,1.8)`, left/right `(±0.85,0.08,1.8)`, upper `(0,0.55,1.8)`. Explicitly grounded referents and metric partners add two proximal positions: ±0.65 m laterally, +0.35 m vertically and 0.30 m toward the arrival head position. Their basis uses the head's position at request arrival, so subsequent head turns do not drag them. Tracked objects may move; their filtered position moves associated candidates. These session tracks are not persistent anchors across application restarts.

Mapped eligible planes add at most four candidates. Planes must fit the card, be substantially vertical (`|normal·up|≤0.65`), and face the viewer; center and local offsets `(±0.4,0)` and `(0,0.3)` are considered. Plane-local base positions are retained per request and move with the MRUK anchor rather than following the head. Cards sit 0.08 m in front of the plane. All scaled corners must fit the plane rectangle and polygon. Distance must be 0.8–3.5 m and head angle ≤50°. Eligible planes are ranked by head angle. World locking comes from loaded scene anchors; this app does not create/save a new spatial anchor for every notification.

## Hard geometric/readability constraints

A full card is 0.62×0.29 m at reference distance. Beyond 1.8 m it scales by `max(1, distance/1.8)` to preserve angular size. The actual candidate rotation defines its envelope. Incidence `|normal·viewDirection|` must be ≥0.57, and distance 0.8–3.5 m. Urgent notifications require head angle ≤20°. Nonfinite or stale pose/context and candidate uncertainty >0.35 m reject a visual candidate. Hard constraints may defer even urgent requests.

The whole card must fit within a 4% margin of the center-head viewport and both eye viewports when stereo is active. Any nontrivial projected overlap with an RGB person box rejects the candidate. Supplied face annotations reject target coverage >2%; supplied hazards reject >1%. Detected objects are protected through projected overlap costs. A card behind a person is still protected because passthrough compositing can visually cover them.

A swept rectangular collision prism conservatively covers the head-to-card rays. It rejects mapped/supplied obstacles, including thin blockers between sparse sample rays; it can also reject otherwise usable locations. Five live depth rays (center and corners) reject observed geometry at least 0.06 m before the panel. Missing depth does not count as proof of free space. Candidate logs report map availability and depth hits. Unmapped regions, unsampled dynamic obstacles, inaccurate scans and missing camera classes remain limitations. This is notification placement, not a safety or navigation system; it does not provide pixel-perfect dynamic real-world occlusion masking.

## Normalized costs and priority profiles

All `clamp` terms below clamp to [0,1]. Let `θ` be head-to-candidate angle, `φ` candidate-to-partner angle, `ρ` candidate-to-explicit-referent angle, `L` workload, `A=exp(−0.5(θ/18)²)`, `H=clamp(θ/90)`, `R=clamp(ρ/90)`.

Without a referent, interruption is `L(0.2 A+0.8 H)` in Physical and `L(0.75 A+0.25 H)` otherwise. With a referent it is `L(0.15 A+0.35 H+0.5 R)` in Physical and `L(0.55 A+0.2 H+0.25 R)` otherwise. Occlusion is the maximum projected target coverage, clamped to [0,1]. Hard rejections are applied independently of weighted cost.

Default reading exposure is `0.8 + 60 words/180` seconds, divided by `max(0.6, incidence)` for visual obliquity. Cue exposure is 0.65 s; chime exposure 0.25 s; deferred exposure 0. Let `P=clamp(φ/90)` and `D=clamp(exposure/8)`. During Conversation, default social cost is `interactionSupport × clamp((0.35 H+0.65 P)(0.25+0.75 D))`; outside Conversation it is zero. Audio does not require a visual attention shift, so its H and P are zero; conversational audio is suppressed separately. Coefficients/durations are hypotheses, not empirical human probabilities.

Visual candidate ranking uses full-card exposure. After selecting modality, the actual channel's exposure and risk are calculated separately for diagnostics; modalities are not jointly mixed into the placement search. Manual conversation uses support 1 when no sensing support is supplied.

| Context | Urgency | Interruption | Occlusion | Social |
|---|---|---:|---:|---:|
| Focus | Low | .70 | .30 | 0 |
| Focus | Normal | .55 | .45 | 0 |
| Focus | Urgent | .20 | .80 | 0 |
| Physical | Low | .30 | .70 | 0 |
| Physical | Normal | .25 | .75 | 0 |
| Physical | Urgent | .15 | .85 | 0 |
| Conversation | Low | .15 | .25 | .60 |
| Conversation | Normal | .20 | .25 | .55 |
| Conversation | Urgent | .10 | .35 | .55 |
| Unknown | Any | .40 | .60 | 0 |

Total = weighted sum. The lowest eligible total wins; fixed candidate order breaks ties. The incumbent is retained until a different candidate improves total by >0.04 and ≥1 s has elapsed since the previous placement change. Invalid incumbent, no incumbent, explicit reading/new request, or context/priority changes force reevaluation. Costs are engineering surrogates; no continuous global optimum or empirically optimal weights are claimed.

## Delivery, interaction and logging

Stale/invalid pose/context → Deferred. No visual candidate → audio only when audio is enabled, no conversation and no detected speech; otherwise Deferred. With a candidate: Urgent → Visual; Low, Unknown or Conversation → Peripheral; Physical with busy hands, permitted audio and no speech → SpatialAudio; otherwise Visual. A visible person disables automatic audio even before conversation confirmation. Explicit reading promotes an eligible candidate to Visual; hard constraints still apply.

A sends a demo; trigger reads/advances; B dismisses; X cycles environments; Y cycles urgency; right stick click toggles automatic mode. Hands-only: point the tracked hand pointer at a visible cue/card and pinch to read, advance pages, or dismiss the final page. A pinch needs valid tracked input, 0.25 s release before activation and 0.7 s cooldown. Tracking loss requires a fresh release. No head-gaze dwell or eye tracking is used. Audio-only cues have no card to point at; content can appear after activity stops if the request remains active, or be opened with the controller.

One chime is permitted per request, including repeated context/placement changes. Debug sound diagnostics intentionally bypass this gate. Normal requests expire after 16 s; explicit reading extends by at least 16 s or the estimated whole-message reading duration. Focus loss stops sensing; presentation delivery requires a tracked, fresh pose. Test notifications are opt-in launch extras, every 15 s for 3 minutes.

JSONL diagnostics include context/evidence age, class IDs, hand summary, anonymous speech counts/probabilities, room/depth availability, request provenance, referent status, candidate position/orientation/source/uncertainty, hard rejection reasons, I/O/S/total costs, selected channel, exposure and profile identity. Notification bodies, raw images, waveforms and embeddings are not written by the application logger. Logs can contain sensitive spatial and interaction metadata; keep experiment files under the research team's data policy.

## Calibration and research claims

The default profile is explicitly `unfitted_prototype`. `tools/fit_social_risk.py` fits a monotone regularized logistic model from independent binary disengagement outcomes with participant-separated held-out evaluation. See [calibration protocol](SOCIAL-CALIBRATION.md). Synthetic tests cannot enable participant-fit status. A valid accepted profile can be bundled as `Resources/Notiflex/SocialRiskProfile.json`; source and held-out metrics accompany it. This is a configurable fit path, not evidence that a human model has already been validated.

The software now implements the paper's two-stage framework and richer spatial/speech paths. Real-room accuracy, stable world alignment, application performance, arbitrary activity/conversation reliability, and human benefits remain experimental questions. Research novelty needs a separate comparison with prior work and evaluation; complexity alone does not establish it.
