# Install and test Notiflex VR

## Desktop preview

Open `NotiflexVR.app` from the delivered Mac archive. This is the new Unity 3D test platform; the earlier photo-based Notiflex app is a separate preview and has not been replaced.

| Input | Action |
|---|---|
| 1 / 2 / 3 / 4 | Desk / Kitchen / Living room / Passthrough setup |
| Arrow keys | Turn the simulated head |
| W A S D | Move the simulated head through the room |
| Space | Send one demonstration notification |
| E | Read/expand the active notification |
| Enter | Dismiss |
| C | Cycle supplied activity: Focus → Physical → Conversation → Unknown |
| U | Cycle urgency: Normal → Urgent → Low |
| Tab | Show/hide the desktop decision inspector |

Wait at least 1.2 seconds after changing activity or head direction for context dwell. Conversation requires looking generally toward the supplied partner (within 65°); Focus requires task alignment within 24°. A mode change alone does not override these conditions. Desktop Passthrough shows an empty setup view because it has no headset cameras.

## Quest 3S installation

Use a Quest developer account and enable Developer Mode through Meta's current device setup flow. Connect the awake headset to the computer with a USB data cable, put it on and accept its USB debugging prompt. No eye tracking or microphone permission is needed by this prototype.

With Android platform-tools installed, run:

```sh
adb devices
adb install -r /absolute/path/to/NotiflexVR.apk
adb shell am start -n org.notiflex.prototype/com.unity3d.player.UnityPlayerGameActivity
```

`adb devices` must show the device as `device`, not `unauthorized`. Alternatively, install the APK through Meta Quest Developer Hub's device application tools. Launch **Notiflex VR** from the headset's Unknown Sources/developer applications list. The Android package starts in **Passthrough**. Keep normal headset boundaries enabled.

The app requests OS-composited passthrough; it does not request access to raw RGB camera frames. Live passthrough and stereo tracking must be confirmed on hardware. The installation commands can only be completed when a headset is available.

## Quest controller controls

| Input | Action |
|---|---|
| Right A | Send demonstration notification |
| Right B | Dismiss |
| Right trigger | Read/expand |
| Left X | Cycle Passthrough → Desk → Kitchen → Living room |
| Left Y | Cycle urgency: Normal → Urgent → Low |
| Left stick click | Cycle supplied activity: Focus → Physical → Conversation → Unknown |
| Right stick up/down in Passthrough | Change annotation distance, 0.8–3.2 m; initial 2 m |
| Right grip in Passthrough | Replace task annotation along right controller forward ray |
| Left grip in Passthrough | Replace partner annotation along left controller forward ray |

Controller annotations use an operator-selected ray distance. They are not measured surface hits. Set the distance deliberately and point toward the intended task/partner before pressing grip. There is no persistent spatial anchor mapping across app sessions. In live mode only the notification appears; annotations have no visible markers or headset control panel. Supply activity using the controller, then use the desktop inspector or logs to verify decisions.

## Demonstration sequence

1. **Focused work:** choose Desk, keep head toward the monitor, supply Focus, wait 1.5 seconds, press A/Space. Normal priority should display a full panel at a low-cost eligible location. Turn the head to change costs and observe the logged selection.
2. **Physical task:** choose Kitchen, supply Physical, wait 1.5 seconds, send. Normal priority with audio enabled produces a positional chime and no panel. Trigger/E requests the visual card if geometry is eligible. The supplied hot-surface region constrains visual overlap.
3. **Conversation:** choose Living room, face the partner proxy, supply Conversation, wait 1.5 seconds, send. Normal priority produces a compact peripheral cue, avoiding the protected face. Trigger/E expands it. Turn away beyond 65° to observe context returning to Unknown after dwell.
4. **Urgency:** change to Urgent and send again. A full visual card requires an eligible location within 20°; hard face/hazard/geometry constraints remain active. If none exists, audio is allowed only outside conversation/speaking; otherwise defer.
5. **Real room:** return to Passthrough, annotate the task/partner at chosen distances, supply the appropriate activity and repeat. Verify that no synthetic furniture or desktop HUD is present.
6. **Lifecycle:** send twice (replacement), dismiss, send and wait over 16 seconds (expiry), then change scene while a card is active (clear). A reading request restarts a 16-second window.

The synthetic rooms are geometric test scenes. Their colliders and annotations are known ground truth; they do not simulate camera noise, object recognition, Quest optics or passthrough latency.

## Retrieve decision logs

Logs contain UTC timestamps, source provenance, context, urgency, channel, selected candidate, pose validity and every candidate's cost components and rejection reason. They contain no camera or microphone recordings.

- macOS: `~/Library/Application Support/Notiflex Research/Notiflex VR/decisions-*.jsonl`
- Quest development package:

```sh
adb shell run-as org.notiflex.prototype ls /sdcard/Android/data/org.notiflex.prototype/files
adb pull /sdcard/Android/data/org.notiflex.prototype/files ./quest-session
adb logcat -d -s Unity > quest-unity-log.txt
```

Android file access varies with OS configuration. If direct `adb pull` is denied, use the developer hub's device file tools or inspect the development app's storage through `run-as`; preserve the original files and record device/OS versions alongside the session.

## Hardware acceptance record

Record the headset model/OS, app version, date and operator for each check. Confirm launch, both-eye rendering, controller mappings, passthrough startup and scene transitions, annotation location, text legibility at selected distance, chime audibility/direction, tracking-loss hiding, recovery, and 10-minute stability. Profile CPU/GPU frame times on the device, targeting the configured 72 Hz budget of 13.89 ms/frame. Do not equate editor evaluation timings with headset frame rate. Run in bright and dim rooms and with seated and standing head heights. These checks remain pending until the APK is installed on a physical headset.
