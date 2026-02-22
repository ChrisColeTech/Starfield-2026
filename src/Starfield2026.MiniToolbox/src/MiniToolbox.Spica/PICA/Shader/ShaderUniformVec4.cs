using System.Numerics;

namespace MiniToolbox.Spica.PICA.Shader
{
    public class ShaderUniformVec4 : ShaderUniform
    {
        public Vector4 Constant;

        public ShaderUniformVec4() { }

        public ShaderUniformVec4(Vector4 Constant)
        {
            this.Constant = Constant;

            IsConstant = true;
        }
    }
}
