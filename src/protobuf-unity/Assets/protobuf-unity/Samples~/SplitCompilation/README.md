# Split compilation sample

Two `.proto` files that generate C# with **different `--csharp_opt` settings**, driven by a
per-folder options asset.

```
SplitCompilation/
  Public/
    public_data.proto          -> PublicData   (public,   from GLOBAL settings)
  Internal/
    internal_data.proto        -> InternalData (internal, from the folder asset)
    ProtobufCsharpOptions.asset  (Internal Access = on)
```

## How it works

`Internal/` contains a **Protobuf Unity/C# Output Options (per folder)** asset with
**Internal Access** ticked. The compiler resolves options per `.proto` by looking for the
nearest such asset in the file's folder or any ancestor:

- `Internal/internal_data.proto` finds the asset in its own folder, so it compiles with
  `--csharp_opt=internal_access` → `internal sealed partial class InternalData`.
- `Public/public_data.proto` has no options asset above it, so it uses the **global**
  defaults from *Preferences > Protobuf* → `public sealed partial class PublicData`.

A per-folder asset **replaces** the global options for its subtree (it doesn't merge), and
each `.proto` is its own protoc run, so the two files can legitimately generate with
different visibility.

## Trying it

After importing this sample, run **Preferences > Protobuf > Force Compilation** once so both
files regenerate with their resolved options (on first import the asset and the `.proto` may
be processed together before the asset is queryable). Then check the generated
`InternalData.cs` (internal) vs `PublicData.cs` (public).

To make your own per-folder override: right-click in a folder →
**Create > Protobuf Unity > C# Output Options (per folder)**, then tick the options you want.
