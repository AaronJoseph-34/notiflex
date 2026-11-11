# Notiflex VR validation record

Version 0.3.0 · November 11, 2026 · Mac development host · Unity 6000.4.7f1

The Android APK and Mac player were built successfully. Software checks establish implementation consistency and interactive desktop behavior. No Quest was attached (`adb devices` returned no devices), so this is a headset-installable research prototype with physical acceptance still pending. It is not a certification of flawless VR behavior or a validation of human interruption costs.

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
| Install and launch on the actual headset/OS | Pending hardware |
| Tracked head pose and correct left/right stereo presentation | Pending hardware |
| Controller mappings and manual task/partner annotations | Pending hardware |
| Passthrough startup, return from immersive scenes and recovery | Pending hardware |
| Text size, distance, per-eye field of view and comfort | Pending hardware |
| Audio audibility, direction and acceptable interruption | Pending hardware |
| Tracking-loss hiding and recovery latency | Pending hardware |
| Sustained CPU/GPU frame times and thermal behavior | Pending hardware |
| Real-room behavior with unannotated objects/lighting changes | Pending hardware; no automatic geometry sensing |
| Human noticeability, interruption and social engagement | Requires a user study |

Use `INSTALL-AND-TEST.md` to run these checks and attach device logs. Standard passthrough composition does not mean the app can see raw camera frames or infer arbitrary conversation state. The application runs in its own foreground XR experience; it is not a system-wide overlay for other apps.
