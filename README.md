# protobuf-unity
Sirawat Pitaksarit / 5argon - Exceed7 Experiments

![settings](https://github.com/5argon/protobuf-unity/raw/master/images/settings.png)

Do you want to integrate [protobuf](https://github.com/google/protobuf) as a data class, game saves, message to the server, etc. in your game? Now you can put those `.proto` files directly in the project, work on it, and have this generate the classes for you.

## Functions
1. When you write a `.proto` file normally you need to use the `protoc` command line to generate C# classes. This plugin automatically find all your `.proto` files in your Unity project, generate them all, and output respective class file at the same place as the `.proto` file. It automatically regenerate when you change any `.proto` file.

If there is an error the plugin will report via the Console. 

2. Now, your generated class will contains `using Google.Protobuf`. Which means you must have Google.Protobuf.dll in your Unity project.

- I did not bundled the protobuf C# lib with this so go check at https://www.nuget.org/packages/Google.Protobuf, press Download manually, use archive extract tools to get the .dll out from nuget package and put it in your Unity project. It contains 2 target : .NET 4.6 and .NET Standard 1.0.
- But if you stay with .NET 3.5 you need to use the unofficial modified package like https://github.com/emikra/protobuf3-cs. 
- The latest version (3.6.0) there is a patch note saying about some movement for this to work with .NET 3.5 and Unity (https://github.com/google/protobuf/blob/master/CHANGES.txt) I don't know if it works with 3.5 fully by now or not.

### Updates
- (1/12/2017) Now include paths is not only the folder of the file that is being compiled, but all folders that has a `.proto` file in your project. Proto includes on the `.proto` file's header does not support `.`, `..` etc. so this way you can use an unqualified name to reference any `.proto` file in your Unity project. Split assembly is available in 2017.3 and it uses folder hierarchy. This can help you split up your proto files.
- (29/07/2018) There is a `package.json` so you could use Unity Package Manager now. Google for how to do it locally and according to Unity Berlin talk we will be able to use UPM with GitHub address directly later. I am preparing for that.

### Problem with iOS + IL2CPP

Now that you can't use mono backend anymore on iOS, there is a problem that IL2CPP is not supporting `System.Reflection.Emit`. Basically you should avoid anything that will trigger reflection as much as possible.

Luckily the most of core funtions does not use reflection. The most likely you will trigger reflection is `protobufClassInstance.ToString()` (Or attempting to `Debug.Log` any of the protobuf instance.) It will then use reflection to scan figure out what is the structure of all the data just to print out pretty JSON-formatted string. To alleviate this you might override `ToString` so that it pull the data out to make a string directly from generated class file's field. I am not sure of other functions that might trigger reflection.

You should see the discussion in [this](https://github.com/google/protobuf/issues/644) and [this](https://github.com/google/protobuf/pull/3794) thread. The gist of it is Unity failed to preserve some information needed for the reflection and it cause the reflection to fail at runtime.

And lastly the latest protobuf (3.6.0) has something related to this issue. Please see https://github.com/google/protobuf/blob/master/CHANGES.txt
So it is recommended to get the latest version!

## Installation 

1. Put files in your Unity project
2. You can access the settings in Preferences > Protobuf. Here you *need* to put a path to your `protoc` executable.

![settings](https://github.com/5argon/protobuf-unity/raw/master/images/settings.png)

3. As soon as you import/reimport/modify (but *not* moving) `.proto` file in your project, it will compile *only that file* to the same location as the file. If you want to temporary stop this there is a checkbox in the settings, then you can manually push the button in there if you like. Note that deleting `.proto` file will not remove its generated class.
 
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

## Problems

Works on macOS. Untested on Windows/Linux since I am not developing games in those environments. If you encountered any problems please use the Issue section or send a PR if you manages to fix it. Thank you.

## License
As this includes protobuf you need to follow Google's license here : https://github.com/google/protobuf/blob/master/LICENSE

For my own Unity code the license is MIT without requiring any attributions. (The part where it says "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software." is not required.) You are welcome to include it if you want.
