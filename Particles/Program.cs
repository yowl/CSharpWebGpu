using WebGpu;
using WGpuAdapter = WebGpu.WGpuObjectBase;
using WGpuCanvasContext = WebGpu.WGpuObjectBase;
using WGpuDevice = WebGpu.WGpuObjectBase;
using WGpuQueue = WebGpu.WGpuObjectBase;
using WGpuRenderPipeline = WebGpu.WGpuObjectBase;
using WGpuCommandEncoder = WebGpu.WGpuObjectBase;
using WGpuRenderPassEncoder = WebGpu.WGpuObjectBase;
using WGpuCommandBuffer = WebGpu.WGpuObjectBase;
using WGpuShaderModule = WebGpu.WGpuObjectBase;
using WGpuBuffer = WebGpu.WGpuObjectBase;
using WGpuBindGroupLayout = WebGpu.WGpuObjectBase;
using WGpuTextureView = WebGpu.WGpuObjectBase;
using static WebGpu.Interop;
using WGpuTexture = WebGpu.WGpuObjectBase;
using WGpuImageBitmap = WebGpu.WGpuObjectBase;
using WGpuComputePipeline = WebGpu.WGpuObjectBase;
using WGpuPipelineLayout = WebGpu.WGpuObjectBase;
using WGpuBindGroup = WebGpu.WGpuObjectBase;
using WGpuComputePassEncoder = WebGpu.WGpuObjectBase;

using System.Runtime.InteropServices;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System;
using System.Numerics;
using Triangle;
using WebGpuSample;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using GLMatrixSharp;

namespace WebGpuSample
{
    public unsafe class Particles
    {
        [DllImport("*")]
        internal static extern unsafe void emscripten_request_animation_frame_loop(delegate* unmanaged<double, void*, /* EM_BOOL */ int> f, IntPtr userDataPtr);

        [DllImport("*")]
        internal static extern unsafe double emscripten_get_device_pixel_ratio();

        [DllImport("*")]
        internal static extern unsafe int canvas_get_width();

        [DllImport("*")]
        internal static extern unsafe int canvas_get_height();

        [DllImport("*", CharSet = CharSet.Ansi)]
        internal static extern unsafe int emscripten_set_canvas_element_size(string selector, int width, int height);

        static WGpuAdapter adapter;
        static WGpuCanvasContext canvasContext;
        static WGpuDevice device;
        static WGpuQueue queue;
        static WGpuRenderPipeline renderPipeline;

        const int numParticles = 1000000;
        const int particlePositionOffset = 0;
        const int particleColorOffset = 4 * 4;

        private const int particleInstanceByteSize =
            3 * 4 + // position
            1 * 4 + // lifetime
            4 * 4 + // color
            3 * 4 + // velocity
            1 * 4 + // padding
            0;

        static int[] presentationSize;
        static WGpuBuffer particlesBuffer;

        static float[] projection;
        static float[] view;
        static float[] mvp;
        static WGpuBuffer simulationUBOBuffer;
        static WGpuBuffer uniformBuffer;
        static WGpuRenderPassDescriptor renderPassDescriptor;
        static  WGpuComputePipeline computePipeline;
        static WGpuBindGroup computeBindGroup;
        static WGpuBindGroup uniformBindGroup;
        static WGpuBuffer quadVertexBuffer;
        static GCHandle pinnedColorAttachmentsHandle;
        // temp for debugging
        static WGpuObjectBase staticDepthTexture;
        private static WGpuRenderPassColorAttachment[] colorAttachments;

