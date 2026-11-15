# Automatic camera mode — implementation and acceptance record

## Connected-headset preflight, November 15, 2026

Quest 3S; Horizon OS v209 (`ro.vros.build.version` and v209 build branch); Android 14/API 34. USB debugging authorized. Camera service enumerates left and right passthrough sources with Meta vendor camera-source tag 0 and position tags 0/1. Battery 84%, charging; approximately 99 GB storage available. Existing Notiflex 0.3.1 is installed with working A/trigger/B/X controls. No camera images were recorded during these checks.

These checks establish hardware prerequisites, not successful image acquisition by Notiflex. The new application must be built and installed before its camera permission can be requested and its inference/placement behavior tested on the headset. Reconnection will be required for that acceptance stage.

## Version 0.4.0 architecture

`CameraContextSensor` owns a left-camera MRUK `PassthroughCameraAccess` component and a CPU Unity Inference Engine worker. Dependencies are pinned to MRUK 209.0.0 and Inference Engine 2.2.1. The bundled model is Meta's quantized YOLOv9t example, with RGB NCHW input at 640 × 640 and three outputs: box corners, COCO class IDs and object scores. Its source, license and hash are recorded in the third-party notices/evidence. The camera requests 640 × 480 at up to 15 FPS; the API selects a supported resolution. Inference preserves aspect ratio using a GPU letterbox pass with RGB padding 114/255. When MRUK supplies an sRGB texture in linear rendering, sampled values are re-encoded to RGB so model input is consistent with training/reference images. Normalized boxes are mapped back through the inverse letterbox to camera viewport coordinates, including the top-left tensor to bottom-left viewport Y flip. Source dimensions come from the actual texture. Boxes entirely in padding are discarded. The source/model dimension mapping is covered by tests.

Only one inference runs at a time. Layer scheduling yields when its elapsed budget reaches 2 ms, then output readiness is polled without synchronous completion in the ordinary observation loop. This is a soft scheduling budget: an individual operation or CPU/GPU transfer can still take longer. Sampling waits at least 0.5 seconds after each completed inference, increasing to as much as 2 seconds when inference is slow. Device frame time and thermal behavior remain unverified.

No model server, API key, cloud inference, microphone capture, eye tracking or identity recognition is used. Application code neither saves nor uploads camera images. Decision logs contain derived classes, confidence, timing, inference reason and placement costs. Bundled SDKs retain their own behavior and permissions; this is not a claim that all SDK network traffic is disabled.

## Modes and controls

On Android, live Passthrough starts in automatic mode. Authored rooms and the Mac simulation retain supplied context/ground truth. A sends a demonstration notification, trigger reads, B dismisses, X changes scenes and Y changes urgency as before. Receiving an external notification stream is a separate integration.

In live Passthrough, **right stick click toggles automatic/manual mode**. **Left stick click switches to manual and cycles the supplied activity**. Either grip annotation also switches to manual. Returning to automatic clears the context dwell; old manual task and partner anchors are excluded from automatic scoring. There are no camera preview panes, detector boxes or persistent controls in the headset view.

The app requests `horizonos.permission.HEADSET_CAMERA` through the normal OS dialog after it obtains VR focus. It does not pre-grant permission through ADB or request scene/microphone permissions for this direction-only implementation. Denial leaves automatic context unavailable; right stick click still provides manual mode. Settings can grant permission later. Camera acquisition stops in manual/synthetic modes, while paused, or when system menus take focus. In-flight work may finish but its results are discarded after a generation change.

## Observation validity

- Use the frame timestamp, not inference completion, as observation time. Include acquisition age and inference latency. Reject timestamps more than 0.05 seconds in the future or older than 1.5 seconds; tolerate up to 0.05 seconds of clock jitter by treating acquisition age as zero.
- Cache the camera pose and affine ray basis before inference. Results are never paired with a newer camera pose. Use SDK intrinsics/crop handling through `ViewportPointToRay`, without a guessed camera FOV.
- Reject results when more than 1.5 seconds old or after head translation exceeds 0.10 m since capture. This conservative direction-only bound can prevent classification during walking. Rotation is handled by projecting captured world bearings into the current head view.
- Require current tracked head pose for delivery. Missing permission, camera failure, no fresh observation, pause and focus loss provide no valid automatic snapshot; the runtime defers notification delivery. Empty but fresh detections produce Unknown and can use the normal peripheral policy.
- Filter nonfinite/invalid boxes, scores and directions. Accept object scores of at least 0.50. Perform class-aware NMS at IoU greater than 0.50 and retain at most 24 relevant detections. These thresholds are prototype parameters, not learned activity calibration.

