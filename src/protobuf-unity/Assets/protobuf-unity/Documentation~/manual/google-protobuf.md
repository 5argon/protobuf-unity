# The `Google.Protobuf` Library

Your generated classes reference the [`Google.Protobuf`](https://www.nuget.org/packages/Google.Protobuf) runtime library to serialize to Protobuf binary, and this package's own Runtime assembly uses it too.

**It comes bundled with this package** at `Plugin/Google.Protobuf.dll`, so it works out of the box — there is nothing to download, extract, or choose. The bundled DLL is auto-referenced, so generated classes in the default `Assembly-CSharp` pick it up automatically. If your generated `.cs` lives under its own `.asmdef`, just leave **Auto Referenced** on (or add `Google.Protobuf` to its references).

No extra dependency assemblies are needed either. On the current LTS the default **.NET Standard 2.1** profile provides `Span<T>`, `Memory<T>`, `System.Buffers`, and `System.Runtime.CompilerServices.Unsafe` in the platform BCL, so the bundled `netstandard2.0` DLL resolves everything against the runtime — no `System.Memory.dll` or other shim assemblies. (Confirmed upstream: the request for a Unity-specific `netstandard2.1` build was closed with *"later Unity versions work fine with the netstandard 2.0 version"* — [protocolbuffers/protobuf#9240](https://github.com/protocolbuffers/protobuf/issues/9240).)

## Using a different version (optional)

The bundled DLL covers the common case. If you specifically need another `Google.Protobuf` version, replace `Plugin/Google.Protobuf.dll` with the `netstandard2.0` build from the [NuGet package](https://www.nuget.org/packages/Google.Protobuf) (a `.nupkg` is a zip; the DLL is under `lib/`). Keep your `protoc` version in step with it, since newer `protoc` output can require a newer runtime library.

## Looking ahead: CoreCLR / .NET 10

Unity's [Path to CoreCLR](https://discussions.unity.com/t/path-to-coreclr-2026-upgrade-guide/1714279) replaces Mono with CoreCLR on a .NET 10 BCL in Unity 6.8, at which point .NET Standard 2.1 becomes the only exposed target framework. The bundled `netstandard2.0` DLL remains consumable there; once CoreCLR lands you can optionally swap in the dependency-free `net5.0` build. In short, the trajectory keeps getting simpler, not harder.