        static void SampleInit()
        {
            double devicePixelRatio = emscripten_get_device_pixel_ratio();

            int width = canvas_get_width();
            int height = canvas_get_height();


            presentationSize = new[]
            {
                (int)(width * devicePixelRatio),
                (int)(height * devicePixelRatio),
            };

            double presentationWidth = width * devicePixelRatio;
            double presentationHeight = height * devicePixelRatio;

            var presentationFormat = navigator_gpu_get_preferred_canvas_format();

            WGpuCanvasConfiguration config = GetWGPU_CANVAS_CONFIGURATION_DEFAULT_INITIALIZER();
            config.device = device;
            config.format = presentationFormat;
            config.alphaMode = WGPU_CANVAS_ALPHA_MODE_OPAQUE;
            wgpu_canvas_context_configure(canvasContext, ref config);

            WGpuBufferDescriptor bufferDescriptor = default;
            bufferDescriptor.size = numParticles * particleInstanceByteSize;
            bufferDescriptor.usage = WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_VERTEX |
                                     WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_STORAGE;
            particlesBuffer = wgpu_device_create_buffer(device, ref bufferDescriptor);

            WGpuRenderPipelineDescriptor renderPipelineDesc = GetWGPU_RENDER_PIPELINE_DESCRIPTOR_DEFAULT_INITIALIZER();
            renderPipelineDesc.layout = new WGpuObjectBase(WGPU_AUTO_LAYOUT_MODE_AUTO);
            WGpuShaderModuleDescriptor shaderModuleDesc = default;
            shaderModuleDesc.code = ShaderCode.particleWGSL;
            WGpuShaderModule particleShaderModule = wgpu_device_create_shader_module(device, ref shaderModuleDesc);

            renderPipelineDesc.vertex.module = particleShaderModule;
            renderPipelineDesc.vertex.entryPoint = "vs_main";
            WGpuVertexBufferLayout[] buffers = new WGpuVertexBufferLayout[2];

            // instanced particles buffer
            buffers[0].arrayStride = particleInstanceByteSize;
            buffers[0].stepMode = WGPU_VERTEX_STEP_MODE.WGPU_VERTEX_STEP_MODE_INSTANCE;
            var buffer0Attributes = new WGpuVertexAttribute[2];
            // position
            buffer0Attributes[0].shaderLocation = 0;
            buffer0Attributes[0].offset = particlePositionOffset;
            buffer0Attributes[0].format = WGPU_VERTEX_FORMAT.WGPU_VERTEX_FORMAT_FLOAT32X3;
            // color
            buffer0Attributes[1].shaderLocation = 1;
            buffer0Attributes[1].offset = particleColorOffset;
            buffer0Attributes[1].format = WGPU_VERTEX_FORMAT.WGPU_VERTEX_FORMAT_FLOAT32X4;

            GCHandle.Alloc(buffer0Attributes);

            buffers[0].attributes = (WGpuVertexAttribute*)Marshal.UnsafeAddrOfPinnedArrayElement(buffer0Attributes, 0);
            buffers[0].numAttributes = buffer0Attributes.Length;

            buffers[1].arrayStride = 2 * 4; // vec2<f32>
            buffers[1].stepMode = WGPU_VERTEX_STEP_MODE.WGPU_VERTEX_STEP_MODE_VERTEX;
            var buffer1Attributes = new WGpuVertexAttribute[1];
            buffer1Attributes[0].shaderLocation = 2;
            buffer1Attributes[0].offset = 0;
            buffer1Attributes[0].format = WGPU_VERTEX_FORMAT.WGPU_VERTEX_FORMAT_FLOAT32X2;

            GCHandle.Alloc(buffer1Attributes);

            buffers[1].attributes = (WGpuVertexAttribute*)Marshal.UnsafeAddrOfPinnedArrayElement(buffer1Attributes, 0);
            buffers[1].numAttributes = buffer1Attributes.Length;

            GCHandle.Alloc(buffers);

            renderPipelineDesc.vertex.buffers =
                (WGpuVertexBufferLayout*)Marshal.UnsafeAddrOfPinnedArrayElement(buffers, 0);
            renderPipelineDesc.vertex.numBuffers = buffers.Length;

            // fragment
            WGpuShaderModuleDescriptor fragmentShaderModuleDesc = default;
            fragmentShaderModuleDesc.code = ShaderCode.particleWGSL;

            WGpuShaderModule particleFragmentShaderModule =
                wgpu_device_create_shader_module(device, ref fragmentShaderModuleDesc);

            renderPipelineDesc.fragment.module = particleFragmentShaderModule;
            renderPipelineDesc.fragment.entryPoint = "fs_main";
            WGpuColorTargetState[] targets = new WGpuColorTargetState[1];
            targets[0].format = presentationFormat;
            targets[0].blend.color.srcFactor = WGPU_BLEND_FACTOR.WGPU_BLEND_FACTOR_SRC_ALPHA;
            targets[0].blend.color.dstFactor = WGPU_BLEND_FACTOR.WGPU_BLEND_FACTOR_ONE;
            targets[0].blend.color.operation = WGPU_BLEND_OPERATION.WGPU_BLEND_OPERATION_ADD;

            targets[0].blend.alpha.srcFactor = WGPU_BLEND_FACTOR.WGPU_BLEND_FACTOR_ZERO;
            targets[0].blend.alpha.dstFactor = WGPU_BLEND_FACTOR.WGPU_BLEND_FACTOR_ONE;
            targets[0].blend.alpha.operation = WGPU_BLEND_OPERATION.WGPU_BLEND_OPERATION_ADD;

            GCHandle.Alloc(targets);

            WGpuColorTargetState colorTarget = GetWGPU_COLOR_TARGET_STATE_DEFAULT_INITIALIZER();
            colorTarget.format = config.format;
            colorTarget.blend.color.operation = WGPU_BLEND_OPERATION.WGPU_BLEND_OPERATION_ADD;
            colorTarget.blend.color.srcFactor = WGPU_BLEND_FACTOR.WGPU_BLEND_FACTOR_SRC_ALPHA;
            colorTarget.blend.color.dstFactor = WGPU_BLEND_FACTOR.WGPU_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
            renderPipelineDesc.fragment.numTargets = 1;
            renderPipelineDesc.fragment.targets = &colorTarget;

            // renderPipelineDesc.fragment.targets =
            //     (WGpuColorTargetState*)Marshal.UnsafeAddrOfPinnedArrayElement(targets, 0);
            // renderPipelineDesc.fragment.numTargets = targets.Length;

            renderPipelineDesc.primitive.topology = WGPU_PRIMITIVE_TOPOLOGY.WGPU_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            renderPipelineDesc.depthStencil.depthWriteEnabled = 0;
            renderPipelineDesc.depthStencil.depthCompare = WGPU_COMPARE_FUNCTION.WGPU_COMPARE_FUNCTION_LESS;
            renderPipelineDesc.depthStencil.format = WGPU_TEXTURE_FORMAT.WGPU_TEXTURE_FORMAT_DEPTH24PLUS;

            // and create the pipeline
            Console.WriteLine("Creating render pipeline");
            renderPipeline = wgpu_device_create_render_pipeline(device, ref renderPipelineDesc);
            Console.WriteLine("Created render pipeline");

            WGpuTextureDescriptor textureDescriptor = GetWGPU_TEXTURE_DESCRIPTOR_DEFAULT_INITIALIZER();
            textureDescriptor.width = (uint)presentationWidth;
            textureDescriptor.height = (uint)presentationHeight;
            textureDescriptor.format = WGPU_TEXTURE_FORMAT.WGPU_TEXTURE_FORMAT_DEPTH24PLUS;
            textureDescriptor.usage = WGPU_TEXTURE_USAGE_FLAGS.WGPU_TEXTURE_USAGE_RENDER_ATTACHMENT;

            Console.WriteLine($"requesting depthTexture with width {textureDescriptor.width} and height {textureDescriptor.height}");
            var depthTexture = wgpu_device_create_texture(device, ref textureDescriptor);
            staticDepthTexture = depthTexture;
            Console.WriteLine("Got depthTexture");

            const int uniformBufferSize =
                4 * 4 * 4 + // modelViewProjectionMatrix : mat4x4<f32>
                3 * 4 + // right : vec3<f32>
                4 + // padding
                3 * 4 + // up : vec3<f32>
                4 + // padding
                0;
            WGpuBufferDescriptor uniformBufferDesc = default;
            uniformBufferDesc.size = uniformBufferSize;
            uniformBufferDesc.usage = WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_UNIFORM |
                                      WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_COPY_DST;
            uniformBuffer = wgpu_device_create_buffer(device, ref uniformBufferDesc);

            WGpuBindGroupEntry[] bindGroupLayoutEntries = new WGpuBindGroupEntry[1];
            bindGroupLayoutEntries[0].binding = 0;
            bindGroupLayoutEntries[0].resource = uniformBuffer;
            bindGroupLayoutEntries[0].bufferBindSize = 0; // bind to whole buffer
            var bindGroupLayoutEntriesHandle = GCHandle.Alloc(bindGroupLayoutEntries);

            uniformBindGroup = wgpu_device_create_bind_group(device,
                wgpu_pipeline_get_bind_group_layout(renderPipeline, 0),
                (WGpuBindGroupEntry*)Marshal.UnsafeAddrOfPinnedArrayElement(bindGroupLayoutEntries, 0), 1);

            renderPassDescriptor = default;


            colorAttachments = new WGpuRenderPassColorAttachment[1];
            colorAttachments[0].view = new WGpuTextureView(0); // undefined, assigned later
            colorAttachments[0].clearValue.r = 0;
            colorAttachments[0].clearValue.g = 0;
            colorAttachments[0].clearValue.b = 0;
            colorAttachments[0].clearValue.a = 1.0;
            colorAttachments[0].loadOp = WGPU_LOAD_OP.WGPU_LOAD_OP_CLEAR;
            colorAttachments[0].storeOp = WGPU_STORE_OP.WGPU_STORE_OP_STORE;

            pinnedColorAttachmentsHandle = GCHandle.Alloc(colorAttachments);
            renderPassDescriptor.colorAttachments =
                (WGpuRenderPassColorAttachment*)Marshal.UnsafeAddrOfPinnedArrayElement(colorAttachments, 0);
            renderPassDescriptor.numColorAttachments = colorAttachments.Length;

            var textureView = wgpu_texture_create_view_simple(depthTexture);
            renderPassDescriptor.depthStencilAttachment.view = textureView;
            renderPassDescriptor.depthStencilAttachment.depthClearValue = 1;
            renderPassDescriptor.depthStencilAttachment.depthLoadOp = WGPU_LOAD_OP.WGPU_LOAD_OP_CLEAR;
            renderPassDescriptor.depthStencilAttachment.depthStoreOp = WGPU_STORE_OP.WGPU_STORE_OP_STORE;

            //////////////////////////////////////////////////////////////////////////////
            // Quad vertex buffer
            //////////////////////////////////////////////////////////////////////////////
            WGpuBufferDescriptor quadBufferDesc = default;
            quadBufferDesc.size = 6 * 2 * 4; // 6x vec2<f32>
            quadBufferDesc.usage = WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_VERTEX;
            quadBufferDesc.mappedAtCreation = 1;
            quadVertexBuffer = wgpu_device_create_buffer(device, ref quadBufferDesc);

            // prettier-ignore
            float[] vertexData =
            {
                -1.0f, -1.0f, +1.0f, -1.0f, -1.0f, +1.0f, -1.0f, +1.0f, +1.0f, -1.0f, +1.0f, +1.0f,
            };
            var mappedRange = wgpu_buffer_get_mapped_range(quadVertexBuffer, 0, WGPU_MAP_MAX_LENGTH);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (mappedRange == -1)
            {
                Console.WriteLine("wgpu_buffer_get_mapped_range failed: returned -1");
            }

            GCHandle.Alloc(vertexData);

            wgpu_buffer_write_mapped_range(quadVertexBuffer, 0, 0,
                (void*)Marshal.UnsafeAddrOfPinnedArrayElement(vertexData, 0), (ulong)(vertexData.Length * 4) /* byte count */);
            wgpu_buffer_unmap(quadVertexBuffer);

            Console.WriteLine("loading bitmap");
            wgpu_load_image_bitmap_from_url_async("webgpu_upside_down.png", 1, &DownloadedImage, IntPtr.Zero);
        }

