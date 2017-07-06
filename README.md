# protobuf-unity
Sirawat Pitaksarit / 5argon - Exceed7 Experiments

![settings](https://github.com/5argon/protobuf-unity/raw/master/images/settings.png)

Do you want to integrate [protobuf](https://github.com/google/protobuf) as a data class, game saves, message to the server, etc. in your game? Now you can put those `.proto` files directly in the project, work on it, and have this generate the classes for you.

## Functions
1. When you write a `.proto` file normally you need to use the `protoc` command line to generate C# classes. This plugin automatically find all your `.proto` files in your Unity project, generate them all, and output respective class file at the same place as the `.proto` file. It automatically regenerate when you change any `.proto` file.

If there is an error the plugin will report via the Console. 

2. Now, your generated class will contains `using Google.Protobuf`. Officially Google provides NuGet package to work with generated class at https://github.com/google/protobuf/tree/master/csharp but it is for .NET 4.5. If you use Unity2017 and switch on .NET 4.5 in PlayerSettings you will have an option to use Google's official package, but if you stay with .NET 3.5 you need to use the unofficial modified package like https://github.com/emikra/protobuf3-cs.

This plugin bundled the 3.5 compatible .dll extracted from aforementioned NuGet link. It is based on version 3.1.x. Please be careful if the newer `protoc` you use generate a class that does not compile, is not compatible, or compile but produces wrong behaviour with this .dll  (I use `protoc` 3.3.0, the version does not exactly match but it seems fine so far.)
 
## Prerequisites
Requires `protoc`. This plugin does not include `protoc` command and will try to run it from your command line (via .NET `System.Diagnostics.Process.Start`). Please see https://github.com/google/protobuf and install it. Confirm with `protoc --version` in your command prompt/terminal.

## Some notes about Protocol Buffer
For complete understanding I suggest you visit [Google's document](https://developers.google.com/protocol-buffers/docs/overview) but here are some gotchas you might want to know before starting.

- Use CamelCase (with an initial capital) for message names – for example, SongServerRequest. Use underscore_separated_names for field names – for example, song_name.
- By default of C# protoc, the underscore_names will become PascalCase and camelCase in the generated code.
- `.proto` file name matters and Google suggests you use underscore_names.proto. It will become the output file name in PascalCase. (Does not related to the file's content or the message definition inside at all.)
- Field index 1 to 15 has the lowest storage overhead so put fields that likely to occur often in this range.
- The generated C# class will has `sealed partial`.
- You cannot use `enum` as `map`'s key.
- It's not `int` but `int32`. And this data type is not efficient for negative number. (In that case use `sint32`)

![project](https://github.com/5argon/protobuf-unity/raw/master/images/project.png)

![code compare](https://github.com/5argon/protobuf-unity/raw/master/images/codecompare.png)

## Settings

You can access the settings in Preferences > Protobuf.

![settings](https://github.com/5argon/protobuf-unity/raw/master/images/settings.png)

## Problems

Works on macOS. Untested on Windows/Linux since I am not developing games in that environment. If you encountered any problems please use the Issue section or send a PR if you manages to fix it. Thank you.

## License
As this includes protobuf you need to follow Google's license here : https://github.com/google/protobuf/blob/master/LICENSE

For my own Unity code the license is MIT without requiring any attributions. (The part where it says "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software." is not required.)