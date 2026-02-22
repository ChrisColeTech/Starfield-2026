using MiniToolbox.Spica.Formats.Common;
using MiniToolbox.Spica.Formats.CtrGfx.AnimGroup;

using System.Collections.Generic;

namespace MiniToolbox.Spica.Formats.CtrGfx
{
    public class GfxNode : GfxObject
    {
        private int BranchVisible;

        private bool _IsBranchVisible;

        public bool IsBranchVisible
        {
            get => _IsBranchVisible;
            set
            {
                _IsBranchVisible = value;

                BranchVisible = BitUtils.SetBit(BranchVisible, value, 0);
            }
        }

        public readonly List<GfxObject> Childs;

        public readonly GfxDict<GfxAnimGroup> AnimationsGroup;

        public GfxNode()
        {
            Childs = new List<GfxObject>();

            AnimationsGroup = new GfxDict<GfxAnimGroup>();
        }
    }
}