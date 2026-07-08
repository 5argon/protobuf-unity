# gRPC service sample

`save_service.proto` defines a `SaveService` with unary and server-streaming RPCs.

> **This sample is not dependency-free.** Importing it only gives you the `.proto`.
> To actually compile what protoc generates, you need two things that this package
> deliberately does **not** bundle: the gRPC codegen plugin and a gRPC runtime.

## 1. The codegen plugin (build time)

Set **Path to grpc** in *Preferences > Protobuf* to the `grpc_csharp_plugin`
executable. It ships in the [`Grpc.Tools`](https://www.nuget.org/packages/Grpc.Tools)
NuGet package (a `.nupkg` is a zip; the binaries are under `tools/<platform>/`).
Match its version to your `protoc`.

With that set, reimporting `save_service.proto` produces `SaveState`/`LoadRequest`
etc. **and** a `SaveServiceGrpc.cs` with the client/server stubs.

If you leave **Path to grpc** empty, the `service` block is ignored and only the
message types are generated — which compiles fine on its own.

## 2. The runtime (run time)

`SaveServiceGrpc.cs` references gRPC runtime types (`Grpc.Core.Api`), so the project
will not compile until a runtime is present. Choose one:

- **Do not use `Grpc.Core`** (the old native C-core library). It is deprecated and
  end-of-life, and Google dropped Unity platform support.
- **grpc-dotnet** (`Grpc.Net.Client`, pure C#) is the current implementation, but it
  runs over HTTP/2 which Unity's Mono/IL2CPP networking doesn't provide natively.
  Add [Cysharp/YetAnotherHttpHandler](https://github.com/Cysharp/YetAnotherHttpHandler)
  to give Unity an HTTP/2 handler so grpc-dotnet works (including on IL2CPP).
- **[MagicOnion](https://github.com/Cysharp/MagicOnion)** is a higher-level RPC
  framework built on that same stack if you want batteries included.
- For WebGL, look at **gRPC-Web** instead of raw HTTP/2.

See the repo README's gRPC section for the same summary.
