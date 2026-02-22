using System;

namespace MiniToolbox.Spica.Formats.Generic.COLLADA
{
    public class DAEAsset
    {
        public DateTime created;
        public DateTime modified;
        public string up_axis;

        public DAEAsset()
        {
            created = DateTime.Now;
            modified = DateTime.Now;
            up_axis = "Y_UP";
        }
    }
}