## Context evidence and precedence

`AutomaticContext.Estimate` uses centrality and measured movement. Confidence is an explicit heuristic evidence score; it is not a calibrated probability of intent, workload or conversation. All selected contexts require evidence of at least 0.55. The existing 1.2-second context dwell stabilizes changes.

1. **Possible social interaction:** person class 0, score at least 0.50, bounding-box area at least 6% of the camera image and head-to-person bearing angle at most 35°. Evidence is `objectScore × exp(-0.5 × (angle / 40°)^2)`. If at least 0.55, use the existing Conversation profile with reason `person_aligned_social_presence_proxy`. This takes precedence over work/physical estimates. It establishes neither speech nor mutual engagement. A TV image, poster or background person may be misdetected. Any accepted visible person suppresses automatic audio even when this social-context threshold is not met.
2. **Possible physical task:** a detected manipulable object within 40° plus smoothed velocity from a position-tracked Touch controller of at least 0.18 m/s. Evidence is `objectScore × clamp01(handSpeed / 0.30) × exp(-0.5 × (angle / 40°)^2)`. Classes are bottle, wine glass, cup, fork, knife, spoon, bowl, scissors, hair drier and toothbrush. This is explicitly a controller-motion proxy, not hand-object contact or general action recognition. Without controllers being held/tracked, this branch may remain Unknown. Movement supplied to the placement policy is `clamp01(handSpeed / 0.5)`.
3. **Possible focused viewing/work:** monitor/TV, laptop, mouse, keyboard or book within 24°, smoothed head translation below 0.12 m/s and head angular speed below 15°/s. Evidence is `objectScore × exp(-0.5 × (angle / 24°)^2)`. Watching TV and working at a screen cannot be distinguished by this rule.
4. **Unknown:** no qualifying evidence. Object labels such as oven/refrigerator never establish cooking or workload on their own. High controller velocity alone cannot establish a physical task.

Head/controller speed smoothing uses exponential blending with a 0.25-second time constant. Workload remains a declared policy surrogate: 0.65 for a selected non-Unknown proxy and 0.10 for Unknown. Speaking remains explicitly supplied and defaults false. No microphone or fully automatic conversation sensing is implemented.

## Visual protection and placement

Automatic mode supplies detected object bearings and image extents, not fabricated metric object positions. Its candidates are the four existing head-relative visual positions (center, left, right and upper). Referent/partner-anchored candidates are not created without metric anchors. Partner bearing still contributes to the social cost. Automatic audio cues originate at a virtual point 2 m ahead of the head, not a claimed measured object location; old manual anchors are excluded from that cue too. The existing interruption/occlusion/social profiles, finite candidate minimization, hysteresis and separate channel choice remain intact.

Relevant detections are projected into the current center-head viewport with an additional 12° tangent-space expansion to reduce baseline, translation and close-object uncertainty. This padding is a prototype heuristic and cannot bound parallax at arbitrary unknown distances. A box crossing behind the head-view plane conservatively protects the entire viewport rather than silently disappearing. Other entirely behind-view boxes are excluded.

Work/manipulable-object rectangles contribute to the existing maximum projected-coverage occlusion cost. Whole-person rectangles reject a panel when protected-region coverage exceeds `0.00001`, rather than treating a full body as a measured face. This conservative policy can defer all candidates around a nearby person. The log's existing `faceCoverage` field also carries this person-proxy coverage; `camera_person_coverage` identifies the rejection source.

No metric room reconstruction, live scene colliders, surface distance, face localization, hot-surface detection or environment-depth occlusion is claimed. Unknown objects and things outside the camera image remain unprotected. The notification is still an overlay within Notiflex's own foreground XR application.

## Diagnostics and acceptance

Unity logcat reports `NOTIFLEX_CAMERA` lifecycle states, `NOTIFLEX_OBSERVATION` counts/timing/proposals and `NOTIFLEX_MODE` changes. JSONL events identify `local_camera_object_and_motion_proxies`, camera status, proposed context, heuristic confidence, reason, observation age, inference duration, detected class IDs, stabilized context and candidate costs.

