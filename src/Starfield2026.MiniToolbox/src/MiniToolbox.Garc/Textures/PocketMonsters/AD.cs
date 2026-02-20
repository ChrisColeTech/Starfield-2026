using System.IO;

using MiniToolbox.Garc.Containers;

namespace MiniToolbox.Garc.Textures.PocketMonsters
{
    class AD
    {
        /// <summary>
        ///     Loads all map textures (and other data) on a AD Pokémon container.
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>The Model group with textures and stuff</returns>
        public static RenderBase.OModelGroup load(Stream data)
        {
            RenderBase.OModelGroup models = new RenderBase.OModelGroup();

            OContainer container = PkmnContainer.load(data);
            for (int i = 1; i < container.content.Count; i++)
            {
                FileIO.LoadedFile file = FileIO.load(new MemoryStream(container.content[i].data));
                if (file.type == FileIO.formatType.model) models.merge((RenderBase.OModelGroup)file.data);
            }

            return models;
        }
    }
}
