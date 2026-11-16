# Notiflex VR validation record

Original software baseline: version 0.3.0 · November 11, 2026 · Mac development host · Unity 6000.4.7f1

The original Android APK and Mac player were built successfully. No Quest was attached for the November 11 software baseline. On November 15, version 0.3.1 was built, installed and launched on a physical Quest 3S: passthrough and A/trigger/B/X behavior were confirmed by the operator and app logs. The three Unity MCP regression groups passed again. See the [controller fix record](CONTROLLER-FIX-0.3.1.md) for exact evidence and scope. This is not a certification of flawless VR behavior or a validation of human interruption costs.

## Automatic-mode 0.4.0 software addendum — November 15

The new camera code imports and compiles with MRUK 209.0.0 and Unity Inference Engine 2.2.1. The Android development APK builds with zero errors and its v2 signature, camera permission, ARM64 libraries and single GameActivity manifest entry were verified; remaining warnings are recorded in the versioned evidence. All six EditMode groups passed with zero skips/failures, including the actual bundled model on CPU, the original 486 spatial scenarios and automatic evidence/freshness, letterbox/Y-origin mapping, class-aware NMS and person protection. Evidence is separated under `evidence/automatic-mode/`; original release evidence remains intact.

Three supplied photos were processed locally with both stretched and aspect-preserving inputs. Letterboxing fixed the desk-image detection miss: keyboard 0.905 and mouse 0.757 versus neither above the 0.50 acceptance threshold when stretched. Person scores were approximately 0.826 in the portrait and 0.646 in the mechanic image. Those detections do not validate live camera alignment or identify a conversation/repair task. See `reference-image-probe.json`.

Physical prerequisite checks confirm Quest 3S, Horizon OS v209, authorized USB debugging, both passthrough camera sources, sufficient battery and storage. The released 0.4.0 APK has now been installed and cold-launched on that headset. The operator allowed camera permission and confirmed a visible notification; app/package diagnostics confirm permission grant, repeated live laptop/monitor detections, stabilized Focus and a Visual delivery. Work-object loss produced Unknown/Peripheral decisions. An initial visual comparison was confounded by trigger-read overrides; the controlled A-only comparison subsequently passed, with operator-confirmed cue/full-message changes and matching Unknown/Peripheral → Focus/Visual notification events. Actual overlay alignment, social/physical ground truth, stereo comfort and sustained performance remain pending. See `evidence/automatic-mode/on-device-2026-11-15.json` for the bounded hardware record. [Automatic rules and hardware procedure](AUTOMATIC-MODE.md) specify the acceptance gates.

## Bare-hand 0.5.0 addendum — validation in progress

The operator reported the social test working, but no audible chime during the physical trial. Inspection found zero Physical decisions and zero SpatialAudio deliveries in that trial, so no chime had been requested. The operator confirmed that bare-hand tracking works in Quest Home. These observations motivated replacing controller motion as the physical-activity input.

Seven Unity MCP EditMode groups now pass, including high-confidence hand-motion processing, stationary jitter, tracking reacquisition, finger-confidence transitions, bimanual evidence without an object class, object/hand association and negative controls. The original model, coordinate, policy and geometry regressions also pass. Results are under `evidence/bare-hands-0.5.0/`. The source-only temporary Android build succeeded with zero errors and 55 warnings after duplicate generated cache files caused packaging failures in the original project. The warnings include deprecated Unity APIs and package shader/compiler diagnostics; they are retained in the versioned evidence. Version 0.5.0/code 4, its v2 signature, ARM64 package and camera/hand-tracking permissions were verified, and the APK was installed on the Quest 3S. Download size is 67,741,311 bytes; the larger Unity build-report total is not the APK file size. Live hand acquisition, natural-task classification and operator-confirmed sound remain pending. See [the full update rules](BARE-HAND-MODE.md).

## Bare-hand 0.5.1 timing correction

The operator reported no chime in 0.5.0. The captured 150-event test sample confirms real hand acquisition (38 two-working-hand events), 13 Physical proposals and a maximum two seconds of hand-motion support, but zero settled Physical states and zero audio requests. Intermittent proposal gaps exposed the overly strict uninterrupted dwell. The 0.5.1 duration window now has a regression that reproduces this failure and verifies a Physical/SpatialAudio result, alongside negative and stale-input controls. All eight software groups pass. Playback and natural-task acceptance remain distinct pending device checks. See `evidence/bare-hands-0.5.1/previous-device-test.json` and the updated bare-hand rules.

## Completed software checks

| Check | Result | Evidence |
|---|---|---|
| Unity import and compilation | Passed | Successful Mac and Android builds |
| Unity MCP EditMode tests | 3 passed, 0 failed/skipped | `evidence/mcp-editmode-results.json` |
| Named policy checks | 40 passed | `evidence/policy-checks.txt` |
| Bounded score examples | 864 combinations passed | Policy test group: context × urgency × angle × coverage × length |
| Spatial room sweep | 486 scenarios passed | `evidence/spatial-sweep.jsonl` and `spatial-summary.txt` |
| Explicit failure cases | 11 passed | `evidence/spatial-failure-checks.txt` |
| Independent saved-score analysis | Minimum selection and all score bounds verified | `evidence/spatial-analysis.json`; reproducible Python analyzer |
| Built desktop player lifecycle | 68 assertions passed | `evidence/runtime/runtime-audit.txt` |
| Diagnostic placement renderings | 9 generated; canvas-center/viewport assertions passed; visually inspected | `evidence/renders/` |
| Built player rendering | Delivery and explicit-read frames saved by final audit | `evidence/runtime/` |
| Rules manual | 7 pages rendered and visually inspected | `docs/Notiflex-Notification-Rules.docx` |
| Android package structure | ARM64 Unity, IL2CPP, OpenXR and OVR native libraries present | `evidence/apk-structure.json` |
| Android signing | APK Signature Scheme v2 verified | `evidence/apk-signature.txt` |
| Android manifest | Correct application ID, SDK levels, VR head tracking and launch activity | `evidence/apk-badging.txt` |

