# How It Works

When you write a `.proto` file you normally need the `protoc` command line to generate C# classes. This plugin automatically finds all the `.proto` files in your Unity project, generates them, and outputs each resulting class file **at the same location as its `.proto` file**. It regenerates whenever you change any `.proto` file, and reports errors through the Console.

As soon as you import, reimport, or modify (but *not* move) a `.proto` file in your project, it compiles *only that file* to the same folder. If you want to stop this temporarily there is a checkbox in the settings; you can then push the compile button manually when you like. Note that deleting a `.proto` file does **not** remove its generated class.

## Project-wide `import` resolution

You can use the `import` statement in your `.proto` file. Normally `protoc` looks for imported files in the `--proto_path` folders passed on the command line. With protobuf-unity, `--proto_path` is automatically set to **all parent folders of every `.proto` file in your Unity project, combined**. That way you can `import` any `.proto` file within your project.

- You cannot use a relative path such as `../` in an `import`.
- Imported files should not live inside a UPM package — the path base is `Application.dataPath`, and packages are outside of it.
- The `google/protobuf/` path is also usable, so you can pull in [well-known types](well-known-types.md) or extend custom options.

## Why Protobuf?

- Smaller size — no heavy luggage like the type information you get from `System.Serializable` + `BinaryFormatter`.
- You could use Unity's `ScriptableObject`, but a well-known gotcha is that Unity can't serialize `Dictionary`. Here you can use [`map<,>`](https://protobuf.dev/programming-guides/proto3/#maps) together with the available Protobuf types. [`Any`](https://protobuf.dev/programming-guides/proto3/#any) and [`oneof`](https://protobuf.dev/programming-guides/proto3/#oneof) are useful too.
- `System.Serializable` is unpredictable on both forward and backward compatibility, which can hurt your business (e.g. if you change how monetization works, a timed-ad field saved long ago can be stuck in your code forever).
- For a Unity-specific problem: rename an `asmdef` and a `BinaryFormatter`-serialized file becomes unreadable without binder hacks, because it needs the fully qualified assembly name.
- Protobuf is a generic C# library, so the serialized file can be read in other languages — for example on your game server. For more Unity-tuned serialization you may also want to look at [Odin Serializer](https://github.com/TeamSirenix/odin-serializer).
- Protobuf-generated classes are powerful: sensible `partial` support and data-merging methods that would otherwise be tedious and buggy for class-type variables (it understands `repeated` and `map` fields).
- Writing a `.proto` to generate a C# class is faster and more readable than writing the equivalent C# by hand (you get properties, null checks, and more — not just `public` fields).

Here is an interesting rebuttal against Protobuf: <http://reasonablypolymorphic.com/blog/protos-are-wrong/> — and a counter-argument from the author: <https://news.ycombinator.com/item?id=18190005>. Use your own judgement about whether it fits your project.

For a complete understanding, read [Google's documentation](https://protobuf.dev/overview/). See also [Notes & Gotchas](notes.md) for things worth knowing before you start.
