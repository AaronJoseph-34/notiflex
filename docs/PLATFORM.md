# Meta Quest 3S platform assessment

Original assessment November 11; camera integration checked November 15, 2026. Physical Quest 3S reports Horizon OS v209 and Android 14/API 34.

Quest 3S supports six-degree-of-freedom head tracking, Touch Plus controller input, hand tracking and color stereoscopic passthrough. Meta lists head/hand tracking for Quest 3S; this implementation uses head orientation and does not enable eye-gaze tracking. See [Meta's device comparison](https://developers.meta.com/vr/essentials/compare-devices/).

Standard passthrough is composited by the OS behind app content. Raw camera-frame access is a separate capability with its own permissions and policies. Version 0.4.0 adds optional local camera-frame processing through MRUK, with its separate camera permission; the original 0.3.x builds use composition only. See [Meta's passthrough overview](https://developers.meta.com/vr/essentials/horizon-os-passthrough/).

Meta's [Unity passthrough tutorial](https://developers.meta.com/vr/documentation/unity/unity-passthrough-tutorial/) includes Quest 3S, macOS development, Android builds, Unity OpenXR and the Meta XR Core SDK. Those capabilities make this stack suitable for a standalone prototype.

## Implemented stack

- Unity 6000.4.7f1; built-in renderer with linear color space.
- Unity OpenXR 1.19.0; Android loader initializes on startup.
- Meta XR Core SDK/MRUK 209.0.0; OVR tracked rig, passthrough underlay and PassthroughCameraAccess.
- Unity Inference Engine 2.2.1; bundled quantized YOLOv9t model, CPU backend, aspect-preserving GPU preprocessing.
- Android ARM64, IL2CPP, OpenGLES3; minimum API 32, target API 35.
- Version 0.3.1 reads Touch controller buttons and annotation poses through Meta XR Core's OVRInput, in LateUpdate after OVRManager refreshes its input state. Actions are suppressed while system menus own VR focus. No Interaction SDK dependency is needed for these controls.
- Unity MCP v10.3.0 for editor authoring, inspection and tests.

## Boundaries of the deliverable

The application must be foregrounded as its own immersive app. It is not a system-wide notification overlay injected into other Quest applications. Live mode displays only its notification over the real room. Simulated modes provide authored geometry and annotations for repeatable algorithm tests.

Versions 0.4–0.5 established the initial camera, controller and bare-hand pipeline on the physical Quest. Version 0.6.0 adds local Silero VAD and WeSpeaker embeddings, MRUK saved-room planes/mesh, native environment raycasts, and depth-supported session object tracks. It uses the supported MRUK V1 scene model; high-fidelity V2 is not claimed. Meta's [Depth API sample documentation](https://developers.meta.com/vr/documentation/unity/unity-sample-depth-api/) lists Quest 3S support; this does not imply a dedicated depth sensor. Spatial data require their [separate permission](https://developers.meta.com/vr/documentation/unity/unity-spatial-data-perm/). APIs and OS support must be verified on the actual device.

The new 0.6.0 paths pass offline tests but have not yet passed headset trials. User chose later hardware testing. No eye tracking, named identity/face recognition, arbitrary activity understanding, guaranteed conversation confirmation, or calibrated social-risk probability is claimed. See [current exact method](CURRENT-METHOD-0.6.0.md), [coverage matrix](PAPER-COVERAGE-IMPLEMENTATION.md) and [hardware procedure](QUEST-ACCEPTANCE-0.6.0.md).
