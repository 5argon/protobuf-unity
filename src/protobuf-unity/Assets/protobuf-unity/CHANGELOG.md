# Changelog

All notable changes to this package are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [2.0.0]

### Changed

- Repository restructured to the UniTask-style layout: the repo root is now a real Unity
  project and the package lives at `src/protobuf-unity/Assets/protobuf-unity`. Install via
  `https://github.com/5argon/protobuf-unity.git?path=src/protobuf-unity/Assets/protobuf-unity`.
- Minimum Unity version is now 6.3 LTS (6000.3).
- The `Google.Protobuf` runtime is bundled at `Plugin/Google.Protobuf.dll`, so serialization
  works out of the box with no extra dependency assemblies on .NET Standard 2.1.
- Documentation moved to a DocFX site (with a generated C# API reference) hosted at
  <https://exceed7.com/protobuf-unity>.

### Added

- Importable samples: **Save Data Schema**, **gRPC Service**, and **Split Compilation**.
- Per-folder C# output options asset (**Create > Protobuf Unity > C# Output Options**), so
  different folders can compile with different `--csharp_opt` settings.
