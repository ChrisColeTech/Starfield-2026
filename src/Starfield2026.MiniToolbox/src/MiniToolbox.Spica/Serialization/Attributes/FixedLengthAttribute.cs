using System;

namespace MiniToolbox.Spica.Serialization.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    class FixedLengthAttribute : Attribute
    {
        public int Length;

        public FixedLengthAttribute(int Length)
        {
            this.Length = Length;
        }
    }
}
