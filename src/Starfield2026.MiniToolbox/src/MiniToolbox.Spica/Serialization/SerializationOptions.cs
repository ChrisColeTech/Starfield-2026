namespace MiniToolbox.Spica.Serialization
{
    struct SerializationOptions
    {
        public LengthPos   LenPos;
        public PointerType PtrType;

        public SerializationOptions(LengthPos LenPos, PointerType PtrType)
        {
            this.LenPos  = LenPos;
            this.PtrType = PtrType;
        }
    }
}
