# Notiflex VR implementation plan

1. Establish pinned Unity OpenXR, Meta XR Core, and Unity MCP dependencies; install Android modules and verify repository access.
2. Specify deterministic rules with named constraints, cost equations, context priority, temporal stability, delivery, tracking loss, stale context, and acknowledgement/defer lifecycle.
3. Separate context providers, spatial candidate evaluation, presentation policy, world-space renderer, and logging. Preserve head orientation as an attention proxy.
4. Build authored Desk, Kitchen and LivingRoom scenes with protected task/face/hazard geometry, task referents, conversation partner proxies, lighting and spatial obstacles. Expose context, urgency and viewpoint variations without relying on scene names as activity recognition.
5. Build a separate Passthrough scene showing only live surroundings and notifications. Supply task/partner annotations explicitly until sensing is implemented. Do not imply that the synthetic scenes validate raw-camera recognition.
6. Run deterministic engine checks, geometric constraints, full scenario sweeps, runtime rendering captures and MCP editor inspection. Fix failures before producing desktop and Android builds.
7. Produce the rules manual and validation report, retain unresolved hardware checks, verify clean reproducible source, then push to the requested private GitHub repository.

Acceptance: three independent simulation scenes; tracked stereo rig on device; readable world-space notification; clear ineligible/deferred states; decision logs; passing automated software checks; Android build if the toolchain installs successfully. Physical headset, passthrough readiness, comfort and sensor accuracy cannot be certified without hardware.

Completed software stages: authored scenes, rules manual, Unity MCP tests, geometric sweeps, offscreen rendering, built-player lifecycle audit, and successful Mac/Android builds. Device acceptance remains the explicit next step when a Quest is available.
