# ProtoBinaryManager

`ProtoBinaryManager` is a Unity-specific utility that handles physical file save/load of your generated Protobuf classes. It is perfect for game saves, so you can load from binary on the next start-up. It has two versions: a completely `static` utility class, and an `abstract` version that requires some generic typing.

The point of the generic version is that, by providing your Protobuf-generated class `T` as the type parameter, you get a manager just for that specific class `T`. It gives you easy save/load of Protobuf data to disk and back to memory, plus an extra static "active slot" of `T` for even easier management of loaded data (so you don't load it over and over, and only save when necessary).

The most common use of this active slot is a local game save, since nowadays mobile games are single-save and there is usually no explicit load screen where you choose a file. There are methods to implement other save schemes too. And because you subclass it, it opens the door to your own validation logic, which would be impossible with just the `static` utility version.

It also contains basic C# AES encryption. Almost everyone wants it, even though you are likely too lazy to separate the key and salt from your game's code. At least it makes it more difficult for the player to open the serialized Protobuf file in a text editor and see exactly where their money variable is.

```csharp
// Recommended naming is `LocalSave`. The 2nd type param gives you the magic `static` access point later.
public class LocalSave : ProtoBinaryManager<T, LocalSave> {
    // Implement required `abstract` implementations...
}

// Then later you could:

// `.Active` static access point for your save data. Automatically loads from disk and caches.
// `Gold` is a property in your generated `T` class from Protobuf.
LocalSave.Active.Gold += 5555;

// `.Save` — easy static method to save your active save file to disk.
LocalSave.Save();

// The next time you start the game, LocalSave.Active contains your previous state,
// because `.Active` automatically loads from disk.

// Other utilities provided at the `.Manager` static access point.
LocalSave.Manager.BackupActive();
LocalSave.Manager.ReloadActive();
```

See [Interoperate With a Backend](backend.md) for taking these save files online.
