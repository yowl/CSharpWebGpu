#include "E:/GitHub/wasm_webgpu/lib/lib_webgpu.h"
#include <emscripten.h>

WGpuRenderPipelineDescriptor GetWGPU_RENDER_PIPELINE_DESCRIPTOR_DEFAULT_INITIALIZER()
{
	return WGPU_RENDER_PIPELINE_DESCRIPTOR_DEFAULT_INITIALIZER;
}

WGpuCanvasConfiguration GetWGPU_CANVAS_CONFIGURATION_DEFAULT_INITIALIZER()
{
	return WGPU_CANVAS_CONFIGURATION_DEFAULT_INITIALIZER;
}

WGpuColorTargetState GetWGPU_COLOR_TARGET_STATE_DEFAULT_INITIALIZER()
{
	return WGPU_COLOR_TARGET_STATE_DEFAULT_INITIALIZER;
}

WGpuRenderPassColorAttachment GetWGPU_RENDER_PASS_COLOR_ATTACHMENT_DEFAULT_INITIALIZER()
{
	return WGPU_RENDER_PASS_COLOR_ATTACHMENT_DEFAULT_INITIALIZER;
}

EM_JS(int, canvas_get_width, (), {
  return canvas.width;
});

EM_JS(int, canvas_get_height, (), {
  return canvas.height;
});
