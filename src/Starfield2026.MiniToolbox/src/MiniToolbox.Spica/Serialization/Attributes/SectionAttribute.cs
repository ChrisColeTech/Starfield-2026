using System;

namespace MiniToolbox.Spica.Serialization.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    class SectionAttribute : Attribute
    {
        public uint SectionId;

        public SectionAttribute(uint SectionId)
        {
            this.SectionId = SectionId;
        }
    }
}