        //////////////////////////////////////////////////////////////////////////////
        // Texture
        //////////////////////////////////////////////////////////////////////////////
        // continue with rest of setup in callback
        [UnmanagedCallersOnly]
        static void DownloadedImage(WGpuImageBitmap gpuImageBitmap, int width, int height, IntPtr userData)
        {
            Console.WriteLine($"loaded bitmap with width {width} and height {height}");

            if (width == 0 || height == 0)
            {
                Console.WriteLine("no image downloaded, exiting.");

                return;
            }

            uint textureWidth = 1;
            uint textureHeight = 1;
            int numMipLevels = 1;

            // Calculate number of mip levels required to generate the probability map
            while (
              textureWidth < width ||
              textureHeight < height
            )
            {
                textureWidth *= 2;
                textureHeight *= 2;
                numMipLevels++;
            }

            Console.WriteLine($"mips levels {numMipLevels} textureWidth {textureWidth}");

            WGpuTextureDescriptor imageTextureDesc = GetWGPU_TEXTURE_DESCRIPTOR_DEFAULT_INITIALIZER();
            imageTextureDesc.width = textureWidth;
            imageTextureDesc.height = textureHeight;
            imageTextureDesc.depthOrArrayLayers = 1;
            imageTextureDesc.mipLevelCount = numMipLevels;
            imageTextureDesc.format = WGPU_TEXTURE_FORMAT.WGPU_TEXTURE_FORMAT_RGBA8UNORM;
            imageTextureDesc.usage = WGPU_TEXTURE_USAGE_FLAGS.WGPU_TEXTURE_USAGE_TEXTURE_BINDING |
                                     WGPU_TEXTURE_USAGE_FLAGS.WGPU_TEXTURE_USAGE_STORAGE_BINDING |
                                     WGPU_TEXTURE_USAGE_FLAGS.WGPU_TEXTURE_USAGE_COPY_DST |
                                     WGPU_TEXTURE_USAGE_FLAGS.WGPU_TEXTURE_USAGE_RENDER_ATTACHMENT;
            WGpuTexture texture = wgpu_device_create_texture(device, ref imageTextureDesc);
            queue = wgpu_device_get_queue(device);
            WGpuImageCopyExternalImage src = default;
            src.source = gpuImageBitmap;

            WGpuImageCopyTextureTagged imageCopyTexture = default;
            imageCopyTexture.texture = texture;
            wgpu_queue_copy_external_image_to_texture(queue, ref src, ref imageCopyTexture, width, height, 1);

            //////////////////////////////////////////////////////////////////////////////
            // Probability map generation
            // The 0'th mip level of texture holds the color data and spawn-probability in
            // the alpha channel. The mip levels 1..N are generated to hold spawn
            // probabilities up to the top 1x1 mip level.
            //////////////////////////////////////////////////////////////////////////////
            {
                WGpuShaderModuleDescriptor probMapShaderModuleDesc = default;
                probMapShaderModuleDesc.code = ShaderCode.probabiltyMapWGSL;
                probMapShaderModuleDesc.hints = null;
                probMapShaderModuleDesc.numHints = 0;
                var probMapImportShaderModule = wgpu_device_create_shader_module(device, ref probMapShaderModuleDesc);
                var probMapExportShaderModule = wgpu_device_create_shader_module(device, ref probMapShaderModuleDesc);
                WGpuPipelineLayout probLayout = new WGpuObjectBase(WGPU_AUTO_LAYOUT_MODE_AUTO);

                Console.WriteLine("creating import pipeline");
                var probabilityMapImportLevelPipeline = wgpu_device_create_compute_pipeline(device, probMapImportShaderModule,
                    "import_level", probLayout, null, 0);
                
                Console.WriteLine("creating export pipeline");
                var probabilityMapExportLevelPipeline = wgpu_device_create_compute_pipeline(device, probMapExportShaderModule,
                    "export_level", probLayout, null, 0);

                ulong probabilityMapUBOBufferSize =
                    1 * 4 + // stride
                    3 * 4 + // padding
                    0;
                WGpuBufferDescriptor uboBufferDesc = default;
                uboBufferDesc.size = probabilityMapUBOBufferSize;
                uboBufferDesc.usage = WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_UNIFORM |
                                      WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_COPY_DST;

                Console.WriteLine($"creating probabilityMapUBOBuffer with size {probabilityMapUBOBufferSize}");
                var probabilityMapUBOBuffer = wgpu_device_create_buffer(device, ref uboBufferDesc);

                WGpuBufferDescriptor bufferADesc = default;
                bufferADesc.size = textureWidth * textureHeight * 4;
                bufferADesc.usage = WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_STORAGE;

                Console.WriteLine($"creating buffer_a with size {bufferADesc.size}");
                var bufferA = wgpu_device_create_buffer(device, ref bufferADesc);

                WGpuBufferDescriptor bufferBDesc = default;
                bufferBDesc.size = textureWidth * textureHeight * 4;
                bufferBDesc.usage = WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_STORAGE;

                Console.WriteLine($"creating buffer_b with size {bufferBDesc.size}");
                var bufferB = wgpu_device_create_buffer(device, ref bufferBDesc);

                uint[] queueBufferWipe = new uint[] { textureWidth };
                GCHandle.Alloc(queueBufferWipe);

                wgpu_queue_write_buffer(queue, probabilityMapUBOBuffer, 0,
                    (void*)Marshal.UnsafeAddrOfPinnedArrayElement(queueBufferWipe, 0), 1 * 4);

                Console.WriteLine("creating commandEncoder");
                WGpuCommandEncoder commandEncoder =
                    wgpu_device_create_command_encoder_simple(device);

                WGpuComputePassDescriptor nullComputePassDescriptor = default;

                // Console.WriteLine("creating dummy encoder");
                // WGpuComputePassEncoder passEncoderX =
                //     wgpu_command_encoder_begin_compute_pass(commandEncoder, &nullComputePassDescriptor);
                // Console.WriteLine("created dummy encoder");

                for (uint level = 0; level < numMipLevels; level++)
                {
                    Console.WriteLine($"setting up mips level {level}");

                    uint levelWidth = textureWidth >> (int)level;
                    uint levelHeight = textureHeight >> (int)level;
                    WGpuBindGroupLayout pipeline =
                        level == 0
                            ? wgpu_pipeline_get_bind_group_layout(probabilityMapImportLevelPipeline, 0)
                            : wgpu_pipeline_get_bind_group_layout(probabilityMapExportLevelPipeline, 0);

                    WGpuBindGroupEntry[] bindGroupEntries = new WGpuBindGroupEntry[4];
                    bindGroupEntries[0].binding = 0;
                    bindGroupEntries[0].resource = probabilityMapUBOBuffer;

                    bindGroupEntries[1].binding = 1;
                    bindGroupEntries[1].resource = (level & 1) == 1 ? bufferA : bufferB;

                    bindGroupEntries[2].binding = 2;
                    bindGroupEntries[2].resource = (level & 1) == 1 ? bufferB : bufferA;

                    WGpuTextureViewDescriptor textureViewDesc = default;
                    textureViewDesc.format = WGPU_TEXTURE_FORMAT.WGPU_TEXTURE_FORMAT_RGBA8UNORM;
                    textureViewDesc.dimension = WGPU_TEXTURE_VIEW_DIMENSION.WGPU_TEXTURE_VIEW_DIMENSION_2D;
                    textureViewDesc.baseMipLevel = level;
                    textureViewDesc.mipLevelCount = 1;
                    textureViewDesc.arrayLayerCount = 1;
                    var texView = wgpu_texture_create_view(texture, &textureViewDesc);
                    bindGroupEntries[3].binding = 3;
                    bindGroupEntries[3].resource = texView;

                    GCHandle.Alloc(bindGroupEntries);

                    Console.WriteLine("calling wgpu_device_create_bind_group");
                    WGpuBindGroup probabilityMapBindGroup = wgpu_device_create_bind_group(device, pipeline,
                        (WGpuBindGroupEntry*)Marshal.UnsafeAddrOfPinnedArrayElement(bindGroupEntries, 0), 4);
                    if (level == 0)
                    {
                        Console.WriteLine("calling wgpu_command_encoder_begin_compute_pass");
                        WGpuComputePassEncoder passEncoder =
                            wgpu_command_encoder_begin_compute_pass(commandEncoder, &nullComputePassDescriptor);
                        Console.WriteLine("calling wgpu_encoder_set_pipeline");
                        wgpu_encoder_set_pipeline(passEncoder, probabilityMapImportLevelPipeline);
                        Console.WriteLine("calling wgpu_encoder_set_bind_group");
                        wgpu_encoder_set_bind_group(passEncoder, 0, probabilityMapBindGroup, null, 0);
                        Console.WriteLine("calling wgpu_compute_pass_encoder_dispatch_workgroups");
                        wgpu_compute_pass_encoder_dispatch_workgroups(passEncoder, (uint)Math.Ceiling(levelWidth / 54d),
                            levelHeight);
                        Console.WriteLine("calling wgpu_render_pass_encoder_end");
                        wgpu_encoder_end(passEncoder);
                    }
                    else
                    {
                        WGpuComputePassEncoder passEncoder =
                            wgpu_command_encoder_begin_compute_pass(commandEncoder, &nullComputePassDescriptor);
                        wgpu_encoder_set_pipeline(passEncoder, probabilityMapExportLevelPipeline);
                        wgpu_encoder_set_bind_group(passEncoder, 0, probabilityMapBindGroup, null, 0);
                        wgpu_compute_pass_encoder_dispatch_workgroups(passEncoder, (uint)Math.Ceiling(levelWidth / 64d),
                            levelHeight);
                        wgpu_encoder_end(passEncoder);
                    }
                }
                Console.WriteLine("calling wgpu_queue_submit_one");

                wgpu_queue_submit_one(queue, wgpu_command_encoder_finish(commandEncoder));
                Console.WriteLine("called wgpu_queue_submit_one");
            }

            //////////////////////////////////////////////////////////////////////////////
            // Simulation compute pipeline
            //////////////////////////////////////////////////////////////////////////////
            // Used in sample UI to control the simulation, we will leave this out
            // const simulationParams = {
            //     deltaTime: 0.04,
            //   simulate: true,
            // };

            ulong simulationUBOBufferSize =
                1 * 4 + // deltaTime
                3 * 4 + // padding
                4 * 4 + // seed
                0;
            WGpuBufferDescriptor simComputeBufferDesc = default;
            simComputeBufferDesc.size = simulationUBOBufferSize;
            simComputeBufferDesc.usage = WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_UNIFORM |
                                         WGPU_BUFFER_USAGE_FLAGS.WGPU_BUFFER_USAGE_COPY_DST;
            simulationUBOBuffer = wgpu_device_create_buffer(device, ref simComputeBufferDesc);

            // this adds the parameters to the screen
            // Object.keys(simulationParams).forEach((k) =>
            // {
            //         gui.add(simulationms, k);
            //     });
            WGpuShaderModuleDescriptor simShaderModuleDesc = default;
            simShaderModuleDesc.code = ShaderCode.particleWGSL;
            simShaderModuleDesc.hints = null;
            simShaderModuleDesc.numHints = 0;
            WGpuShaderModule simShaderModule = wgpu_device_create_shader_module(device, ref simShaderModuleDesc);
            WGpuPipelineLayout simLayout = new WGpuObjectBase(WGPU_AUTO_LAYOUT_MODE_AUTO);
            computePipeline = wgpu_device_create_compute_pipeline(device, simShaderModule,
                "simulate", simLayout, null, 0);

            WGpuBindGroupEntry[] simBindGroupEntries = new WGpuBindGroupEntry[3];
            simBindGroupEntries[0].binding = 0;
            simBindGroupEntries[0].resource = simulationUBOBuffer;
            simBindGroupEntries[0].bufferBindSize = 0;

            simBindGroupEntries[1].binding = 1;
            simBindGroupEntries[1].resource = particlesBuffer;
            simBindGroupEntries[1].bufferBindOffset = 0;
            simBindGroupEntries[1].bufferBindSize = numParticles * particleInstanceByteSize;

            WGpuTextureView textureView = wgpu_texture_create_view(texture, null);
            simBindGroupEntries[2].binding = 2;
            simBindGroupEntries[2].resource = textureView;
            simBindGroupEntries[2].bufferBindSize = 0;

            var simBindHandle = GCHandle.Alloc(simBindGroupEntries);

            computeBindGroup = wgpu_device_create_bind_group(device,
                wgpu_pipeline_get_bind_group_layout(computePipeline, 0),
                (WGpuBindGroupEntry*)Marshal.UnsafeAddrOfPinnedArrayElement(simBindGroupEntries, 0), 3);

            simBindHandle.Free();

            float aspect = (float)presentationSize[0] / presentationSize[1];
            projection = Mat4.Perspective((float)((2 * Math.PI) / 5), aspect, 1, 100.0f);
            view = Mat4.Create();
            mvp = Mat4.Create();

            emscripten_request_animation_frame_loop(&Frame, IntPtr.Zero);
        }

