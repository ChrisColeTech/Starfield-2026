using MiniToolbox.Garc.Containers;
using System;
using System.Collections.Generic;
using System.IO;

namespace MiniToolbox.Garc.Models.PocketMonsters
{
    class CM
    {
        /// <summary>
        ///     Loads a CM overworld character model from Pokémon.
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>The Model group with the character meshes</returns>
        public static RenderBase.OModelGroup load(Stream data)
        {
            RenderBase.OModelGroup models = new RenderBase.OModelGroup();

            OContainer container = PkmnContainer.load(data);
            models = GfModel.load(new MemoryStream(container.content[0].data));

            if (container.content.Count > 1 && container.content[1].data != null && container.content[1].data.Length > 4)
            {
                try
                {
                    List<RenderBase.OSkeletalAnimation> anms = GfMotion.load(new MemoryStream(container.content[1].data));
                    foreach (RenderBase.OSkeletalAnimation anm in anms)
                    {
                        models.skeletalAnimation.list.Add(anm);
                    }
                }
                catch (Exception)
                {
                    // Animation data may be malformed for some entries
                }
            }

            return models;
        }
    }
}
