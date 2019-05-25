namespace E7.Protobuf
{
    public interface ILocalSave<SELF>
    {
        /// <summary>
        /// You may be in a situation that `new()` was already taken (like generated from Protobuf) 
        /// and you would like to do more to initialize the save file.
        /// 
        /// You can simply return the `new()` if that is not the case.
        /// </summary>
        SELF CreateEmptySaveFile();
    }
}