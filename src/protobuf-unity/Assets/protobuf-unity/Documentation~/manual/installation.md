# Installation

## 1. Install `protoc` on your machine

This plugin does **not** include the `protoc` command — it runs it from your command line (via .NET `System.Diagnostics.Process.Start`). The version of `protoc` you use depends on how high a C# [`Google.Protobuf`](google-protobuf.md) library version you want, because a newer `protoc` can generate code that an older runtime library cannot use.

**macOS** — with [Homebrew](https://brew.sh):

```
brew install protobuf
```

The Homebrew formula is called `protobuf`; it bundles the `protoc` executable. (If you also want the gRPC C# plugin for the gRPC field in the settings, `brew install grpc` provides `grpc_csharp_plugin`.)

**Windows** — with a package manager, either:

```
choco install protoc      # Chocolatey
scoop install protobuf    # Scoop
```

Or install manually: download `protoc-<version>-win64.zip` from the [Protobuf releases page](https://github.com/protocolbuffers/protobuf/releases), extract it, and add its `bin` folder to your `PATH`.

**Any platform** — the [releases page](https://github.com/protocolbuffers/protobuf/releases) also has prebuilt `protoc` binaries you can unzip and point at directly.

After installing, confirm with `protoc --version`. To find the exact path to paste into the plugin settings, run `which protoc` (macOS/Linux) or `where protoc` (Windows) — for example `/opt/homebrew/bin/protoc` on Apple Silicon, `/usr/local/bin/protoc` on Intel Macs, or `C:\ProgramData\chocolatey\bin\protoc.exe` on Windows.

## 2. Add the package to your project

It is Unity Package Manager compatible. In **Window > Package Manager > + > Add package from git URL**, paste:

```
https://github.com/5argon/protobuf-unity.git?path=src/protobuf-unity/Assets/protobuf-unity
```

The `?path=` points at the innermost package folder in this repo's [UniTask-style](https://github.com/Cysharp/UniTask) layout. You can also clone and open `src/protobuf-unity` directly as a Unity project to develop the package itself.

To reference the package from your own assembly, the runtime assembly name is `E7.ProtobufUnity`.

## 3. Point the plugin at `protoc`

Open **Preferences > Protobuf**. Here you *need* to set the path to your `protoc` executable.

![The Protobuf preferences panel](images/settings.png)

That's it — edit a `.proto` file and its C# class is generated next to it.
