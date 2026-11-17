# 0.6.0 evidence and limits

Final Unity MCP EditMode run: **16 groups passed, 0 failed, 0 skipped**. Tests include the existing three-room spatial sweep and geometry/tracking failures; new tests cover assignment/Kalman/referent ambiguity, depth robustness, world candidates, speech/person/screen fusion, controls, DSP, actual bundled speech inference and synthetic full-pipeline replay.

`synthetic-pipeline.jsonl` contains nine explicitly synthetic decisions (three contexts × three urgency levels), including all candidate scores. An independent audit checked bounded I/O/S/total scores and eligible minimum selection. These are controlled inputs, not live camera observations.

`unity-speech-contract.txt` times model load/first CPU execution on the desktop and compares synthetic outputs to the original models. These timings are not headset frame timing. `speech-model-conversion.json` pins original models/tool versions and differential errors; `source-and-model-sha256.json` records matching source/models used in the isolated build project. Kaldi-native-fbank supplies the independent feature reference. Synthetic inputs do not measure speech, diarization or context-recognition accuracy.

`calibration-tool-selftest.json` is synthetic-only; it cannot enable participant-fit status. No participant calibration data were collected.

`build-summary.json` records Android build outcome, warnings and actual APK bytes. Unity's `buildReportTotalBytes` also counts build/debug products and is not the installable APK size. The final build reports **zero errors and 17 warnings**, preserved in `build-warnings.txt`: SDK compatibility/OpenXR notices and deprecated APIs. The actual package uses the requested GameActivity; a Meta setup checker still recommends that setting. The OpenXR notice says a feature's requested older patch version is ignored. These notices are not a substitute for device startup/interaction testing. Compiler deprecations do not fail this build.

`apk-badging.txt`, `apk-permissions.txt`, `apk-signature.txt`, `package-checks.json` and `package.json` record package identity, ARM64 ABI, V2 signature, sensor permissions, prompt policy, APK hash/size and upgrade certificate checks. No eye-tracking permission is added. APK is a development-signed research prerelease.

**New hardware acceptance: pending.** The operator chose to finish the build and test the headset later. No new room-scan/depth/microphone/pinch accuracy or sustained-performance result is claimed. Follow `docs/QUEST-ACCEPTANCE-0.6.0.md`; retain earlier hardware records under their original versions. There are no new participant outcomes or novelty claims in this evidence folder.
