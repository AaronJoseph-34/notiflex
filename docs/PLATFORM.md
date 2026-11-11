# Meta Quest 3S platform assessment

Checked against Meta's documentation on November 11, 2026.

Quest 3S supports six-degree-of-freedom head tracking, Touch Plus controller input, hand tracking and color stereoscopic passthrough. Meta lists head/hand tracking for Quest 3S; this implementation uses head orientation and does not enable eye-gaze tracking. See [Meta's device comparison](https://developers.meta.com/vr/essentials/compare-devices/).

Standard passthrough is composited by the OS behind app content. Raw camera-frame access is a separate capability with its own permissions and policies. The MVP uses standard passthrough, with no camera-frame processing. See [Meta's passthrough overview](https://developers.meta.com/vr/essentials/horizon-os-passthrough/).

Meta's [Unity passthrough tutorial](https://developers.meta.com/vr/documentation/unity/unity-passthrough-tutorial/) includes Quest 3S, macOS development, Android builds, Unity OpenXR and the Meta XR Core SDK. Those capabilities make this stack suitable for a standalone prototype.

## Implemented stack

- Unity 6000.4.7f1; built-in renderer with linear color space.
- Unity OpenXR 1.19.0; Android loader initializes on startup.
- Meta XR Core SDK 209.0.0; OVR tracked rig and passthrough underlay.
- Android ARM64, IL2CPP, OpenGLES3; minimum API 32, target API 35.
- Controller buttons through Unity XR InputDevices; no Interaction SDK dependency is needed for the notification-only interaction model.
- Unity MCP v10.3.0 for editor authoring, inspection and tests.

## Boundaries of the deliverable

The application must be foregrounded as its own immersive app. It is not a system-wide notification overlay injected into other Quest applications. Live mode displays only its notification over the real room. Simulated modes provide authored geometry and annotations for repeatable algorithm tests.

Hand tracking is a device capability but this build deliberately uses controllers. No speech detector, arbitrary-room object detector, live face detector, fitted activity classifier, or raw-camera processing pipeline is included. Room geometry in passthrough is not automatically reconstructed; only the supplied task/partner rectangles constrain projected occlusion. Unannotated objects and hazards are not detected. A future sensing adapter can provide those inputs while retaining the policy's units and validity contract.
