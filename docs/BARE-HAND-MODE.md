# Bare-hand activity update — 0.5.0

## Requirement and previous test

The physical-task mode must work while the user is using bare hands, without controller movement as an activity signal. Version 0.4.0 did not meet that requirement. Its automatic log contained no Physical or SpatialAudio decisions during the reported trial, so a chime was never requested. Some subsequent events used manual annotations or synthetic-room context. The Quest's media stream was not muted (speaker 6/15). The operator reported that the social-presence test worked and confirmed that bare-hand pointers work in Quest Home.

## Implemented sensing

Version 0.5.0 declares ControllersAndHands support, selects the OpenXR hand skeleton and starts Meta's simultaneous hands/controllers mode. Controller-driven hand poses are disabled. No hand/controller meshes or extra headset panels are added; the view remains passthrough plus notifications.

`BareHandActivity` reads Meta's real hand state through `OVRPlugin.GetHandState`. It requires tracked, high-confidence hands, rejects system gestures and rejects held-controller/inferred poses when reported by the SDK. SDK held/inferred signals have limitations; this is not a claim that the SDK perfectly distinguishes every grasp. Controller velocity is no longer read by the camera context pipeline. Real hand tracking is produced by the Quest's own on-device vision system; RGB detections continue through the bundled local model.

Wrist positions are transformed from tracking space to world space. Index/thumb articulation contributes only while both fingers have high confidence; confidence transitions do not become movement. Position is low-pass filtered with a 0.06 s time constant before differentiation. Velocity magnitude is smoothed with a 0.25 s time constant. Gaps over 0.2 s, nonfinite data, wrist jumps over 0.20 m per sample or motion over 2 m/s reset the history instead of generating activity. Tracking reacquisition starts at zero velocity. Duplicate native timestamps are tolerated for at most 0.2 s.

A working hand must be 0.15–1.25 m from the head, 0.10–0.95 m below it, and forward of the head view by at least 0.08 m. Two working hands are bimanual when separated by less than 0.65 m. Motion support accumulates while speed is at least 0.07 m/s, up to 2 s, and decays at twice real time when motion stops. Physical proposals require at least 1 s of support and speed at least 0.10 m/s.

Two routes supply physical evidence:

- **Object-associated manipulation:** a supported work/manipulable object and a real active hand align within 25 degrees; object bearing is within 70 degrees of head forward. Evidence is `objectScore × clamp01(handSpeed / 0.16)`.
- **Bimanual manipulation:** two real hands work together in the lower workspace. It does not require a recognized COCO object. Evidence is `0.85 × clamp01(handSpeed / 0.16)`.

Both routes retain the 0.55 decision threshold and 1.2 s context dwell. Sustained hand movement suppresses the focused-viewing proposal. Social presence still takes precedence. Hands-busy remains conservatively latched while the Physical context is retained by dwell, so a brief hand occlusion cannot promote an audio cue to a full card. Missing/stale camera or head pose still defers delivery.

These are explicit manipulation heuristics, not a trained classifier for every named activity. They can miss occluded, subtle or unusual tasks and can confuse other sustained hand motions with manipulation. No metric hand-object contact is inferred from camera bearing alone. Existing interruption/occlusion/social placement weights and separate delivery selection remain unchanged.

## Controls and sound

The standard A/B/trigger controls remain available. In automatic mode, side grips no longer silently switch to manual annotations. Switch to manual with right stick click before using grip annotations. Left stick click still explicitly selects manual activity.

The 250 ms chime has increased source gain and explicit linear attenuation (1–6 m) to improve audibility. `audio_play_requested` and `NOTIFLEX_AUDIO` record requests, source/listener volume and playback state. A request log is not proof that the operator heard the sound. System volume remains the user's setting.

## Hands-free test notifications

This development build supports an explicitly enabled incoming-notification test. It starts with a 12 s delay, sends every 15 s while the app has VR focus and fresh automatic camera observations, and stops after 180 s of app time. It adds no visible controls and is disabled for ordinary launches. It does not identify real external phone/calendar notifications.

To start the test on a connected Quest:

```sh
adb shell am force-stop org.notiflex.prototype
adb shell am start -n org.notiflex.prototype/com.unity3d.player.UnityPlayerGameActivity --ez notiflex_test_notifications true
```

For an ordinary launch with the schedule disabled, force-stop and launch the activity without that extra. Camera permission persists across app updates/restarts.

Put both controllers down. Keep both hands visible below the headset and perform a natural two-handed task such as folding a cloth or sheet of paper for 20–30 s, keeping the head reasonably steady. Then rest both hands. Verify confident bare-hand counts, sustained motion, Physical/SpatialAudio selection, an audio request and operator-confirmed audibility. Repeat with stationary hands and an isolated hand wave; those must not establish object-free physical activity. A second test can stir an empty bowl or manipulate a detected object with one bare hand.

The automatic scheduler avoids picking up a controller to send a test notification, which would otherwise change the sensing conditions. If another person is detected, automatic audio remains suppressed by the existing policy.

## Validation status

Seven software test groups cover the prior policy/model/geometry tests plus hand-motion noise, tracking recovery, finger-confidence changes, bimanual activity without an object label, object/hand association, brief motion and missing tracking. The clean Android build succeeded with zero errors; package signature/manifest/version checks passed and 0.5.0 is installed on Quest 3S. Live hand acquisition, natural-task recognition and audible notification behavior must be verified on the headset before reporting the physical mode as passed. No camera recordings are required for these checks.

Official SDK references: [Multimodal hands/controllers](https://developers.meta.com/vr/documentation/unity/unity-multimodal/), [hand tracking overview](https://developers.meta.com/vr/documentation/unity/unity-handtracking-overview/). Local implementation is verified against the pinned Meta XR Core SDK 209.0.0 source.

## Build isolation

The working checkout's generated Unity cache accumulated numbered duplicate DLLs and Android resources. Those files caused native compilation and packaging failures. The release candidate is therefore built from the tracked Assets, Packages and ProjectSettings plus the new hand-tracking source in a temporary project outside Documents, without copying Library, Temp, build output or optional machine-local agent settings. The pinned package manifest restores dependencies. Source hashes and the final build summary are retained in `evidence/bare-hands-0.5.0/`. An APK is accepted only after a successful build with zero reported errors, package/version checks and signature verification.
