# Install and test Notiflex VR

Version note: this document records an earlier implementation. See [the current 0.6.0 method](CURRENT-METHOD-0.6.0.md) and [new device acceptance steps](QUEST-ACCEPTANCE-0.6.0.md) for the speech, depth, tracking and interaction upgrade. New hardware results remain pending.

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

Use a Quest developer account and enable Developer Mode through Meta's current device setup flow. Connect the awake headset to the computer with a USB data cable, put it on and accept its USB debugging prompt. No eye tracking or microphone permission is needed. Version 0.4.0 automatic mode additionally requests headset camera permission inside the app.

With Android platform-tools installed, run:

```sh
adb devices
adb install -r /absolute/path/to/NotiflexVR.apk
adb shell am start -n org.notiflex.prototype/com.unity3d.player.UnityPlayerGameActivity
```

`adb devices` must show the device as `device`, not `unauthorized`. Alternatively, install the APK through Meta Quest Developer Hub's device application tools. Launch **Notiflex VR** from the headset's Unknown Sources/developer applications list. The Android package starts in **Passthrough**. Keep normal headset boundaries enabled.

The app uses OS-composited passthrough. Version 0.4.0 also requests access to the left RGB camera for local object inference in automatic mode. Accept the OS camera dialog to test it. The new camera mode still needs hardware acceptance; the earlier 0.3.1 passthrough and basic controls were confirmed on a Quest 3S.

## Quest controller controls

| Input | Action |
|---|---|
| Right A | Send demonstration notification |
| Right B | Dismiss |
| Right trigger | Read/expand |
| Left X | Cycle Passthrough → Desk → Kitchen → Living room |
| Left Y | Cycle urgency: Normal → Urgent → Low |
| Left stick click | Switch to manual and cycle supplied activity: Focus → Physical → Conversation → Unknown |
| Right stick click in Passthrough (0.4.0) | Toggle automatic camera/manual context |
| Right stick up/down in Passthrough | Change annotation distance, 0.8–3.2 m; initial 2 m |
| Right grip in Passthrough | Replace task annotation along right controller forward ray |
| Left grip in Passthrough | Replace partner annotation along left controller forward ray |

Controller annotations use an operator-selected ray distance. They are not measured surface hits. Set the distance deliberately and point toward the intended task/partner before pressing grip. There is no persistent spatial anchor mapping across app sessions. In live mode only the notification appears; annotations have no visible markers or headset control panel. From 0.5.0, switch to manual with right stick click before grip annotations. Grips are ignored in automatic mode. In automatic mode, local detections provide context proxies and protected angular regions; use logs to verify decisions. See [automatic rules and tests](AUTOMATIC-MODE.md).

Press A to create a notification before trying the right trigger. The trigger expands an active request; it does not create one. Virtual controller models are not drawn. Version 0.3.1 logs received actions as `NOTIFLEX_INPUT` and reports controller connection, position-tracking and VR focus changes as `NOTIFLEX_INPUT_STATUS` in Unity logcat. Return from the Quest system menu to the app before testing; controls intentionally do nothing while the menu owns focus.

## Demonstration sequence

For these controlled simulation tests, use supplied activity as below. For automatic real-room checks, follow AUTOMATIC-MODE.md.

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
adb shell ls /sdcard/Android/data/org.notiflex.prototype/files
adb pull /sdcard/Android/data/org.notiflex.prototype/files ./quest-session
adb logcat -d -s Unity > quest-unity-log.txt
```

Android file access varies with OS configuration. On the tested Quest 3S Android 14 setup, ordinary `adb shell ls` and `adb pull` work for this external app directory; `run-as` cannot list it. If direct access is denied, use the developer hub's device file tools. Preserve original files and record device/OS versions alongside the session.

## Hardware acceptance record

Record the headset model/OS, app version, date and operator for each check. Confirm launch, both-eye rendering, controller mappings, passthrough startup and scene transitions, annotation location, text legibility at selected distance, chime audibility/direction, tracking-loss hiding, recovery, and 10-minute stability. Profile CPU/GPU frame times on the device, targeting the configured 72 Hz budget of 13.89 ms/frame. Do not equate editor evaluation timings with headset frame rate. Run in bright and dim rooms and with seated and standing head heights. These checks remain pending until the APK is installed on a physical headset.

## Bare-hand physical activity, 0.5.0

Use the [bare-hand procedure](BARE-HAND-MODE.md) with both controllers put down. It includes an explicitly armed, time-limited incoming-notification schedule so no controller is needed to send test notifications. The prior controller-motion physical test is superseded. Social remains a person-presence estimate.

### 0.5.1 isolated sound diagnosis

Start with `adb shell am force-stop org.notiflex.prototype`, then `adb shell am start -n org.notiflex.prototype/com.unity3d.player.UnityPlayerGameActivity --ez notiflex_test_audio true`. After five continuous seconds of VR focus, one normal chime is requested. This explicitly enabled development diagnostic bypasses context selection and does not count as activity recognition. Restart without the audio extra before the real hands-free notification trial. Automatic 0.5.1 context acceptance uses 1.2 s proposal support in a trailing 2 s window; see BARE-HAND-MODE.md for every condition.
