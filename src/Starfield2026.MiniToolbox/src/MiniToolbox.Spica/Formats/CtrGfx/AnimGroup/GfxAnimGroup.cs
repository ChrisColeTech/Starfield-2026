using MiniToolbox.Spica.Formats.Common;
using MiniToolbox.Spica.Serialization.Attributes;

namespace MiniToolbox.Spica.Formats.CtrGfx.AnimGroup
{
    [TypeChoice(0x80000000u, typeof(GfxAnimGroup))]
    public class GfxAnimGroup : INamed
    {
        private uint Flags;

        private string _Name;

        public string Name
        {
            get => _Name;
            set => _Name = value ?? throw Exceptions.GetNullException("Name");
        }

        public int MemberType;

        public readonly GfxDict<GfxAnimGroupElement> Elements;

        public int[] BlendOperationTypes;

        public GfxAnimEvaluationTiming EvaluationTiming;

        public GfxAnimGroup()
        {
            Elements = new GfxDict<GfxAnimGroupElement>();
        }
    }
}
