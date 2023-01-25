# CSharpWebGpu
C# examples using WebGPU and NativeAOT

Contains some examples that use https://github.com/juj/wasm_webgpu, emscripten, Ninja, and NativeAOT-LLVM (https://github.com/dotnet/runtimelab/tree/feature/NativeAOT-LLVM) to enable WebGPU with C#.

```
git clone https://github.com/juj/wasm_webgpu
git clone https://github.com/yowl/CSharpWebGpu
git clone https://github.com/emscripten-core/emsdk
cd emsdk
# Consult with https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-emscripten.cmd#L14-L18
# for actual commit. That may change without change here.
git checkout b4fd475
./emsdk install 3.1.23
./emsdk activate 3.1.23
cd ..
cd wasm_webgpu
mkdir build
cd build
# for Windows 
# scoop install ninja
emcmake cmake ../samples -DCMAKE_BUILD_TYPE=Debug
cmake --build .
cd ../../CSharpWebGpu/Triangle
```

Compile from the `Triangle` folder with 
```
dotnet publish --self-contained -r browser-wasm /p:MSBuildEnableWorkloadResolver=false
```

And serve with any web server, for example using command `npx http-server bin/Debug/net7.0/browser-wasm/publish/`.  Use Chrome with WebGPU enabled to view.  WebGPU is currently behind a flag in Chrome, see https://developer.chrome.com/docs/web-platform/webgpu/ to turn it on.

