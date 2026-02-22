namespace MiniToolbox.Spica.Serialization
{
    interface ICustomSerializeCmd
    {
        void SerializeCmd(BinarySerializer Serializer, object Value);
    }
}
