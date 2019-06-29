# protobuf-unity

![settings](.images/settings.png)

Do you want to integrate [protobuf](https://github.com/google/protobuf) as a data class, game saves, message to the server, etc. in your game? Now you can put those `.proto` files directly in the project, work on it, and have the editor script in here generate the classes for you.

# Installation 

1. Install `protoc` on the machine. This plugin does not include `protoc` command and will try to run it from your command line (via .NET `System.Diagnostics.Process.Start`). Please see https://github.com/google/protobuf and install it. Confirm with `protoc --version` in your command prompt/terminal.
2. Put files in your Unity project. This is also Unity Package Manager compatible. You can pull from online to your project directly.
3. You can access the settings in Preferences > Protobuf. Here you *need* to put a path to your `protoc` executable.

![settings](.images/settings.png)

As soon as you import/reimport/modify (but *not* moving) `.proto` file in your project, it will compile *only that file* to the same location as the file. If you want to temporary stop this there is a checkbox in the settings, then you can manually push the button in there if you like. Note that deleting `.proto` file will *not* remove its generated class.

# Overview

1. When you write a `.proto` file normally you need to use the `protoc` command line to generate C# classes. This plugin automatically find all your `.proto` files in your Unity project, generate them all, and output respective class file at the same place as the `.proto` file. It automatically regenerate when you change any `.proto` file. If there is an error the plugin will report via the Console. 

2. Your generated class will contains `using Google.Protobuf`, so you have to add `Google.Protobuf.dll` precompiled library in your Unity project or link to your `asmdef`. This plugin itself doesn't need it, and I didn't bundle the `dll` along with this.

- Check at https://www.nuget.org/packages/Google.Protobuf, press Download manually, use archive extract tools to get the .dll out from nuget package and put it in your Unity project. It contains targets such as .NET 4.6 and .NET Standard 1.0/2.0.
- But if you stay with .NET 3.5 you need to use the unofficial modified package like https://github.com/emikra/protobuf3-cs. 
- The latest version (3.6.0) there is a patch note saying about some movement for this to work with .NET 3.5 and Unity (https://github.com/google/protobuf/blob/master/CHANGES.txt) I don't know if it works with 3.5 fully by now or not.

## Why Protobuf?

- Smaller size, no big luggages like type information when if you used `System.Serializable` + `BinaryFormatter`.
- `System.Serializable` is terrible on both forward and backward compatibility unpredictably, may affect your business badly. (e.g. you wanna change how your game's monetization works, that timed ads that was saved in the save file is now unnecessary, but because inflexibility you have to live with them forever in the code.)
- For Unity-specific problem, just rename your `asmdef` and the serialized file is now unreadable without binder hacks because `BinaryFormatter` needs fully qualified assembly name.
- Protobuf is flexible that it is a generic C# library, and the serialized file could potentially be read in other languages like on your game server. For more Unity-tuned serialization, you may want to check out [Odin Serializer](https://github.com/TeamSirenix/odin-serializer).
- Protobuf-generated C# class is powerful. It comes with sensible `partial` and some useful data merging methods which otherwise would be tedious and buggy for a class-type variable. (e.g. it understands how to handle list-like and dictionary-like data, the `repeated` field and `map` field.)
- Programming in `.proto` to generate a C# class is simply faster and more readable than C# to get the same function. (e.g. has properties, null checks, bells and whistles, and not just all C# `public` fields.)

## Problem with iOS + IL2CPP

Now that you can't use mono backend anymore on iOS, there is a problem that IL2CPP is not supporting `System.Reflection.Emit`. Basically you should avoid anything that will trigger reflection as much as possible.

Luckily the most of core funtions does not use reflection. The most likely you will trigger reflection is `protobufClassInstance.ToString()` (Or attempting to `Debug.Log` any of the protobuf instance.) It will then use reflection to figure out what is the structure of all the data to print out pretty JSON-formatted string. To alleviate this you might override `ToString` so that it pull the data out to make a string directly from generated class file's field. I am not sure of other functions that might trigger reflection.

You should see the discussion in [this](https://github.com/google/protobuf/issues/644) and [this](https://github.com/google/protobuf/pull/3794) thread. The gist of it is Unity failed to preserve some information needed for the reflection and it cause the reflection to fail at runtime.

And lastly the latest protobuf (3.6.0) has something related to this issue. Please see https://github.com/google/protobuf/blob/master/CHANGES.txt
So it is recommended to get the latest version!

## Some more notes about Protocol Buffer

For complete understanding I suggest you visit [Google's document](https://developers.google.com/protocol-buffers/docs/overview) but here are some gotchas you might want to know before starting.

- Use CamelCase (with an initial capital) for message names, for example, SongServerRequest. Use underscore_separated_names for field names – for example, song_name.
- By default of C# `protoc`, the `underscore_names` will become `PascalCase` and `camelCase` in the generated code.
- `.proto` file name matters and Google suggests you use `underscore_names.proto`. It will become the output file name in `PascalCase`. (Does not related to the file's content or the message definition inside at all.)
- The comment in your `.proto` file will carry over to your generated class and fields if that comment is over them. Multiline supported.
- Field index 1 to 15 has the lowest storage overhead so put fields that likely to occur often in this range.
- The generated C# class will has `sealed partial`. You could write more properties to design new access or write point.
- You cannot use `enum` as `map`'s key.
- You cannot use duplicated `enum` name even if they are not in the same type. You may have to prefix your `enum` especially if they sounded generic like `None`.
- It's not `int` but `int32`. And this data type is not efficient for negative number. (In that case use `sint32`)
- If you put `//` comment (or multiline) over a field or message definition, it will be transferred nicely to C# comment.
- It is [possible to generate a C# namespace](https://developers.google.com/protocol-buffers/docs/reference/csharp-generated#structure).

![project](.images/project.png)

![code compare](.images/codecompare.png)

# Problems in generated C# code

There are some problems with Protobuf-generated C# that I am not quite content with : 

- The generated properties are all `public get` and `public set`, this maybe not desirable. For example your `Gem` property could be modified by everyone and that's bug-prone. You probably prefer some kind of `PurchaseWithGem(iapItem)` method in your `partial` that decreases your `Gem` and keep the setter `private`.
- The class contains `partial`, I would like to use `partial` feature and don't want to make a completely new class as a wrapper to this protobuf-generated class. It would be easier to handle the serialization and data management. Also I don't want to redo all the protobuf-generated utility methods like `MergeFrom` or deep `Clone`.
- Some fields in `proto` like `map` are useful as Unity couldn't even serialize `Dictionary` properly, but it is even more likely than normal fields that you don't want anyone to access this freely and add things to it. Imagine a `map<string,string>` describing friend's UID code to the string representation of `DateTime` of when they last online. It doesn't make sense to allow access to this map because `string` doesn't make sense. You want it completely `private` then write a method accessor like `RememberLastOnline(friend, dateTime)` to modify its value, and potentially call the save method to write to disk at the same time.
- These unwanted accessors show up in your intellisense and you don't want to see them.

One could utilize the [Compiler Plugin feature](https://developers.google.com/protocol-buffers/docs/reference/other#plugins), but I think it is overkill. I think I am fine with just some dumb RegEx over generated C# classes in Unity. In the preference menu, there will be several post-processing options available when it is done.

# ProtoBinaryManager

This is a Unity-specific utility to deal with physical file save-load of your generated protobuf class. This is perfect for game saves so you can load it from binary on the next start up. It has 2 versions, a completely `static` utility class and an `abstract` version which requires some generic typing.

The point of generic version is that, by providing your Protobuf-generated class `T` in the type parameter, you will get a manager just for that specific class `T` to easily save and load Protobuf data to disk and back to memory, plus an extra static "active slot" of that `T` for an even easier management of loaded data. (So you don't load it over and over, and save when necessary.) The most common use of this active slot is as a local game saves, since nowadays mobile games are single-save and there is usually no explicit load screen where you choose your save file. There are methods you can use to implement other game save schemes. And because you subclass it, it open ways for your validation logic which would be impossible with just the `static` utility version.

It also contains some basic C# AES encryption, I think almost everyone wants it even though you are likely too lazy to separate key and salt from your game's code. At least it is more difficult for the player to just open the serialized protobuf file with Note Pad and see exactly where his money variable is.

```csharp
//Recommended naming is `LocalSave`. The LocalSave 2nd type param will give you magic `static` access point later.
public class LocalSave : ProtoBinaryManager<T, LocalSave> { 
    //Implement required `abstract` implementations...
}

// Then later you could :

//`.Active` static access point for your save data. Automatic load from disk and cache. `Gold` is a property in your generated `T` class from Protobuf.
LocalSave.Active.Gold += 5555;

//.Save easy static method to save your active save file to the disk.
LocalSave.Save();

//When you start the game the next time, LocalSave.Active will contains your previous state because .Active automatically load from disk.

//Other utilities provided in `.Manager` static access point.
LocalSave.Manager.BackupActive();
LocalSave.Manager.ReloadActive();
```

## License

As this will need Protobuf you need to follow Google's license here : https://github.com/google/protobuf/blob/master/LICENSE. For my own Unity code the license is MIT.