# Interoperate With a Backend

`protobuf-unity` and [`ProtoBinaryManager`](proto-binary-manager.md) together deal with your **offline** save data. What about taking that online? Maybe just to back up the save file for players (without relying on e.g. iCloud), or so you can view, inspect, or award your player something from the server.

## JSON at the client side

The point of Protobuf is often to send everything over the wire with matching `.proto` files waiting on the other end. But what if you are not in control of the receiving side? The key is often JSON serialization, since that is the de-facto standard for interoperability. There is a class called [`Google.Protobuf.JsonFormatter`](https://protobuf.dev/reference/csharp/api-docs/class/google/protobuf/json-formatter/) available from Google's DLL already.

To use it, instantiate the class (or use `JsonFormatter.Default` for quick, config-free formatting) and call `.Format(yourProtobufMessageObject)`. It uses reflection to build key/value pairs of C# variable names and values, which may not be the most efficient solution, but it does the trick. It works well with `repeated` (as a JSON array); it reportedly does not work as well with `any`.

If you only need a backup, you may not need JSON at all — just dump the binary or its base64 and upload the whole thing. But JSON often lets the backend actually **do something** with the data. For example, [Microsoft Azure PlayFab](https://playfab.com/) supports [attaching a JSON object](https://learn.microsoft.com/gaming/playfab/features/data/entities/quickstart) to an entity. With understandable save data available in PlayFab, you can segment and run live ops based on the save (e.g. players who progressed slower), or award points from the server on event completion and sync back to Protobuf on the local device.

## Deciphering Protobuf at the server side

As an alternative to doing JSON on the client and sending it up, you can send Protobuf bytes to the server and deserialize them with the JS version of the generated code.

### Node.js example

Here's how to set up Node's `Crypto` so it decrypts what the C# side encrypted. This pattern works well in Firebase Functions, which spin up a Node server with a lambda fragment, receiving the save file for safekeeping and deciphering it so the server knows its content. Assume you already have a Node `Buffer` of the save data at the server as `saveBuffer`:

```js
function decipher(saveBuffer) {
    // Mirrors `Rfc2898DeriveBytes` in C#. Use the same password, salt, and iteration count.
    const key = pbkdf2Sync(encryptionPassword, encryptionSalt, 5555, 16, 'sha1')

    // Pick the IV from the cipher text.
    const iv = saveBuffer.slice(0, 16)

    // The remaining real content.
    const content = saveBuffer.slice(16)

    // C#'s default when creating `AesCryptoServiceProvider` is CBC mode with PKCS7 padding.
    const decipher = createDecipheriv('aes-128-cbc', key, iv)

    const decrypted = decipher.update(content)
    const final = decipher.final()
    const finalBuffer = Buffer.concat([decrypted, final])

    // Now you have naked Protobuf bytes without encryption, and can obtain a nicely structured class.
    return YourGeneratedProtoClassJs.deserializeBinary(finalBuffer)
}
```

### Compatibility with Google Firebase Firestore

Firestore can store JSON-like data, and the JS Firestore library can store a JS object straight into it. However, not everything is supported, as a JS object is a superset of what Firestore accepts. It cannot store `undefined`, and it cannot store nested arrays. While `undefined` does not exist in JSON, a nested array *is* possible in JSON but cannot be stored in Firestore.

What you get from `YourGeneratedProtoClassJs.deserializeBinary` is not a plain JS object — it is a Protobuf message class instance. There is a `.toObject()` method to convert it, but if you look at what you had as `map<A,B>`, `.toObject()` produces `[A,B][]` instead. That is likely how Protobuf really keeps your `map`. As noted, a nested array can't go straight into Firestore.

After eliminating any `undefined`/`null`, you need to post-process every `map` field (including nested ones), turning `[A,B][]` into a proper JS object by using `A` as the key and `B` as the value. `repeated` fields convert fine — they are just straight arrays.

## Known cross-language bugs

- **`repeated` enum** — it serializes and deserializes fine within C#, but if you get `Unhandled error { AssertionError: Assertion failed` when turning a C#-made buffer into a JS object, it may be [this long-standing bug](https://github.com/protocolbuffers/protobuf/issues/5232). Adding `[packed=false]` did not fix it.
- **Integer map with key 0** — `map<int, Something>` works in C# but can fail to deserialize in JS. Until [this issue](https://github.com/protocolbuffers/protobuf/issues/7713) is fixed, use a non-zero key for integer-keyed maps.