The 486 sweep scenarios combine three rooms, three supplied active contexts, three urgencies, six yaw directions and three head positions. These are spatial-policy inputs, not tests of automatic activity classification. Every eligible candidate must satisfy the implemented face/hazard coverage, viewport, distance and urgency constraints. All sweep scenarios have at least one eligible visual candidate; separate failure tests deliberately remove the entire eligible set. This avoids inferring fallback coverage from a sweep that never enters that branch.

The failure tests include invalid tracking, stale context, a wall blocking every sight volume, head-origin collider overlap, protected face/hazard coverage, permitted audio fallback, conversational deferral, and projected overlay on nearer faces/hazards when environment-depth occlusion is absent. During implementation, the initial-overlap case exposed the limitation of relying only on a shape cast; a separate overlap check now rejects that case. Visual review also exposed undersized distant cues; the renderer and geometric envelope now use the same distance compensation, with two dedicated scale checks.

The desktop audit loads each room and exercises Focus → Visual, Physical → SpatialAudio and Conversation → Peripheral after the context dwell, then explicitly reads and dismisses each request. It verifies a single persistent runtime, notification-free scene entry, valid selected geometry, scene clearing and a Passthrough scene without synthetic room renderers. The final harness also checks replacement and 16-second expiry. A completed audit ends with `COMPLETE`; partial output must never be reported as a pass.

## Cost model and timing interpretation

The implementation enumerates a small candidate set and finds its exact weighted minimum, subject to eligibility. Runtime hysteresis can deliberately retain an eligible incumbent until a challenger improves its score by more than 0.04 after a one-second hold. This is a transparent, bounded optimization over supplied candidates, not a claim of optimal placement throughout continuous space.

The saved Mac EditMode sweep recorded median spatial evaluation **0.02530 ms**, p95 **0.0394 ms** and maximum **5.8848 ms** in that run. These measurements cover only synchronous spatial evaluation on the development computer, with editor/cache effects. They do not measure Quest rendering, frame time, thermal load, tracking latency or passthrough latency. The configured 72 Hz target has a 13.89 ms frame budget that must be measured on device.

Weights and interruption/social functions are implementation hypotheses motivated by the supplied paper. They have not been fitted or validated with participants. The manual distinguishes paper-supported design principles from implementation coefficients and additional constraints.

## Render evidence interpretation

`evidence/renders/` contains diagnostic full-card placement illustrations. They intentionally draw a full card at the selected visual candidate even if the real delivery policy would select audio or a cue. They are not proof of actual modality behavior.

`evidence/runtime/*-delivery.png` records the actual delivery state in the built desktop app. Physical-task audio frames correctly contain no visible notification. `*-read.png` records the explicit reading action. These are offscreen Unity camera renders, not captures of the locked macOS desktop. The diagnostic helper explicitly binds its world-space canvas to the offscreen camera; canvas center and projected corners are checked against the scored placement and viewport inset. Text is readable in the desktop render at the saved resolution; headset optics and peripheral legibility remain untested.

The rooms are authored geometric simulations, with stylized partner proxies and explicit ground truth. They do not reproduce camera sensor noise or demonstrate recognition of real rooms or people.

## Build and integration corrections

- Added required engine modules and corrected SDK API references during compilation.
- Separated `ProtectedRegion` into its own Unity script for valid scene serialization.
- Configured OpenXR, Meta features, Quest 3S/3 targeting and supported passthrough.
- Set linear color space to satisfy Meta OpenXR's Android build requirement.
- Added Unity's required Mac microphone usage-description metadata for the bundled SDK reference. The application does not call microphone capture APIs; the final APK declares no CAMERA or RECORD_AUDIO permission.
- Disabled the unused Meta development agent and scrubbed its automatically injected local credential after the SDK build hook. Checked the APK and Mac package for that local credential; no match was embedded. Machine-local generated settings are excluded from Git.
- Built Android with Passthrough as its initial scene; the Mac simulation starts at Desk.
- Preserved the separate earlier photo-based desktop preview.

## Pending physical Quest 3S acceptance

| Gate | Status |
|---|---|
| Install and launch on the actual headset/OS | Passed: Quest 3S, Android 14, version 0.3.1, November 15 |
| Tracked head pose and correct left/right stereo presentation | Pending hardware |
| Controller mappings and manual task/partner annotations | A/trigger/B/X passed; remaining mappings and annotations pending |
| Passthrough startup, return from immersive scenes and recovery | Startup and transition into authored room passed; return/recovery pending |
| Text size, distance, per-eye field of view and comfort | Pending hardware |
| Audio audibility, direction and acceptable interruption | Pending hardware |
| Tracking-loss hiding and recovery latency | Pending hardware |
| Sustained CPU/GPU frame times and thermal behavior | Pending hardware |
| Real-room behavior with unannotated objects/lighting changes | Pending hardware; no automatic geometry sensing |
| Human noticeability, interruption and social engagement | Requires a user study |

Use `INSTALL-AND-TEST.md` to run these checks and attach device logs. Standard passthrough composition does not mean the app can see raw camera frames or infer arbitrary conversation state. The application runs in its own foreground XR experience; it is not a system-wide overlay for other apps.
