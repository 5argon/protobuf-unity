# Changelog

All notable changes to this package are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [2.0.0]

### Fixed

- The static save-data slot (`Active`) and `Manager` are now reset when entering Play Mode, so the package behaves correctly under Fast Enter Play Mode / no domain reload (default for new projects in Unity 6.6+, and the only option in 6.8). Previously the in-memory active save from a prior Play session would persist instead of reloading from disk. A public `ClearStaticState()` was also added so you can reset it manually (e.g. when returning to a main menu).

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

- Rolling archives (versioned backups) on `ProtoBinaryManager`, alongside the existing single-slot
  backup which is unchanged. `ArchiveActive()` writes the active save into a dated archive file
  (`SaveData.archive.2026-03-14.save`), self-throttling to one snapshot per `ArchiveIntervalDays`
  (call it as often as you like) and pruning to `ArchiveRetentionCount`. Round it out with
  `ListArchives()` (newest first), `RestoreFromArchive(intervalsBack)`, `PruneArchives(keepCount)`
  and `ClearArchives()`. The archive methods never touch the single-slot backup. The interval,
  retention, archive suffix, and the clock (`Now`) are all overridable `protected virtual` members.
- Edit-mode test assembly `E7.ProtobufUnity.Tests` covering the whole save API and the archive
  logic, with a committed `TestSave.proto` and its generated C#.
- Importable samples: **Save Data Schema**, **gRPC Service**, and **Split Compilation**.
- Per-folder C# output options asset (**Create > Protobuf Unity > C# Output Options**), so
  different folders can compile with different `--csharp_opt` settings.
