# The `Google.Protobuf` Library

Your generated classes reference the [`Google.Protobuf`](https://www.nuget.org/packages/Google.Protobuf) runtime library to serialize to Protobuf binary, and this package's own Runtime assembly uses it too.

**It comes bundled with this package** at `Plugin/Google.Protobuf.dll`, so it works out of the box — there is nothing to download, extract, or choose. The bundled DLL is auto-referenced, so generated classes in the default `Assembly-CSharp` pick it up automatically. If your generated `.cs` lives under its own `.asmdef`, just leave **Auto Referenced** on (or add `Google.Protobuf` to its references).

The bundled DLL is the `netstandard2.0` build, which has no in-box `Span`/`Memory` and therefore
references three assemblies by name: `System.Memory` (4.0.1.1), `System.Buffers` (4.0.2.0), and
`System.Runtime.CompilerServices.Unsafe` (4.0.4.1). Only the last is bundled alongside it, because
only the last is missing — **Unity already supplies the other two** and a redundant copy is one more
assembly to keep in step for no benefit.

That asymmetry is worth knowing, because the reference list on its own suggests all three are needed.
It is equally tempting to assume none are — netstandard2.1 does provide `Span<T>`, `Memory<T>`,
`MemoryMarshal` and `BinaryPrimitives` as *types* — but a precompiled assembly binds to the *assembly
identity* it was compiled against, and type availability and reference resolution are different
things. What settles it is what Unity ships under each name: `System.Memory` and `System.Buffers`
exist both in Mono (4.0.99.0) and in the netstandard compat shims that player builds resolve against
(4.0.2.0 and 4.0.3.0), all higher than what is asked for and all binding. Nothing named
`System.Runtime.CompilerServices.Unsafe` appears in either. Verified on Unity 6000.3: without that
one, the first serialization of a message carrying a non-empty string fails with

```
FileNotFoundException: Could not load file or assembly
'System.Runtime.CompilerServices.Unsafe, Version=4.0.4.1'
  at Google.Protobuf.WritingPrimitives.WriteString(...)
```

Two properties make this easy to miss. Proto3 omits empty strings from the wire, so a project whose
string fields happen to be empty serializes indefinitely without incident; and only the first missing
assembly is reported, so resolving one reveals the next. A project on **.NET Framework** does not hit
it at all, and the reason is in the runtime rather than the profile: Mono's `net_4_x` directory ships
`System.Runtime.CompilerServices.Unsafe` (4.0.4.0, close enough for Mono's version-tolerant load),
while the `unityjit` directory that backs .NET Standard ships none at all. That asymmetry is why the
upstream report ([protocolbuffers/protobuf#9240](https://github.com/protocolbuffers/protobuf/issues/9240),
closed with *"later Unity versions work fine with the netstandard 2.0 version"*) does not describe
every case. The bundled copy is harmless on .NET Framework: verified on both profiles, with the
runtime's own equivalent present, without conflict.

## Using a different version (optional)

The bundled DLL covers the common case. If you specifically need another `Google.Protobuf` version, replace `Plugin/Google.Protobuf.dll` with the `netstandard2.0` build from the [NuGet package](https://www.nuget.org/packages/Google.Protobuf) (a `.nupkg` is a zip; the DLL is under `lib/`). Keep your `protoc` version in step with it, since newer `protoc` output can require a newer runtime library.

## Looking ahead: CoreCLR / .NET 10

Unity's [Path to CoreCLR](https://discussions.unity.com/t/path-to-coreclr-2026-upgrade-guide/1714279)
replaces Mono with CoreCLR on a .NET 10 BCL in Unity 6.8, at which point .NET 10 becomes the only
exposed target framework and precompiled assemblies targeting anything other than .NET Standard 2.1
may break.

Two consequences for this package. The bundled `netstandard2.0` runtime is on the wrong side of that
line and should move to a `netstandard2.1` build before 6.8. And the remaining shim becomes redundant
once the BCL supplies it, so it is expected to be removed at that point rather than carried forward —
Unity 6000.6 already supplies it, as the table below shows.

### How many shims are actually required

Measured, rather than inferred from the reference list, by deleting shims from `Plugin/` and running
this package's own Edit Mode suite (19 tests, several of which round-trip a non-empty string through
`ProtoBinaryManager`):

| Unity | Editor assemblies | Bundled shims | Result |
|---|---|---|---|
| 6000.3 | .NET Framework | `Unsafe` only | 19/19 |
| 6000.3 | .NET Framework | none | 18/19 |
| 6000.3 | .NET Standard | none | 18/19 |
| 6000.3 | .NET Standard | `Unsafe` only | 19/19 |
| 6000.6 | .NET Standard | none | 19/19 |

Two conclusions, both with the stock netstandard2.0 assembly and no custom build.

**`System.Memory` and `System.Buffers` are not required**, and are not bundled. Mono ships its own at
4.0.99.0 and the netstandard compat shims that player builds resolve against ship 4.0.2.0 and
4.0.3.0, all satisfying the 4.0.1.1 and 4.0.2.0 requests — higher versions of the same major, which
bind. Only `System.Runtime.CompilerServices.Unsafe` genuinely has nothing behind it, which is why it
was the assembly named in the exception.

**Unity 6000.6 needs no shim at all.** It adds `Resources/Scripting/BCLExtensions/`, whose
`runtime/netstandard2.1/` supplies `Unsafe` at 6.0.0.0 and satisfies the 4.0.4.1 request. Unity
6000.3 has no `BCLExtensions` directory, so one shim is still required there.

Note the Edit Mode caveat: this exercises Mono in the editor, not IL2CPP. A player build resolves
references at build time and has not been measured here.

### What a netstandard2.1 build would change

Building `Google.Protobuf` 3.35.1 from source against `netstandard2.1` — same `DefineConstants` as
the netstandard2.0 target — leaves the assembly referencing exactly two things:

```
netstandard, Version=2.1.0.0
System.Runtime.CompilerServices.Unsafe, Version=6.0.0.0
```

`System.Memory` and `System.Buffers` leave the reference list outright, since netstandard2.1 has
every memory API protobuf touches in-box: `Span`, `Memory`, `MemoryMarshal`, `BinaryPrimitives`,
`MemoryExtensions`, `ArrayPool`, `IBufferWriter`, `ReadOnlySequence`. `Unsafe` is the sole exception,
used unconditionally in `WritingPrimitives` and `ParsingPrimitives` with no `#if` fallback, and not
part of the netstandard2.1 contract.

That tidies the reference list but changes no shim count the table above does not already reach, and
it carries a real cost: **NuGet publishes no `netstandard2.1` build** — the package ships `net45`,
`netstandard1.1`, `netstandard2.0` and `net5.0` only — so it means building from source and carrying
that step at every version bump. The reason to do it is the target framework itself rather than the
shims, so it belongs with the CoreCLR move above and not before it.

The build is at least a true drop-in when that time comes: the repository's own signing key
reproduces the official identity, `Google.Protobuf, Version=3.35.1.0,
PublicKeyToken=a7d26565bac4d604`. It also costs nothing in .NET Framework compatibility inside Unity,
which is the usual objection — both of Unity's profiles carry a `netstandard` **2.1.0.0** facade,
including `unity-4.8-api`, so the general rule that .NET Framework tops out at netstandard2.0 does
not hold for Unity's Mono.