        [UnmanagedCallersOnly]
        static int Frame(double t, void* data)
        {
            // Console.WriteLine("frame");
            WGpuComputePassDescriptor nullComputePassDescriptor = default;

            float[] frameSimArray = new float[]
            {
                0.04f,
                0.0f,
                0.0f,
                0.0f, // padding
                random.NextSingle() * 100,
                random.NextSingle() * 100, // seed.xy
                1 + random.NextSingle(),
                1 + random.NextSingle()
            };
            var frameSimArrayHandler = GCHandle.Alloc(frameSimArray);

            wgpu_queue_write_buffer(queue, simulationUBOBuffer, 0, (float*)Marshal.UnsafeAddrOfPinnedArrayElement(frameSimArray, 0), 8 * 4);

            frameSimArrayHandler.Free();

            Mat4.Identity(view);
            view = Mat4.Translate(view, new [] { 0f, 0f, -3f });
            view = Mat4.RotateX(view, (float)(Math.PI * -0.2));
            mvp = Mat4.Multiply(projection, view);

            // prettier-ignore
            float[] frameUniformArray = new[]
            {
                mvp[0], mvp[1], mvp[2], mvp[3],
                mvp[4], mvp[5], mvp[6], mvp[7],
                mvp[8], mvp[9], mvp[10], mvp[11],
                mvp[12], mvp[13], mvp[14], mvp[15],

                view[0], view[4], view[8], // right

                0, // padding

                view[1], view[5], view[9], // up

                0, // padding
            };

            var frameUniformArrayHandle = GCHandle.Alloc(frameUniformArray);

            wgpu_queue_write_buffer(queue, uniformBuffer, 0, (float*)Marshal.UnsafeAddrOfPinnedArrayElement(frameUniformArray, 0), frameUniformArray.Length * 4);

            frameUniformArrayHandle.Free();

            WGpuTextureView textureView = wgpu_texture_create_view_simple(wgpu_canvas_context_get_current_texture(canvasContext));
            // prettier-ignore
            // Console.WriteLine($"textureView Ptr {textureView.ptr}");
            colorAttachments[0].view = textureView;
            // renderPassDescriptor.colorAttachments[0].view = textureView;
            // renderPassDescriptor.colorAttachments =
            //     (WGpuRenderPassColorAttachment*)Marshal.UnsafeAddrOfPinnedArrayElement(colorAttachments, 0);

            // Console.WriteLine("creating commandEncoder");
            WGpuCommandEncoder commandEncoder = wgpu_device_create_command_encoder_simple(device);
            {
                WGpuComputePassEncoder passEncoder = wgpu_command_encoder_begin_compute_pass(commandEncoder, &nullComputePassDescriptor);
                // this is the render pass encoder set pipeline method, but think they are all mapped to wgpu_encoder_set_pipeline, TODO: create encoder alias
                wgpu_render_pass_encoder_set_pipeline(passEncoder, computePipeline);
                wgpu_encoder_set_bind_group(passEncoder, 0, computeBindGroup, null, 0);
                wgpu_compute_pass_encoder_dispatch_workgroups(passEncoder, (uint)(Math.Ceiling((double)numParticles / 64)));
                wgpu_render_pass_encoder_end(passEncoder);
            }
            // Console.WriteLine("creating passEncoder");
            {
                var viewPtr = (*renderPassDescriptor.colorAttachments);
                // Console.WriteLine($"renderPassDescriptor.colorAttachments view {viewPtr.view.ptr}");
                colorAttachments[0].view = textureView;

                // WGpuRenderPassDescriptor renderPassDescriptor2 = default;
                // var colorAttachments2 = new WGpuRenderPassColorAttachment[1];
                // colorAttachments2[0].view = textureView;
                // colorAttachments2[0].clearValue.r = 0;
                // colorAttachments2[0].clearValue.g = 0;
                // colorAttachments2[0].clearValue.b = 0;
                // colorAttachments2[0].clearValue.a = 1.0;
                // colorAttachments2[0].loadOp = WGPU_LOAD_OP.WGPU_LOAD_OP_CLEAR;
                // colorAttachments2[0].storeOp = WGPU_STORE_OP.WGPU_STORE_OP_STORE;
                //
                // pinnedColorAttachmentsHandle = GCHandle.Alloc(colorAttachments2);
                // renderPassDescriptor2.colorAttachments =
                //     (WGpuRenderPassColorAttachment*)Marshal.UnsafeAddrOfPinnedArrayElement(colorAttachments2, 0);
                // renderPassDescriptor2.numColorAttachments = colorAttachments2.Length;
                // var depthStencilTextureView = wgpu_texture_create_view_simple(staticDepthTexture);
                //
                // renderPassDescriptor2.depthStencilAttachment.view = depthStencilTextureView;
                // renderPassDescriptor2.depthStencilAttachment.depthClearValue = 1;
                // renderPassDescriptor2.depthStencilAttachment.depthLoadOp = WGPU_LOAD_OP.WGPU_LOAD_OP_CLEAR;
                // renderPassDescriptor2.depthStencilAttachment.depthStoreOp = WGPU_STORE_OP.WGPU_STORE_OP_STORE;
                //
                WGpuRenderPassEncoder renderPassEncoder =
                    wgpu_command_encoder_begin_render_pass(commandEncoder, ref renderPassDescriptor);
                // Console.WriteLine("called wgpu_command_encoder_begin_render_pass");

                wgpu_render_pass_encoder_set_pipeline(renderPassEncoder, renderPipeline);
                wgpu_encoder_set_bind_group(renderPassEncoder, 0, uniformBindGroup, null, 0);
                wgpu_render_pass_encoder_set_vertex_buffer(renderPassEncoder, 0, particlesBuffer);
                wgpu_render_pass_encoder_set_vertex_buffer(renderPassEncoder, 1, quadVertexBuffer);
                wgpu_render_pass_encoder_draw(renderPassEncoder, 6, numParticles, 0, 0);
                wgpu_render_pass_encoder_end(renderPassEncoder);
            }

            wgpu_queue_submit_one_and_destroy(queue, wgpu_command_encoder_finish(commandEncoder));

            return 1; // run indefinitely
        }

