using System.Collections.Generic;
using System.IO;

using MiniToolbox.Garc.Containers;

namespace MiniToolbox.Garc.Textures.PocketMonsters
{
    class PT
    {
        /// <summary>
        ///     Loads all monster textures on a PT Pokémon container.
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>The monster textures</returns>
        public static List<RenderBase.OTexture> load(Stream data)
        {
            List<RenderBase.OTexture> textures = new List<RenderBase.OTexture>();
            RenderBase.OModelGroup models = new RenderBase.OModelGroup();

            OContainer container = PkmnContainer.load(data);
            for (int i = 0; i < container.content.Count; i++)
            {
                FileIO.LoadedFile file = FileIO.load(new MemoryStream(container.content[i].data));
                if (file.type == FileIO.formatType.model) textures.AddRange(((RenderBase.OModelGroup)file.data).texture);
            }

            return textures;
        }
    }
}
