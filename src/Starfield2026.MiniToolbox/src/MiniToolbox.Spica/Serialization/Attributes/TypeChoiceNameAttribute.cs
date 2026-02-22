using System;

namespace MiniToolbox.Spica.Serialization.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    class TypeChoiceNameAttribute : Attribute
    {
        public string FieldName;

        public TypeChoiceNameAttribute(string FieldName)
        {
            this.FieldName = FieldName;
        }
    }
}
