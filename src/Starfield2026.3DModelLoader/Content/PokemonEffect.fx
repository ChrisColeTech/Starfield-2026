// PokemonEffect.fx — Custom shader for Pokemon models with vertex color alpha blending
// Formula: finalColor = lerp(textureColor, white, vertexColor.a) * lighting

float4x4 World;
float4x4 View;
float4x4 Projection;

// Lighting
float3 AmbientColor;

float3 Light0Direction;
float3 Light0DiffuseColor;
float3 Light0SpecularColor;

float3 Light1Direction;
float3 Light1DiffuseColor;

float3 Light2Direction;
float3 Light2DiffuseColor;

float3 SpecularColor;
float SpecularPower;

float3 CameraPosition;

// Texture
texture ModelTexture;
sampler2D TextureSampler = sampler_state {
    Texture = (ModelTexture);
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position  : SV_POSITION;
    float2 TexCoord  : TEXCOORD0;
    float3 Normal    : TEXCOORD1;
    float3 WorldPos  : TEXCOORD2;
    float4 VtxColor  : COLOR0;
};

VertexShaderOutput VS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    float4 worldPos = mul(input.Position, World);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(mul(worldPos, View), Projection);
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = input.TexCoord;
    output.VtxColor = input.Color;
    
    return output;
}

float4 PS(VertexShaderOutput input) : SV_TARGET
{
    // Sample texture
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    
    // Apply vertex color alpha blending: lerp(textureColor, white, vertexAlpha)
    // Vertex alpha controls how much white blending occurs (0 = texture only, 1 = full white)
    float3 baseColor = lerp(texColor.rgb, float3(1.0, 1.0, 1.0), input.VtxColor.a);
    
    // Lighting
    float3 normal = normalize(input.Normal);
    float3 viewDir = normalize(CameraPosition - input.WorldPos);
    
    float3 diffuse = float3(0.0, 0.0, 0.0);
    float3 specular = float3(0.0, 0.0, 0.0);
    
    // Light 0 (key light)
    float3 l0 = normalize(Light0Direction);
    float ndotl0 = max(dot(normal, -l0), 0.0);
    diffuse += Light0DiffuseColor * ndotl0;
    float3 h0 = normalize(-l0 + viewDir);
    float ndoth0 = max(dot(normal, h0), 0.0);
    specular += Light0SpecularColor * pow(ndoth0, SpecularPower) * ndotl0;
    
    // Light 1 (fill light)
    float3 l1 = normalize(Light1Direction);
    float ndotl1 = max(dot(normal, -l1), 0.0);
    diffuse += Light1DiffuseColor * ndotl1;
    
    // Light 2 (rim light)
    float3 l2 = normalize(Light2Direction);
    float ndotl2 = max(dot(normal, -l2), 0.0);
    diffuse += Light2DiffuseColor * ndotl2;
    
    float3 finalColor = baseColor * (AmbientColor + diffuse) + specular * SpecularColor;
    finalColor = saturate(finalColor);
    
    return float4(finalColor, texColor.a);
}

technique PokemonTechnique
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
};
