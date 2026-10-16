# Mac Play mode

The system OpenXR runtime resolves through `/usr/local/share/openxr/1/active_runtime.json` to Meta XR Simulator 69.0.0 under Homebrew. The crash occurs in native simulator graphics initialization, before scene gameplay. Its Vulkan loader reports `volkInitialize failed: -3`.

## Desktop workaround applied

In XR Plug-in Management, the **desktop / Standalone** Initialize XR on Startup setting is off. This permits ordinary desktop Play and the XRI simulated-input workflow without loading the native Meta runtime. Android Initialize XR on Startup remains on for Quest builds. Desktop native VR testing requires re-enabling the desktop setting after repairing the runtime.

This is a workaround, not a repair of Meta XR Simulator. Meta documents a Vulkan SDK dependency for some Unity OpenXR/macOS combinations: https://developers.meta.com/vr/downloads/package/meta-xr-simulator/74/ . The installed runtime is older than that release. Updating it and verifying a compatible Vulkan installation is separate from building/running Quest apps.

## Crash evidence

```text
00000.043845][E][arvr/projects/openxr_simulator/src/rendering/sim_vulkan_helpers.cpp:531] volkInitialize failed: -3[Meta XR Simulator][00000.043855][E][arvr/projects/openxr_simulator/src/rendering/sim_vulkan_helpers.cpp:150] Failed Expectation: initializeVolk()[Meta XR Simulator][00000.043862][E][arvr/projects/openxr_simulator/src/sim_xrapilayer_rendering_metal_on_vulkan.cpp:66] [XrApiLayerRenderingMetalOnVulkan::createVulkanObjects] Unable to create Vulkan bundle for this instance[Meta XR Simulator][00000.043869][I][arvr/projects/openxr_simulator/src/sim_xrapilayer_rendering_metal_on_vulkan.cpp:66] The above trace will only be logged once. Repeats will not be logged
#0  0x00000517642054 in openxr_simulator::XrApiLayerRenderingMetalOnVulkan::createVulkanObjects(XrInstance_T*)
```
