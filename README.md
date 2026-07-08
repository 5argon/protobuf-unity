# protobuf-unity

Automatic `.proto` file compilation in Unity to C# as you edit them, plus other Protobuf utilities made for games.

Drop your `.proto` files directly into the project, edit them, and the editor script here regenerates the C# classes for you automatically — no manual `protoc` command line, no build step. It also ships [`ProtoBinaryManager`](https://exceed7.com/protobuf-unity/manual/proto-binary-manager.html), a Unity-specific save/load utility (with optional AES encryption) built for game saves.

## Installation

You need the `protoc` compiler installed on your machine — this package runs it, it does not bundle it. See the [installation guide](https://exceed7.com/protobuf-unity/manual/installation.html) for platform-specific steps.

Then install the package with the Unity Package Manager (**Window > Package Manager > + > Add package from git URL**):

```
https://github.com/5argon/protobuf-unity.git?path=src/protobuf-unity/Assets/protobuf-unity
```

Or add it to your `Packages/manifest.json`:

```json
"com.e7.protobuf-unity": "https://github.com/5argon/protobuf-unity.git?path=src/protobuf-unity/Assets/protobuf-unity"
```

Append `#<tag>` to pin a version. The `?path=` points at the innermost package folder in this repo's [UniTask-style](https://github.com/Cysharp/UniTask) layout — you can also clone and open `src/protobuf-unity` directly as a Unity project to develop the package itself.

## Documentation

Full documentation, guides, and the C# API reference live at **<https://exceed7.com/protobuf-unity>**:

- [How it works](https://exceed7.com/protobuf-unity/manual/index.html) — automatic compilation and project-wide `import` resolution
- [The bundled `Google.Protobuf` library](https://exceed7.com/protobuf-unity/manual/google-protobuf.html)
- [C# output options](https://exceed7.com/protobuf-unity/manual/csharp-output.html) and [gRPC](https://exceed7.com/protobuf-unity/manual/grpc.html)
- [`ProtoBinaryManager`](https://exceed7.com/protobuf-unity/manual/proto-binary-manager.html) and [interoperating with a backend](https://exceed7.com/protobuf-unity/manual/backend.html)

## License

For your generated code you must follow [Google's Protobuf license](https://github.com/protocolbuffers/protobuf/blob/main/LICENSE). For this package's own code, the license is MIT.
