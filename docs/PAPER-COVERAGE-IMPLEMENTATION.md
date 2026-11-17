# Paper coverage implementation — 0.6.0

This upgrade implements the missing software paths from the supplied manuscript. Implementation, device acceptance and scientific validation are separate gates. No empirical engagement calibration or claim of research novelty can be manufactured by adding code.

## Work plan and acceptance gates

1. **Spatial sensing:** load the device's saved MRUK room/planes/mesh with spatial-data permission; use the supported environment raycaster for live depth. Preserve unavailable/denied/stale states. Never substitute authored rooms for device observations.
2. **Referents and tracking:** give requests explicit semantic/track/scene-anchor metadata; associate detections over time, fuse depth-supported 3D positions with uncertainty, and reject ambiguous associations. A generic message has no invented referent.
3. **World and task placement:** generate candidates beside grounded referents/partners and on eligible mapped surfaces, retain scene-anchor-relative positions, and check actual room/depth visibility. Maintain a conservative head-relative fallback.
4. **Context and conversation:** fuse task-object, head, real-hand, person and optional local speech evidence over time. Report speech/person/interaction evidence distinctly. A single microphone cannot establish mutual conversation or participant identity with certainty.
5. **Presentation and social cost:** use presentation-specific exposure durations, partner-relative shifts and configurable calibration parameters. Supply a reproducible calibration/evaluation workflow; until human data exist, report a heuristic risk score rather than a validated probability.
6. **Interaction and verification:** support controller-free explicit reading, suppress duplicate audio per request, regress existing behavior, test synthetic spatial/tracking/conversation failure cases, build a signed Android artifact, and document exact device acceptance steps.

The headset was not USB-connected at the start of this work. Real scene permission/scan, depth, microphone, alignment, anchoring, interaction and sustained-performance checks therefore remain device gates until observed. Existing successful 0.5.1 hardware trials do not validate new 0.6.0 features.

## Scientific boundary

The supplied paper specifies a framework, not fitted coefficients or a validated arbitrary-task classifier. The implementation must expose evidence provenance and uncertainty. Novelty requires a separate prior-work comparison and evaluation. The social-cost calibration tooling can be implemented now; empirical calibration requires consented participant outcomes and held-out evaluation later.

## Final coverage matrix

| Paper element | 0.6.0 implementation | Remaining gate/limit |
|---|---|---|
| Focus/physical/social context | Object/head/bare-hand fusion, fine-hand refinement, speech/person interaction estimate, temporal support | Bounded proxies; arbitrary actions and real conversational intent are not solved; headset accuracy evaluation pending |
| Referent identification/localization | Explicit request metadata, class/session-track/scene UUID binding, ambiguity rejection, robust depth fusion, Kalman state, Hungarian assignment | Producer must provide semantics; selected COCO classes; approximate 3D location/normal rather than full semantic 6DoF pose; tracking accuracy pending |
| Partner localization/conversation | Fresh person tracks, optional depth-supported position, local Silero speech activity and WeSpeaker anonymous turn evidence, screen/group ambiguity handling | No named identity, certified diarization, mutual-gaze or confirmed conversation; new microphone/depth path untested on device |
| Scene reconstruction/planes | Actual MRUK saved-room anchors/planes/bounds/global mesh and supported live depth raycaster; real geometry rejection | Room scan/permission required; mapped V1 geometry is incomplete/stale; no claim of continuous dense reconstruction of every object |
| World/task-proximal placement | Scene-anchor-local surface centers retained per request; explicit referent and metric partner candidates; actual orientation/envelope checks | MRUK alignment/stability pending hardware test; session object tracks are not persistent spatial anchors across restarts |
| I/O/S normalization, urgency/context priorities | Auditable [0,1] costs, explicit weight table, hard constraints, minimum eligible cost and incumbent hysteresis | Engineering hypotheses, not fitted optimal weights |
| Separate modality stage | Visual candidate scored first; context/urgency policy selects visual/peripheral/spatial audio/deferred; actual-channel exposure logged | Human effects, audio noticeability and tradeoffs require study |
| Presentation/reading/social disengagement | Channel exposure, reading duration, view incidence, head/partner shifts and interaction support; configurable monotone fitted-profile path | No participant data: empirical calibration remains missing by evidence, not disguised as complete |
| Continuous adaptation and interaction | Freshness and pose gates, one chime/request, explicit tracked pointer + pinch, pagination, original controller path | Device performance, robustness, permission sequence and hand usability pending |
| Evaluation | 16 Unity test groups, three authored rooms/original spatial sweep, synthetic full-pipeline replay, independent DSP/model parity, Android build | New Quest trials and participant comparisons are not performed; no novelty proof |

See [current complete method](CURRENT-METHOD-0.6.0.md), [device procedure](QUEST-ACCEPTANCE-0.6.0.md) and [calibration protocol](SOCIAL-CALIBRATION.md). The implementation expands testable capability without rewriting unperformed experiments as results.

## Reproduce model preparation and regression

The serialized `.sentis` files are bundled for normal builds. To reproduce their preparation, use Python with the exact NumPy/ONNX/ONNX Runtime versions recorded in the conversion manifest, plus `kaldi-native-fbank==1.23.1` for independent reference features:

```sh
python tools/prepare_speech_models.py --cache /tmp/notiflex-models \
  --unity-project UnityProject --evidence evidence/paper-coverage-0.6.0
python tools/prepare_speech_reference.py --cache /tmp/notiflex-models \
  --output UnityProject/Assets/Notiflex/Editor/Fixtures/speech-reference.json
```

The first script verifies pinned source SHA-256 values and places the specialized ONNX files in an Editor-only staging directory. Import through Unity, run `Notiflex.VR.Editor.SpeechModelBuilder.Convert`, then run all EditMode tests through the Unity Test Runner or Unity MCP. Original ONNX files are not required at runtime and should not be copied into Resources. The speech tests execute the actual bundled models, compare outputs against original ONNX Runtime predictions on synthetic inputs, and compare 80-bin feature extraction against Kaldi. Passing these contracts does not measure live speech detection or speaker-identification accuracy.

Use a source-only Unity checkout with its own Library cache. This workspace's old cache contained duplicated generated assemblies/resources; the successful builds use an isolated clean-cache project with matching source hashes. Do not copy the old Library into a new checkout. Generated API models/scene assets remain pinned in source; model downloads and SDK package restoration are development-time network operations, not cloud runtime inference.
