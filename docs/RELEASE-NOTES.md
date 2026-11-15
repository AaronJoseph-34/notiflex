# Notiflex VR 0.4.0 — experimental automatic camera mode

Adds local Quest camera acquisition, a bundled object detector and explicit context heuristics for focused viewing, controller-assisted physical-task activity and possible social interaction. Existing normalized placement costs and separate delivery selection remain intact. No cloud service or microphone capture is used.

Automatic mode starts in live Passthrough after the camera permission dialog. Right stick click toggles manual/automatic; left stick click or either grip annotation returns to manual. Authored rooms remain supplied-context simulations. The headset view retains only the room and notification.

Six Unity MCP test groups passed. Offline tests on the three supplied photos exposed a desk detection miss caused by stretched input; aspect-preserving letterboxing recovered keyboard/mouse detections. Coordinate inversion and person-protection tests passed. These are software results; live camera behavior and performance require hardware acceptance before making reliability claims.

The physical Quest 3S prerequisite checks passed on Horizon OS v209. This version's camera permission, live frames, detections, automatic switching, placement alignment and thermal performance remain pending. The earlier 0.3.1 A/trigger/B/X controls were confirmed on hardware.

This is a research development build. Social interaction is inferred from a large aligned person, not detected speech or a confirmed conversation. Physical mode currently requires tracked-controller motion with a supported visible object; it is not a finished hands-free action recognizer. The camera pipeline has no metric room reconstruction or automatic hazard recognition. See AUTOMATIC-MODE.md for every coefficient, limitation and test step.

# Notiflex VR 0.3.1 — Quest controller fix

Fixes unresponsive A/B/X/Y, trigger and grip polling by reading Meta XR Core OVRInput after its frame update. Adds controller connection/focus and action diagnostics, and corrects controller annotation positions to use the rig tracking space. Placement scores and modality rules are unchanged.

Built, signed, installed and launched on a physical Quest 3S (Android 14). The operator confirmed passthrough, A to send, right trigger to expand, B to dismiss and X to switch scenes; device logs confirm these actions. All three Unity MCP EditMode regression groups passed. Other mappings, annotation accuracy, stereo comfort, audio direction and sustained performance remain acceptance checks. Context and annotations remain supplied manually; automatic camera/activity/conversation sensing is not implemented.

This update includes the Quest APK and controller fix record. Use the v0.3.0 release for the existing Mac simulation and complete rules manual. The APK remains a research development build.

## Original 0.3.0 release

Notiflex VR 0.3.0 is a research prototype with installable Quest and universal Mac packages.

Includes authored Desk, Kitchen and LivingRoom scenes; a notification-only live Passthrough scene; head-direction-based attention proxy; normalized interruption, projected occlusion and social cost scoring; explicit context/urgency profiles; separate visual, peripheral, positional-chime and deferred delivery; controller annotations; lifecycle handling; and candidate-level decision logs.

Validation: 3 Unity MCP test groups passed, 40 named policy checks, 864 bounded-score combinations, 486 spatial scenarios, 11 failure checks and 68 built-player assertions. Android signing/native-library/manifest checks and Mac signature/archive checks passed. The Word manual documents 35 rules and all coefficients and limitations.

Physical Quest 3S acceptance is pending: no headset was connected. Stereo tracking, passthrough, controller behavior, comfort and sustained device performance require testing on hardware. Activity modes and task/partner annotations are supplied explicitly; no automatic camera understanding, conversation detector or eye tracking is implemented. The application runs as its own foreground XR experience.

Download the APK for Quest, the Mac ZIP for a desktop test, and the rules manual. See INSTALL-AND-TEST.md and VALIDATION.md for controls, scenarios and hardware checks. Packages are development builds; the Mac app is locally signed and not notarized. SHA256SUMS.txt identifies the exact files.
