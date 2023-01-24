# CSharpWebGpu
C# examples using WebGPU and NativeAOT

Contains some examples that use https://github.com/juj/wasm_webgpu , emscripten, and NativeAOT-LLVM (https://github.com/dotnet/runtimelab/tree/feature/NativeAOT-LLVM) to enable WebGPU with C#.

Compile from the `Triangle` folder with 
```
dotnet publish --self-contained -r browser-wasm /p:MSBuildEnableWorkloadResolver=false
```

And serve with and web server.  Use Chrome with WebGPU enabled to view.  WebGPU is currently behind a flag in Chrome, see https://developer.chrome.com/docs/web-platform/webgpu/ to turn it on.
