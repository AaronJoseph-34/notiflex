# Notiflex VR research prototype

A Unity application for studying context-aware XR notification placement. It includes three authored 3D test rooms—Desk, Kitchen and LivingRoom—and a separate Quest Passthrough scene that renders notifications over the live room. The headset view has no desktop control panel.

The system scores eligible visual candidates for interruption, projected occlusion and social disengagement, selects a position, then separately selects full visual, peripheral cue, positional chime or deferred delivery. Head direction is an attention proxy. Version 0.4.0 added an experimental on-device camera mode for live Passthrough: object detections and head/controller motion propose focused viewing, physical-task and social-presence contexts. Version 0.5.0 replaces the controller-motion activity proxy with real bare-hand tracking and object-associated or bimanual manipulation evidence. Authored simulations and manual mode retain supplied context. These are heuristic proxies; speech/conversation recognition, eye tracking and metric room reconstruction are not implemented. See [automatic-mode rules and acceptance status](docs/AUTOMATIC-MODE.md).

Download the [experimental 0.4.0 Quest APK](https://github.com/AaronJoseph-34/notiflex/releases/tag/v0.4.0). Its camera permission, live local detections, focused-work estimate and visible notification passed an initial Quest 3S test. The controlled short-cue/full-card comparison also passed; broader hardware acceptance remains incomplete. See the automatic-mode record. The [0.3.1 release](https://github.com/AaronJoseph-34/notiflex/releases/tag/v0.3.1) is the prior headset-tested version for passthrough and A/trigger/B/X controls. The existing Mac simulation and original Word manual remain in [0.3.0](https://github.com/AaronJoseph-34/notiflex/releases/tag/v0.3.0).

The [0.5.0 bare-hand test build](https://github.com/AaronJoseph-34/notiflex/releases/tag/v0.5.0) is built, signed and installed on Quest 3S. All seven software test groups pass. The [bare-hand update rules and hands-free test procedure](docs/BARE-HAND-MODE.md) describe the replacement physical estimator. Hardware acceptance for that replacement is pending.

## Start here

- [Automatic camera mode, complete rules and acceptance steps](docs/AUTOMATIC-MODE.md)
- [Quest 3S capabilities and implementation boundaries](docs/PLATFORM.md)
- [Install, controls and test scenarios](docs/INSTALL-AND-TEST.md)
- [Complete notification rules](docs/NOTIFICATION-RULES.md) · [Word manual](docs/Notiflex-Notification-Rules.docx)
- [Validation report and hardware gates](docs/VALIDATION.md)
- [Implementation plan](docs/IMPLEMENTATION-PLAN.md)
- [Evidence](evidence/): test results, candidate scores and diagnostic renderings

## Build from source

Open `UnityProject` with **Unity 6000.4.7f1** and install its Android Build Support, SDK/NDK and OpenJDK modules. The project restores pinned OpenXR 1.19.0, Meta XR Core/MRUK 209.0.0, Unity Inference Engine 2.2.1 and Unity MCP v10.3.0 packages. Meta's registry and GitHub need internet access on first import.

Unity menu **Notiflex → Build authored environments** regenerates the four scenes. **Notiflex → Configure Quest build** sets Android ARM64/IL2CPP, OpenGLES3, SDK minimum 32/target 35, OpenXR and Meta passthrough support. Regenerating scenes overwrites their authored contents; keep scene edits in source control first.

From a terminal, with `UNITY` pointing to the Unity executable:

```sh
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD/UnityProject" \
  -executeMethod Notiflex.VR.Editor.PlatformBuilder.BuildMac -logFile mac-build.log
"$UNITY" -batchmode -nographics -quit -buildTarget Android -projectPath "$PWD/UnityProject" \
  -executeMethod Notiflex.VR.Editor.PlatformBuilder.BuildAndroid -logFile android-build.log
```

Outputs are `Builds/NotiflexVR.app` and `Builds/NotiflexVR.apk` beside this README. Both are development builds for research diagnostics; the APK uses a development signing key. The Mac package is a local prototype, not a notarized App Store distribution.

## Tests and Unity MCP

The Unity Test Runner has seven EditMode groups: policy/normalized costs, 486 room/viewpoint/context/urgency scenarios, explicit geometry/tracking failures, automatic evidence/freshness, detection coordinate/NMS/person protection, and execution of the actual bundled detector, and bare-hand motion/confidence/recovery. Select **Window → General → Test Runner → EditMode → Run All**, or use Unity's `-runTests -testPlatform EditMode -testResults results.xml` command-line options.

Unity MCP was used for scene authoring/configuration, hierarchy inspection and test execution. To reproduce: start the installed Unity MCP bridge using its supported editor UI or `MCPForUnity.Editor.McpCiBoot.StartStdioForCi`, install the matching `mcp-for-unity` Python server, then use `tools/mcp_call.py` with `UNITY_MCP_SERVER` set to that executable. Use only trusted local clients; no public bridge service is required.

The desktop player's opt-in lifecycle audit runs with `--notiflex-audit`; `NOTIFLEX_AUDIT_OUTPUT` chooses the output directory. It loads all scenes, exercises context dwell, delivery, reading and dismissal, and saves offscreen frames. Ordinary launches never activate the audit.

## Technical scope

`PlacementPolicy` contains pure rules and profiles; `SpatialEvaluator` evaluates the finite candidate set and geometric constraints; `ContextDwell` filters activity changes; `NotiflexRuntime` integrates tracked/simulated head pose, controller input, lifecycle and JSONL diagnostics; `NotificationView` draws a world-space panel. The full equations, units, thresholds, precedence and limitations are in the rules manual.

These cost functions are auditable implementation hypotheses based on the supplied paper. Software tests verify their implementation; they do not establish empirically optimal weights or human performance. Physical Quest 3S acceptance remains a separate test step.

![Desktop player focused-work example](evidence/runtime/Desk-Focus-delivery.png)

Desktop simulation evidence; live-room passthrough requires the Quest package and physical headset acceptance.
