# gRPC

gRPC is **optional** — you only need it if your `.proto` files declare `service` blocks. When the **Path to grpc** setting is populated, `protoc` also runs the C# gRPC generator and emits a `*Grpc.cs` next to each service file. Leave it empty to skip gRPC entirely; service blocks are then ignored and only message types generate.

1. Get the `grpc_csharp_plugin` executable from the [`Grpc.Tools`](https://www.nuget.org/packages/Grpc.Tools) NuGet package (a `.nupkg` is a zip; the binaries are under `tools/<platform>/`). Match its version to your `protoc`.
2. Set **Path to grpc** in **Preferences > Protobuf** to that executable.

The generated `*Grpc.cs` stubs need a gRPC **runtime**, which this package does **not** bundle (it's an app-specific, opt-in choice).

> [!WARNING]
> The old native `Grpc.Core` library is deprecated and end-of-life — Google dropped Unity support, so don't start with it.

The current path is the managed **grpc-dotnet** (`Grpc.Net.Client`) plus an HTTP/2 handler for Unity/IL2CPP such as [Cysharp/YetAnotherHttpHandler](https://github.com/Cysharp/YetAnotherHttpHandler), or the higher-level [MagicOnion](https://github.com/Cysharp/MagicOnion) framework. See the importable **gRPC Service** sample for details.