        [UnmanagedCallersOnly]
        static void ObtainedWebGpuDevice(WGpuDevice result, IntPtr userData)
        {
            if (result.ptr == IntPtr.Zero)
            {
                Console.WriteLine("ObtainedWebGpuDevice was given an invalid device " + result.ptr);
                return;
            }
            Console.WriteLine("ObtainedWebGpuDevice was given device " + result.ptr);

            device = result;
            queue = wgpu_device_get_queue(device);

            canvasContext = wgpu_canvas_get_webgpu_context("canvas");

            SampleInit();
        }

        static Random random = new Random();

        [UnmanagedCallersOnly]
        static void ObtainedWebGpuAdapter(WGpuAdapter result, IntPtr userData)
        {
            adapter = result;
            if (adapter.ptr == IntPtr.Zero)
            {
                Console.WriteLine("ObtainedWebGpuAdapter was given a null GpuAdapter :-(");
                return;
            }
            Console.WriteLine("ObtainedWebGpuAdapter was given GpuAdapter " + result.ptr);

            WGpuDeviceDescriptor deviceDesc = default;
            //TODO deviceDesc In/Out or ref
            wgpu_adapter_request_device_async(adapter, ref deviceDesc, &ObtainedWebGpuDevice, IntPtr.Zero);
        }

        public static void Main()
        {
            Console.WriteLine("Main");
            // // hack to get the function emitted into javascript
            // wgpu_object_destroy(new WGpuObjectBase(0));
            int width = 512;
            int height = 512;
            emscripten_set_canvas_element_size("#canvas", width, height);

            canvasContext = wgpu_canvas_get_webgpu_context("canvas");
            WGpuRequestAdapterOptions options = default;
            options.powerPreference = WGPU_POWER_PREFERENCE_LOW_POWER;
            navigator_gpu_request_adapter_async(ref options, &ObtainedWebGpuAdapter, IntPtr.Zero);
        }
    }
}