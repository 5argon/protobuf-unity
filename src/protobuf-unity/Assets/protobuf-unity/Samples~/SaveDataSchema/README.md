# protobuf-unity sample

A small game-save schema split across four `.proto` files in two folders, used to
exercise the plugin end to end. Every file shares one package, `game.save`, which
maps to the C# namespace `Game.Save` (see
[packages](https://protobuf.dev/programming-guides/proto3/#packages)).

```
Core/
  common.proto   enums, Vector3, Color32, scalar-type showcase (Blob)
  item.proto     Item; import public "common.proto"
Gameplay/
  player.proto   PlayerSave; imports item.proto across folders
  world.proto    WorldSnapshot; imports common.proto across folders
```

Import graph (each arrow is an `import`):

```
world.proto ─▶ player.proto ─▶ item.proto ─(public)─▶ common.proto
     └─▶ common.proto (across folders, for Vector3)
```

## What it demonstrates

Plugin behavior:

- **Cross-folder imports by bare filename** — `player.proto` in `Gameplay/` does `import "item.proto"` from `Core/`. protobuf-unity adds every `.proto`'s parent folder to protoc's include path, so no `../` relative paths are ever needed.
- **Transitive imports** — compiling `world.proto` pulls in `player.proto`, `item.proto`, and `common.proto`.

Protocol Buffers / proto3 features:

- **One package across many files**, and `package` → C# namespace (no `csharp_namespace` override).
- **Same-package references by simple name** (`Rarity`, `Vector3`) vs **cross-package fully-qualified names** (`google.protobuf.Timestamp`).
- **`import public`** — `item.proto` re-exports `common.proto`, so `player.proto` sees `Vector3` without importing it directly.
- **Well-known types** — `Timestamp`, `Duration`, `Int32Value` (wrappers), `Struct`, `Any`.
- **Field presence** — `optional` (explicit presence, generates `HasSocketCount`) alongside a wrapper type.
- **Scalar types** — `bytes`, `double`, `sint32`, `fixed64`, `sfixed32`, plus the usual `int32`/`uint32`/`uint64`/`float`/`bool`/`string`.
- **Enums** — a zero default, `reserved` values/names, and `allow_alias`.
- **Composite fields** — `repeated`, `map<,>`, `oneof`, nested messages/enums, and referencing a nested type across files (`Item.Enchantment`).
- **`reserved`** field numbers and names for safe schema evolution.

> gRPC `service` blocks are intentionally left out. With a gRPC plugin path set, protoc emits a companion `*Grpc.cs` that needs the gRPC runtime (`Grpc.Core.Api`), which this package doesn't bundle — so it wouldn't compile standalone. gRPC is opt-in (grpc-dotnet + Cysharp/YetAnotherHttpHandler, or MagicOnion); see the repo README's gRPC section.

## Trying it

With a `protoc` path set in **Preferences > Protobuf**, importing or reimporting
any of these `.proto` files generates a matching `.cs` next to it
(`common.proto` → `Common.cs`, and so on), all in the `Game.Save` namespace. If all
four generate without console errors, the plugin — including its cross-folder
include-path handling — is working.

Note: the plugin compiles the single file you just changed, so after editing
`common.proto` its importers aren't auto-regenerated. Use **Force Compilation** in
the settings to rebuild everything at once.
