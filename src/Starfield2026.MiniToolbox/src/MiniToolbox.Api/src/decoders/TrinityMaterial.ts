import type { PathString } from '../utils/PathString.js';
import type { TRMaterial, TRTexture, TRFloatParameter, TRVec2fParameter, TRVec3fParameter, TRVec4fParameter, TRSampler } from '../flatbuffers/TR/Model/index.js';

/**
 * Lightweight texture reference (no GL texture loading).
 */
export class TextureRef {
    public Name: string = '';
    public FilePath: string = '';
    public Slot: number = 0;
}

/**
 * Data-only material decoded from TRMTR / Gfx2 material files.
 * Ported from gftool Material.cs — all GL rendering code removed.
 */
export class TrinityMaterial {
    public Name: string = '';
    public ShaderName: string = '';
    public Textures: TextureRef[] = [];
    public ShaderParams: Array<{ Name: string; Value: string }> = [];
    public FloatParams: TRFloatParameter[] = [];
    public Vec2Params: TRVec2fParameter[] = [];
    public Vec3Params: TRVec3fParameter[] = [];
    public Vec4Params: TRVec4fParameter[] = [];
    public Samplers: TRSampler[] = [];

    constructor(modelPath: PathString, trmat: TRMaterial) {
        this.Name = trmat.Name ?? '';
        this.ShaderName = TrinityMaterial.ResolveShaderName(
            trmat.Shader?.length > 0 ? trmat.Shader[0].Name : ''
        );

        this.FloatParams = trmat.FloatParams ?? [];
        this.Vec2Params = trmat.Vec2fParams ?? [];
        this.Vec3Params = trmat.Vec3fParams ?? [];
        this.Vec4Params = trmat.Vec4fParams ?? [];
        this.Samplers = trmat.Samplers ?? [];

        if (trmat.Shader != null && trmat.Shader.length > 0 && trmat.Shader[0].Values != null) {
            for (const param of trmat.Shader[0].Values) {
                this.ShaderParams.push({ Name: param.Name, Value: param.Value });
            }
        }

        for (const tex of trmat.Textures ?? []) {
            this.Textures.push({
                Name: tex.Name,
                FilePath: modelPath.combine(tex.File),
                Slot: tex.Slot
            });
        }
    }

    private static ResolveShaderName(name: string): string {
        if (!name || name.length === 0) {
            return 'Standard';
        }

        switch (name) {
            case 'Opaque': return 'Standard';
            case 'Transparent': return 'Transparent';
            case 'Hair': return 'Hair';
            case 'SSS': return 'SSS';
            case 'EyeClearCoat': return 'EyeClearCoat';
            case 'Unlit': return 'Unlit';
            default: return name;
        }
    }
}
