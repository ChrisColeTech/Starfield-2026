using System.IO;

using MiniToolbox.Garc.Containers;

namespace MiniToolbox.Garc.Models.PocketMonsters
{
    class MM
    {
        /// <summary>
        ///     Loads a MM overworld chibi character model from Pokémon.
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>The Model group with the character meshes</returns>
        public static RenderBase.OModelGroup load(Stream data)
        {
            RenderBase.OModelGroup models = new RenderBase.OModelGroup();

            OContainer container = PkmnContainer.load(data);
            models = BCH.load(new MemoryStream(container.content[0].data));

            return models;
        }
    }
}
