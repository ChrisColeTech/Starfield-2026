using System;

namespace MiniToolbox.Spica.Serialization.Attributes
{
    class PaddingAttribute : Attribute
    {
        public int Size;

        public PaddingAttribute(int Size)
        {
            this.Size = Size;
        }
    }
}
