# Quest 3S initial bare-hand acceptance — 0.5.1

Local test date: November 15, 2026 (America/New_York). Device event timestamps use UTC on November 16.

The 0.5.1/code 5 APK was built with zero errors, signed and installed on the same Quest 3S. All eight Unity MCP test groups passed. Its SHA-256 is `cdf9d5a46198c9fe280bb5999ca84f9d76b108607770b95decdca4d511f4ebad`.

## Independent sound check

The development-only sound extra requested one normal chime after VR focus. The operator confirmed hearing it. The matching playback log reports source volume 0.4, listener volume 1 and playing=True. The decision context was Unknown and request ID 0; this was deliberately separate from automatic delivery and is not activity-recognition evidence.

## Automatic bare-hand task

The app was force-stopped and restarted with only scheduled incoming notifications, with the sound diagnostic disabled. The operator was instructed to put both controllers down, fold paper or a cloth with both hands in view for 30–45 s, then rest for 15 s. They confirmed hearing the chime during the task.

The captured 100-event sample contains zero diagnostic-audio events. At 03:40:05 UTC the settled context is Physical, delivery is SpatialAudio, two confident bare hands are in the workspace, speed is 0.278 m/s, hand-motion support is 2 s and context-window support is 1.222 s. No recognized object label is required for this bimanual route. The normal audio request is logged. A later incoming notification is also delivered as Physical/SpatialAudio. At 03:40:19 UTC the context clears to Unknown while two hands remain tracked and motion slows to approximately 0.008 m/s. Ground-truth task/rest timestamps were not independently recorded.

This passes the initial reported two-handed task and audible automatic-delivery check. It does not establish performance across arbitrary activities, one-handed work, occlusion, different lighting, multiple users or prolonged use. Focus and social-presence behavior require regression trials with this new temporal filter. Static-hand, isolated-wave and tracking-loss negative controls pass in software; the headset negative controls are not a complete controlled study.

## Remaining observed behavior

Four audio requests occurred across the sampled trial. One notification replayed its chime when person/audio exclusion toggled delivery between Visual and SpatialAudio. Audio is currently tied to decision changes rather than limited to one chime per request. The exact requests are retained in `evidence/bare-hands-0.5.1/on-device-task-test.json`; they must not be counted as four independent task recognitions. Evaluating and suppressing unnecessary repeats remains a refinement for interruption studies.

No raw camera images or recordings were saved. Evidence consists of derived measurements, decision logs and the operator's report. The app remains a research prototype, not a flawless or general-purpose task recognizer.
