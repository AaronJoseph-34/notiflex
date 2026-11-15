# Initial Quest 3S camera test — November 15, 2026

The released Notiflex 0.4.0 APK installed and cold-launched successfully on the connected Quest 3S. The operator allowed camera permission and Android reported HEADSET_CAMERA granted. The local model produced repeated live laptop/monitor detections; Notiflex stabilized in Focus and displayed a notification confirmed by the operator.

A controlled A-only comparison passed: while facing away, the notification showed “Update available / Trigger to read”; after returning to the screen it showed the full update message. The app log records Normal-priority notifications changing from Unknown/Peripheral to Focus/Visual. The initial no-change report was confounded by trigger-read events, which intentionally expand a cue regardless of context. No code change or APK replacement was needed for this comparison.

Steady sampled inference elapsed time was approximately 250–278 ms in this short session. It spans multiple rendered frames and does not measure headset rendering frame time. A first-use warmup result and some motion-related stale observations were discarded. This is initial functional evidence, not a sustained benchmark or proof of comfortable 72 Hz rendering.

Social-presence ground truth, physical-task behavior, exact projected alignment, stereo legibility, permission denial, pause/resume, manual/automatic toggles and sustained thermal/performance acceptance remain outstanding. Detected person classes alone are not evidence of a conversation.

Only aggregate derived observations are saved in the repository. Raw diagnostic logs remain local; no camera images were recorded. See `evidence/automatic-mode/on-device-2026-11-15.json` and `AUTOMATIC-MODE.md` for evidence and rules.