Six EditMode groups cover the original policy/spatial/failure tests plus automatic evidence/freshness, coordinate/NMS/person protection, and execution of the actual bundled model. A separate offline probe runs on the three supplied reference photos using local preprocessed tensors; only derived results are saved in the repository. Neither is evidence of live camera alignment or headset performance.

After installing the new APK, run these hardware checks:

1. Accept camera permission. Verify `observing`, fresh timestamps, finite inference timing and repeated frame sequence increments. Deny permission once in a separate run and verify manual mode remains usable.
2. Look steadily toward a real laptop/monitor or book, then turn away. Compare detections, proposal, 1.2-second dwell and selected visual/peripheral behavior. Test an unrelated TV to document the known intent ambiguity.
3. Face a person with their agreement. Verify the social-presence proxy, projected protection and absence of automatic audio. Repeat with background people, a poster and multiple people to expose false positives.
4. Hold a tracked controller while moving it near a visible supported object. Verify the physical-task proxy; repeat with a static object, no object and an untracked controller to verify that each alone is insufficient. This is an experiment proxy, not a claim that controllers must be held for a finished hands-free product.
5. Exercise X through every room and back, system-menu focus, pause/resume and permission changes. Confirm no stale result crosses a mode/scene transition.
6. Measure actual per-eye text legibility, camera/notification alignment, frame times at the 72 Hz target (13.89 ms/frame), latency, head-motion failure behavior and 10-minute thermal stability. Tune only against saved evidence; do not label offline tests as headset acceptance.

Live camera acquisition, permission behavior, on-headset inference and automatic-context acceptance are pending until this new version is installed and tested.

Official references: [Camera API](https://developers.meta.com/vr/documentation/unity/unity-pca-overview/), [MRUK camera integration](https://developers.meta.com/vr/documentation/unity/unity-pca-documentation/), [on-device inference](https://developers.meta.com/vr/documentation/unity/unity-pca-sentis/).

## Offline reference-image findings

The supplied desk photo exposed a preprocessing failure: directly stretching it to 640 × 640 produced no relevant detection above the 0.50 threshold (monitor score approximately 0.389). Preserving aspect ratio raised the keyboard score to approximately 0.905 and mouse score to 0.757, without lowering thresholds. The portrait produced a person score approximately 0.826; the mechanic photo produced a person score approximately 0.646. The latter does not demonstrate recognizing repair work. These three reference images are development probes, not a representative accuracy dataset or a validation of context classification. Original stretched comparisons are retained in derived evidence for transparency.

## Build configuration and local cache correction

Android uses a single GameActivity entry, the new input backend, landscape orientation and contextual passthrough during OS loading. The desktop build explicitly restores both input backends for its legacy keyboard controls. OpenGL ES3 remains the tested baseline; Vulkan-only buffer-discard optimization is disabled, and OpenXR latency preference is input polling. These settings do not establish measured device latency.

The development checkout contained numbered duplicate generated Gradle resources and type databases. Their copies were preserved outside the Unity project cache before a fresh build. The duplicate type databases had produced large duplicate-class diagnostic counts even in successful builds; the fresh process/build eliminated those diagnostics. This was a generated-cache issue, not a reason to remove or rename application source assets.

## Packaged development build

Final Android build result: **Succeeded, 0 errors, 35 warnings**. APK v2 signature verified; ARM64 Unity, OpenXR, OVR, MRUK camera and Burst libraries are present. Camera permission is HEADSET_CAMERA; no microphone or Scene permission is declared. The final manifest contains a single UnityPlayerGameActivity entry and uses the new Android input backend. APK SHA-256: `c98f2b19ef7bc6b5d213e971add6f2f7157fb56dd9ebb0a8b8fda14ed4eb87e3`.

Remaining warnings are preserved in `evidence/automatic-mode/build-warnings.txt`: pre-existing Unity object-search API deprecations, bundled inference shader diagnostics, the SDK OpenXR patch-version advisory and Meta's project-manifest checker. That checker still warns about GameActivity, while direct inspection of the built manifest confirms the requested entry. These are recorded rather than treated as headset validation. No physical camera test has been passed by this build.
