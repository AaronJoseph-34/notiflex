# Controller input update — November 15, 2026

On a physical Quest 3S running Android 14 (API 34), version 0.3.0 installed and launched successfully. The operator confirmed visible passthrough. App diagnostics reported valid tracked head pose; Android reported two enabled VR controller peripherals. The operator reported no response to right A or left X, and the app decision log contained no notification or scene-change events from those attempts. This establishes a failed controller acceptance check; it does not establish the precise native input failure.

Version 0.3.1 replaces the Unity XR CommonUsages controller polling with Meta XR Core OVRInput raw Touch controls. It reads in LateUpdate, after OVRManager's Update refresh, and preserves rising-edge behavior. Inputs held while system menus own VR focus are suppressed until release. Controller annotation positions now use the rig's tracking-space transform. Action and controller-status changes are logged without adding a headset HUD or recording camera/microphone data. Placement costs and delivery rules are unchanged.

The Android ARM64/IL2CPP build completed successfully. APK Signature Scheme v2 verification passed; package identity is `org.notiflex.prototype`, version name `0.3.1`, version code `2`, minimum SDK 32 and target SDK 35.

APK SHA-256: `e387a892210752a3d61f43258df2632b420a95f467fbb7758a647275a93c2457`.

The update installed successfully and cold-launched on the headset. The operator confirmed that A sends a visible notification, the right index trigger expands it, B dismisses it and X switches from passthrough into an authored room. Device logs independently recorded all four actions; status logs recorded VR focus and both controllers becoming tracked. The app lifecycle log recorded notification, reading and dismissal events and the scene transition. See `evidence/controller-fix-0.3.1/hardware-acceptance.json` and `quest-controller-actions.txt`.

Unity MCP regression testing also passed all three existing EditMode groups (policy/normalized scores, room geometry, and failure/tracking cases), with zero failures or skips. Results and spatial sweep evidence are preserved separately under `evidence/controller-fix-0.3.1/`; the original v0.3.0 evidence is unchanged. These tests do not establish headset comfort, annotation accuracy, audio direction or sustained rendering performance, which remain separate acceptance checks.

Automatic room/activity/conversation sensing is not part of this update. Context modes and task/partner annotations remain explicitly supplied.
