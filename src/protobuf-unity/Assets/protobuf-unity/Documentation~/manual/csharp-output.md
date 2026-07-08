# C# Output Options

**Preferences > Protobuf** has a **Global C# output options** group that maps to protoc's [`--csharp_opt`](https://protobuf.dev/reference/csharp/csharp-generated/#compiler_options) flag, applied to every compile:

- **Global Internal Access** — generate types as `internal` instead of `public` (`internal_access`).
- **Global Serializable** — add `[System.Serializable]` to generated message classes (`serializable`).
- **Global File Extension** — extension for generated files; empty means the default `.cs`, and `.g.cs` is a common choice to mark generated code (`file_extension=`).
- **Global Extra csharp_opt** — appended verbatim (comma-separated) for anything else, e.g. `base_namespace=Example`.

Leave them at their defaults to get the standard `public` classes with `.cs` files.

Note that these options are per-`protoc`-invocation, so e.g. **Internal Access** makes *every* generated type `internal` — protoc has no per-message switch.

## Per-folder overrides

To use different options for different folders, create an override asset: right-click a folder → **Create > Protobuf Unity > C# Output Options (per folder)**. Every `.proto` in that folder (and its subfolders, until a deeper override is found) then compiles with that asset's options **instead of** the global ones — each `.proto` is a separate protoc run.

This lets you, for example, keep most schemas `public` while generating one folder's messages as `internal`. See the importable **Split Compilation** sample.
