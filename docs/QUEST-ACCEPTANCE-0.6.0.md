# Install and test 0.6.0 on Quest 3S

**Status: new 0.6.0 hardware checks pending.** The user chose to finish the build and test the headset later. Successful 0.5.1 physical/audio trials remain recorded separately. Treat this as a prerelease research build.

## Install and launch

Use a USB data cable and an authorized developer-mode Quest. The new APK upgrades `org.notiflex.prototype`; retain 0.5.1 for rollback. In Meta Quest Developer Hub choose the connected device and install the APK, or use Android platform-tools:

```sh
adb devices
adb install -r Builds/NotiflexVR-0.6.0.apk
adb shell am force-stop org.notiflex.prototype
adb shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER \
  -n org.notiflex.prototype/com.unity3d.player.UnityPlayerGameActivity \
  --ez notiflex_test_notifications true --ez notiflex_local_speech true
```

If multiple Android devices are attached, select the intended serial with `adb -s SERIAL`. Accept USB debugging in the headset when asked. Complete Guardian/boundary setup normally. Automatic scheduled demo messages run every 15 s for 3 minutes in this development build. Ordinary A-button demos remain available. Restart the application to apply new launch extras.

Approve camera, microphone and spatial-data prompts when the system requests them. These are separate permissions. The app may ask the system to scan the room if no saved scene exists; scan walls and important furniture, then return to Notiflex. Quest 3/3S with a compatible Horizon OS and supported camera/depth API is required; developer mode alone does not guarantee every sensor API is ready. Use current device/SDK compatibility information if an API reports unsupported. The application deliberately preserves denied/unavailable states.

To test without microphone, force-stop and relaunch with `--ez notiflex_local_speech false`, or revoke its microphone permission in the device's application permissions. No cloud credentials are needed. To replace/rebuild a room scan, use the Quest's physical-space/room setup, then restart Notiflex. No scan data are uploaded by Notiflex.

## Controls

A sends; trigger reads or advances pages; B dismisses; X cycles Passthrough → Desk → Kitchen → LivingRoom; Y changes urgency; right thumbstick click toggles automatic mode. Manual passthrough annotations retain their older grip controls when automatic mode is off.

For hands only, put controllers down, point the tracked hand pointer at the visible cue/card, release the pinch for at least 0.25 s, then pinch. The card highlights when the tracked hand pointer targets it. The cue expands; further point/pinches advance pages, then dismiss the final page. The card subtly highlights while the tracked pointer targets it; no extra panel or hand mesh is added. There is no gaze-dwell expansion. If the OS hands work but in-app pointing does not, record the test as failed and keep the controller path for comparison. Do not infer success from a passing desktop hit-test.

## Controlled trials and acceptance evidence

Run each trial at least three times, saving the application version, room-scan status, starting condition, expected result, observed result and diagnostic log segment. Document failures as well as successes.

| Trial | Procedure | Expected observations to verify |
|---|---|---|
| Permission denial | Decline microphone or spatial data on a fresh test device/profile | No crash or fake room; corresponding unavailable/permission status; no speech-based conversation claim with mic disabled |
| Desk | View a real monitor/laptop/book steadily, then a blank wall | Focus settles with fresh object evidence; a normal message can change from full card to peripheral as evidence becomes Unknown |
| Physical | Fold paper/cloth with both bare hands visible, no person in view; stop working | Physical support settles during motion; one chime per request when audio allowed; no repeated chime from context fluctuations; recovery after hands leave/reenter view |
| Fine desk motion | Type or make small finger motions near a grounded desk object | Record whether the new fine-hand refinement avoids inappropriate Physical mode; not guaranteed arbitrary typing recognition |
| Silent person | Look toward a nearby silent person | Person protected and audio suppressed; presence alone does not establish Conversation |
| Speech/person | Converse with the visible person; alternate ≥2 s utterances | Speech/person evidence, then possible anonymous switch evidence; peripheral normal notification; log metric versus bearing-only partner localization |
| Negatives | TV/video-call person, recorded speech without visible person, two similarly aligned people | Screen-depth exclusion when geometry supports it; no partner for speech alone; ambiguous groups reported; record false positives/negatives |
| Referent | Send the explicit request below while viewing one laptop; then two similar laptops | Depth-supported track, referent-proximal candidates; ambiguous class-only matching yields no referent; generic A demo remains unbound |
| Spatial placement | Scan a room, trigger messages by a wall/task, turn/translate head slowly | Scene-plane/task-proximal candidates and UUID/track provenance; world position stays fixed or follows its actual track, rather than merely following head rotation |
| Occlusion | Introduce an obstacle/person between viewer and candidate | Depth/map/projected-person rejection is observable; no claim of perfect unsampled geometry protection |
| Hands-only reading | Point/pinch a peripheral cue, repeat after tracking loss; pinch away from card | Valid explicit expansion; no activation from off-target pinches or a held pinch on tracking recovery |
| Longer message | Send a multi-page request and read all pages | No silent content truncation; page counts and advancement work |
| Sustained operation | Run 20–30 minutes, room changes, pause/resume, recenter, lighting changes | Measure frame times, camera/VAD/embedding age, thermal behavior, memory/backlog and crashes; desktop timings cannot pass this gate |

Surface or referent candidates need not win every optimization. Demonstrate their existence, hard-constraint evaluation and winning decisions in suitable geometry; do not force the optimizer to place a card through a person just to show a new feature. If no safe candidate exists, Deferred is an expected result.

## Explicit referent integration example

COCO class 63 is laptop. Class 62 is TV/monitor, 73 is book. This request explicitly makes a laptop its referent; it does not pretend a generic reminder identifies its own object. Launch-time JSON is read once on startup:

```sh
adb shell am force-stop org.notiflex.prototype
adb shell 'am start -n org.notiflex.prototype/com.unity3d.player.UnityPlayerGameActivity --es notiflex_notification '\''{"id":"laptop-demo-1","source":"explicit_test_producer","title":"Laptop update","body":"Check the laptop when you reach a stopping point.","priority":1,"referentClassId":63,"referentTrackId":-1,"demonstration":true}'\'''
```

Keep the laptop in view while sensing warms up. A plain controller A demo remains generic. For a moving object or persistent saved-room object, a producer can supply a fresh session track ID or room anchor UUID through the Unity request API. Track IDs are not stable across restarts/relocalization. Request bodies are not logged; IDs/source/referent status are.

## Retrieve diagnostics

```sh
adb pull /sdcard/Android/data/org.notiflex.prototype/files/ quest-060-evidence/
adb logcat -d -s Unity > quest-060-unity-log.txt
```

Decision JSONL includes selected placement, I/O/S/total scores, all candidate rejection reasons, referent state/uncertainty, room/depth status, microphone status, anonymous counts and interaction evidence. No raw images/audio/embeddings are written by Notiflex's sensor code. Keep spatial/interaction logs private to the study team. SDK/system logs may contain other device metadata.

Record measured results in a new `HARDWARE-TEST-0.6.0.md`; do not copy the 0.5.1 pass labels. If regression blocks your existing demo, reinstall the saved 0.5.1 APK with the appropriate Android downgrade/development workflow or uninstall/reinstall after preserving logs. Uninstalling clears app data and permissions; do that deliberately, not automatically.
