import * as fs from 'fs';
import * as path from 'path';
import { BitmapData } from '../../Textures/Codecs/TextureCodec.js';
import { MeshUtils } from '../Mesh/MeshUtils.js';
import {
  OVector2,
  OVector3,
  OVector4,
  OMatrix,
  OBone,
  OVertex,
  OMesh,
  OMaterial,
  OModel,
  OModelGroup,
  OTexture,
  OTextureCoordinator,
  OTextureMapper,
  OTextureWrap,
  OTextureMinFilter,
  OTextureMagFilter,
  OAnimationKeyFrame,
  OAnimationKeyFrameGroup,
  OAnimationFrame,
  OSkeletalAnimationBone,
  OSkeletalAnimation,
  OAnimationListBase,
} from '../../Core/RenderBase.js';

// ---------------------------------------------------------------------------
// Matrix helpers using RenderBase OMatrix
// ---------------------------------------------------------------------------
function createMatrix(): OMatrix {
  return new OMatrix();
}

function mulMatrix(a: OMatrix, b: OMatrix): OMatrix {
  return OMatrix.mul(a, b);
}

function invertMatrix(mat: OMatrix): OMatrix {
  return mat.invert();
}

function scaleMatrix(s: OVector3): OMatrix {
  return OMatrix.scaleVec3(s);
}

function rotateXMatrix(angle: number): OMatrix {
  return OMatrix.rotateX(angle);
}

function rotateYMatrix(angle: number): OMatrix {
  return OMatrix.rotateY(angle);
}

function rotateZMatrix(angle: number): OMatrix {
  return OMatrix.rotateZ(angle);
}

function translateMatrix(p: OVector3): OMatrix {
  return OMatrix.translateVec3(p);
}

// ---------------------------------------------------------------------------
// PNG helper
// ---------------------------------------------------------------------------
/**
 * Saves a BitmapData to a PNG file.
 * Uses the `sharp` package if available, otherwise writes raw RGBA with a
 * TODO comment.
 */
async function savePng(bitmap: BitmapData, filePath: string): Promise<void> {
  try {
    // Dynamic import to avoid hard dependency
    const sharp = (await import('sharp')).default;
    await sharp(bitmap.data, {
      raw: { width: bitmap.width, height: bitmap.height, channels: 4 },
    }).png().toFile(filePath);
  } catch {
    // Fallback: write raw RGBA -- caller will need to convert to PNG later
    fs.writeFileSync(filePath + '.rgba', bitmap.data);
    // TODO: install sharp for proper PNG encoding, or provide another encoder
  }
}

// ---------------------------------------------------------------------------
// float formatting helper
// ---------------------------------------------------------------------------
function fstr(v: number): string {
  // Match C#'s InvariantCulture float formatting
  return v.toString();
}

function floatArrayStr(arr: number[]): string {
  return arr.map(fstr).join(' ');
}

// ---------------------------------------------------------------------------
// XML escaping helper
// ---------------------------------------------------------------------------
function xmlEscape(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/** Strip known image extensions (.tga, .png, etc.) to avoid double-extension filenames. */
function stripImageExt(name: string): string {
  return name.replace(/\.(tga|png|bmp|jpg|jpeg)$/i, '');
}

// ---------------------------------------------------------------------------
// DAE exporter
// ---------------------------------------------------------------------------
const AnimationFramesPerSecond = 30;

export class DAE {
  static DiagnosticLogging = false;

  /**
   * Exports a Model to the COLLADA (.dae) format.
   * @param model - The model group containing models, textures, and animations
   * @param fileName - The output file path
   * @param modelIndex - Index of the model to export
   * @param skeletalAnimationIndex - Optional index of the skeletal animation (-1 = none)
   */
  static export(
    model: OModelGroup,
    fileName: string,
    modelIndex: number,
    skeletalAnimationIndex: number = -1,
  ): void {
    const mdl = model.model[modelIndex];
    const now = new Date().toISOString().replace(/\.\d{3}/, '');

    // Save texture PNG files alongside the DAE
    const outDir = path.dirname(fileName);
    for (const tex of model.texture) {
      const pngPath = path.join(outDir, stripImageExt(tex.name) + '.png');
      savePng(tex.texture as BitmapData, pngPath);
    }

    // ---- Collect data for XML generation ----
    const xml: string[] = [];
    xml.push('<?xml version="1.0" encoding="utf-8"?>');
    xml.push('<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">');

    // asset
    xml.push('\t<asset>');
    xml.push(`\t\t<created>${now}</created>`);
    xml.push(`\t\t<modified>${now}</modified>`);
    xml.push('\t\t<up_axis>Y_UP</up_axis>');
    xml.push('\t</asset>');

    // library_images
    xml.push('\t<library_images>');
    for (const tex of model.texture) {
      xml.push(`\t\t<image id="${xmlEscape(stripImageExt(tex.name))}_id" name="${xmlEscape(stripImageExt(tex.name))}">`);
      xml.push(`\t\t\t<init_from>./${xmlEscape(stripImageExt(tex.name))}.png</init_from>`);
      xml.push('\t\t</image>');
    }
    xml.push('\t</library_images>');

    // library_materials & library_effects
    const materialsXml: string[] = [];
    const effectsXml: string[] = [];
    for (const mat of mdl.material) {
      materialsXml.push(`\t\t<material id="${xmlEscape(mat.name)}_mat_id" name="${xmlEscape(mat.name)}_mat">`);
      materialsXml.push(`\t\t\t<instance_effect url="#eff_${xmlEscape(mat.name)}_id"/>`);
      materialsXml.push('\t\t</material>');

      const surfaceSid = 'img_surface_' + mat.name;
      const samplerSid = 'img_sampler_' + mat.name;

      const wrapMap: Record<number, string> = {
        [OTextureWrap.repeat]: 'WRAP',
        [OTextureWrap.mirroredRepeat]: 'MIRROR',
        [OTextureWrap.clampToEdge]: 'CLAMP',
        [OTextureWrap.clampToBorder]: 'BORDER',
      };
      const minFilterMap: Record<number, string> = {
        [OTextureMinFilter.linearMipmapLinear]: 'LINEAR_MIPMAP_LINEAR',
        [OTextureMinFilter.linearMipmapNearest]: 'LINEAR_MIPMAP_NEAREST',
        [OTextureMinFilter.nearestMipmapLinear]: 'NEAREST_MIPMAP_LINEAR',
        [OTextureMinFilter.nearestMipmapNearest]: 'NEAREST_MIPMAP_NEAREST',
      };
      const magFilterMap: Record<number, string> = {
        [OTextureMagFilter.linear]: 'LINEAR',
        [OTextureMagFilter.nearest]: 'NEAREST',
      };

      const mapper = mat.textureMapper?.[0];
      const wrapS = mapper ? (wrapMap[mapper.wrapU] ?? 'NONE') : 'NONE';
      const wrapT = mapper ? (wrapMap[mapper.wrapV] ?? 'NONE') : 'NONE';
      const minF = mapper ? (minFilterMap[mapper.minFilter] ?? 'NONE') : 'NONE';
      const magF = mapper ? (magFilterMap[mapper.magFilter] ?? 'NONE') : 'NONE';

      effectsXml.push(`\t\t<effect id="eff_${xmlEscape(mat.name)}_id" name="eff_${xmlEscape(mat.name)}">`);
      effectsXml.push('\t\t\t<profile_COMMON>');
      effectsXml.push(`\t\t\t\t<newparam sid="${xmlEscape(surfaceSid)}">`);
      effectsXml.push('\t\t\t\t\t<surface type="2D">');
      effectsXml.push(`\t\t\t\t\t\t<init_from>${xmlEscape(stripImageExt(mat.name0))}_id</init_from>`);
      effectsXml.push('\t\t\t\t\t\t<format>PNG</format>');
      effectsXml.push('\t\t\t\t\t</surface>');
      effectsXml.push('\t\t\t\t</newparam>');
      effectsXml.push(`\t\t\t\t<newparam sid="${xmlEscape(samplerSid)}">`);
      effectsXml.push('\t\t\t\t\t<sampler2D>');
      effectsXml.push(`\t\t\t\t\t\t<source>${xmlEscape(surfaceSid)}</source>`);
      effectsXml.push(`\t\t\t\t\t\t<wrap_s>${wrapS}</wrap_s>`);
      effectsXml.push(`\t\t\t\t\t\t<wrap_t>${wrapT}</wrap_t>`);
      effectsXml.push(`\t\t\t\t\t\t<minfilter>${minF}</minfilter>`);
      effectsXml.push(`\t\t\t\t\t\t<magfilter>${magF}</magfilter>`);
      effectsXml.push(`\t\t\t\t\t\t<mipfilter>${magF}</mipfilter>`);
      effectsXml.push('\t\t\t\t\t</sampler2D>');
      effectsXml.push('\t\t\t\t</newparam>');
      effectsXml.push('\t\t\t\t<technique sid="img_technique">');
      effectsXml.push('\t\t\t\t\t<phong>');
      effectsXml.push('\t\t\t\t\t\t<emission><color>0 0 0 1</color></emission>');
      effectsXml.push('\t\t\t\t\t\t<ambient><color>0 0 0 1</color></ambient>');
      effectsXml.push('\t\t\t\t\t\t<diffuse>');
      effectsXml.push(`\t\t\t\t\t\t\t<texture texture="${xmlEscape(samplerSid)}" texcoord="uv"/>`);
      effectsXml.push('\t\t\t\t\t\t</diffuse>');
      effectsXml.push('\t\t\t\t\t\t<specular><color>1 1 1 1</color></specular>');
      effectsXml.push('\t\t\t\t\t</phong>');
      effectsXml.push('\t\t\t\t</technique>');
      effectsXml.push('\t\t\t</profile_COMMON>');
      effectsXml.push('\t\t</effect>');
    }

    xml.push('\t<library_materials>');
    xml.push(...materialsXml);
    xml.push('\t</library_materials>');

    xml.push('\t<library_effects>');
    xml.push(...effectsXml);
    xml.push('\t</library_effects>');

    // Pre-compute joint names and inv bind poses
    let jointNames = '';
    let invBindPoses = '';
    for (let index = 0; index < mdl.skeleton.length; index++) {
      let transform = createMatrix();
      transform = DAE.transformSkeleton(mdl.skeleton, index, transform);
      jointNames += mdl.skeleton[index].name ?? '';
      invBindPoses += DAE.matrixToString(invertMatrix(transform));
      if (index < mdl.skeleton.length - 1) {
        jointNames += ' ';
        invBindPoses += ' ';
      }
    }

    // library_geometries + library_controllers
    const geometriesXml: string[] = [];
    const controllersXml: string[] = [];
    const vsNodes: string[] = [];
    let meshIndex = 0;

    for (const obj of mdl.mesh) {
      const meshMaterial = mdl.material[obj.materialId];
      if (!meshMaterial) continue; // skip meshes with missing material
      const meshName = 'mesh_' + meshIndex++ + '_' + obj.name;

      const mesh = MeshUtils.optimizeMesh<OVertex>(obj);
      const positions: number[] = [];
      const normals: number[] = [];
      const uv0: number[] = [];
      const uv1: number[] = [];
      const uv2: number[] = [];
      const colors: number[] = [];

      for (const vtx of mesh.vertices) {
        positions.push(vtx.position.x, vtx.position.y, vtx.position.z);
        if (mesh.hasNormal) normals.push(vtx.normal.x, vtx.normal.y, vtx.normal.z);
        if (mesh.texUVCount > 0) {
          const t = DAE.applyTextureCoordinator(vtx.texture0, meshMaterial.textureCoordinator[0]);
          uv0.push(t.x, t.y);
        }
        if (mesh.texUVCount > 1) {
          const t = DAE.applyTextureCoordinator(vtx.texture1, meshMaterial.textureCoordinator[1]);
          uv1.push(t.x, t.y);
        }
        if (mesh.texUVCount > 2) {
          const t = DAE.applyTextureCoordinator(vtx.texture2, meshMaterial.textureCoordinator[2]);
          uv2.push(t.x, t.y);
        }
        if (mesh.hasColor) {
          colors.push(
            ((vtx.diffuseColor >> 16) & 0xff) / 255,
            ((vtx.diffuseColor >> 8) & 0xff) / 255,
            (vtx.diffuseColor & 0xff) / 255,
            ((vtx.diffuseColor >> 24) & 0xff) / 255,
          );
        }
      }

      // Build geometry XML
      geometriesXml.push(`\t\t<geometry id="${meshName}_id" name="${meshName}">`);
      geometriesXml.push('\t\t\t<mesh>');

      // position source
      const posId = meshName + '_position_id';
      const posArrId = meshName + '_position_array_id';
      geometriesXml.push(`\t\t\t\t<source id="${posId}" name="${meshName}_position">`);
      geometriesXml.push(`\t\t\t\t\t<float_array id="${posArrId}" count="${positions.length}">${floatArrayStr(positions)}</float_array>`);
      geometriesXml.push('\t\t\t\t\t<technique_common>');
      geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${posArrId}" count="${mesh.vertices.length}" stride="3">`);
      geometriesXml.push('\t\t\t\t\t\t\t<param name="X" type="float"/>');
      geometriesXml.push('\t\t\t\t\t\t\t<param name="Y" type="float"/>');
      geometriesXml.push('\t\t\t\t\t\t\t<param name="Z" type="float"/>');
      geometriesXml.push('\t\t\t\t\t\t</accessor>');
      geometriesXml.push('\t\t\t\t\t</technique_common>');
      geometriesXml.push('\t\t\t\t</source>');

      // normal source
      let normalId = '';
      if (mesh.hasNormal) {
        normalId = meshName + '_normal_id';
        const normArrId = meshName + '_normal_array_id';
        geometriesXml.push(`\t\t\t\t<source id="${normalId}" name="${meshName}_normal">`);
        geometriesXml.push(`\t\t\t\t\t<float_array id="${normArrId}" count="${normals.length}">${floatArrayStr(normals)}</float_array>`);
        geometriesXml.push('\t\t\t\t\t<technique_common>');
        geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${normArrId}" count="${mesh.vertices.length}" stride="3">`);
        geometriesXml.push('\t\t\t\t\t\t\t<param name="X" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="Y" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="Z" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t</accessor>');
        geometriesXml.push('\t\t\t\t\t</technique_common>');
        geometriesXml.push('\t\t\t\t</source>');
      }

      // uv sources
      const uvIds: string[] = [];
      const uvArrays = [uv0, uv1, uv2];
      for (let i = 0; i < mesh.texUVCount; i++) {
        const uvId = meshName + '_uv' + i + '_id';
        uvIds.push(uvId);
        const uvArrId = meshName + '_uv' + i + '_array_id';
        geometriesXml.push(`\t\t\t\t<source id="${uvId}" name="${meshName}_uv${i}">`);
        geometriesXml.push(`\t\t\t\t\t<float_array id="${uvArrId}" count="${uvArrays[i].length}">${floatArrayStr(uvArrays[i])}</float_array>`);
        geometriesXml.push('\t\t\t\t\t<technique_common>');
        geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${uvArrId}" count="${mesh.vertices.length}" stride="2">`);
        geometriesXml.push('\t\t\t\t\t\t\t<param name="S" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="T" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t</accessor>');
        geometriesXml.push('\t\t\t\t\t</technique_common>');
        geometriesXml.push('\t\t\t\t</source>');
      }

      // color source
      let colorId = '';
      if (mesh.hasColor) {
        colorId = meshName + '_color_id';
        const colArrId = meshName + '_color_array_id';
        geometriesXml.push(`\t\t\t\t<source id="${colorId}" name="${meshName}_color">`);
        geometriesXml.push(`\t\t\t\t\t<float_array id="${colArrId}" count="${colors.length}">${floatArrayStr(colors)}</float_array>`);
        geometriesXml.push('\t\t\t\t\t<technique_common>');
        geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${colArrId}" count="${mesh.vertices.length}" stride="4">`);
        geometriesXml.push('\t\t\t\t\t\t\t<param name="R" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="G" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="B" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="A" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t</accessor>');
        geometriesXml.push('\t\t\t\t\t</technique_common>');
        geometriesXml.push('\t\t\t\t</source>');
      }

      // vertices
      const verticesId = meshName + '_vertices_id';
      geometriesXml.push(`\t\t\t\t<vertices id="${verticesId}">`);
      geometriesXml.push(`\t\t\t\t\t<input semantic="POSITION" source="#${posId}"/>`);
      geometriesXml.push('\t\t\t\t</vertices>');

      // triangles
      geometriesXml.push(`\t\t\t\t<triangles material="${xmlEscape(meshMaterial.name)}" count="${Math.floor(mesh.indices.length / 3)}">`);
      geometriesXml.push(`\t\t\t\t\t<input semantic="VERTEX" source="#${verticesId}"/>`);
      if (mesh.hasNormal) geometriesXml.push(`\t\t\t\t\t<input semantic="NORMAL" source="#${normalId}"/>`);
      if (mesh.hasColor) geometriesXml.push(`\t\t\t\t\t<input semantic="COLOR" source="#${colorId}"/>`);
      for (let i = 0; i < mesh.texUVCount; i++) {
        const setAttr = i > 0 ? ` set="${i}"` : '';
        geometriesXml.push(`\t\t\t\t\t<input semantic="TEXCOORD" source="#${uvIds[i]}"${setAttr}/>`);
      }
      geometriesXml.push(`\t\t\t\t\t<p>${mesh.indices.join(' ')}</p>`);
      geometriesXml.push('\t\t\t\t</triangles>');
      geometriesXml.push('\t\t\t</mesh>');
      geometriesXml.push('\t\t</geometry>');

      // Controller (skinning)
      const hasNode = obj.vertices.length > 0 && obj.vertices[0].node.length > 0;
      const hasWeight = obj.vertices.length > 0 && obj.vertices[0].weight.length > 0;
      const hasController = hasNode && hasWeight && mdl.skeleton.length > 0;
      let controllerId = '';

      if (hasController) {
        controllerId = meshName + '_ctrl_id';

        const wArr: string[] = [];
        const vcountArr: string[] = [];
        const vArr: string[] = [];
        const wLookBack = new Float64Array(32);
        let wLookBackIndex = 0;
        let buffLen = 0;
        let wIndex = 0;
        let wCount = 0;

        for (const vtx of mesh.vertices) {
          const count = Math.min(vtx.node.length, vtx.weight.length);
          vcountArr.push(count.toString());
          for (let n = 0; n < count; n++) {
            vArr.push(vtx.node[n].toString());
            let found = false;
            let bPos = (wLookBackIndex - 1) & 0x1f;
            for (let i = 0; i < buffLen; i++) {
              if (wLookBack[bPos] === vtx.weight[n]) {
                vArr.push((wIndex - (i + 1)).toString());
                found = true;
                break;
              }
              bPos = (bPos - 1) & 0x1f;
            }
            if (!found) {
              vArr.push((wIndex++).toString());
              wArr.push(fstr(vtx.weight[n]));
              wCount++;
              wLookBack[wLookBackIndex] = vtx.weight[n];
              wLookBackIndex = (wLookBackIndex + 1) & 0x1f;
              if (buffLen < 32) buffLen++;
            }
          }
        }

        const jointsId = meshName + '_ctrl_joint_names_id';
        const jointsArrId = meshName + '_ctrl_joint_names_array_id';
        const bindPosesId = meshName + '_ctrl_inv_bind_poses_id';
        const bindPosesArrId = meshName + '_ctrl_inv_bind_poses_array_id';
        const weightsId = meshName + '_ctrl_weights_id';
        const weightsArrId = meshName + '_ctrl_weights_array_id';

        controllersXml.push(`\t\t<controller id="${controllerId}">`);
        controllersXml.push(`\t\t\t<skin source="#${meshName}_id">`);
        controllersXml.push(`\t\t\t\t<bind_shape_matrix>${DAE.matrixToString(createMatrix())}</bind_shape_matrix>`);

        // joint names source
        controllersXml.push(`\t\t\t\t<source id="${jointsId}">`);
        controllersXml.push(`\t\t\t\t\t<Name_array id="${jointsArrId}" count="${mdl.skeleton.length}">${jointNames}</Name_array>`);
        controllersXml.push('\t\t\t\t\t<technique_common>');
        controllersXml.push(`\t\t\t\t\t\t<accessor source="#${jointsArrId}" count="${mdl.skeleton.length}" stride="1">`);
        controllersXml.push('\t\t\t\t\t\t\t<param name="JOINT" type="Name"/>');
        controllersXml.push('\t\t\t\t\t\t</accessor>');
        controllersXml.push('\t\t\t\t\t</technique_common>');
        controllersXml.push('\t\t\t\t</source>');

        // inv bind poses source
        controllersXml.push(`\t\t\t\t<source id="${bindPosesId}">`);
        controllersXml.push(`\t\t\t\t\t<float_array id="${bindPosesArrId}" count="${mdl.skeleton.length * 16}">${invBindPoses}</float_array>`);
        controllersXml.push('\t\t\t\t\t<technique_common>');
        controllersXml.push(`\t\t\t\t\t\t<accessor source="#${bindPosesArrId}" count="${mdl.skeleton.length}" stride="16">`);
        controllersXml.push('\t\t\t\t\t\t\t<param name="TRANSFORM" type="float4x4"/>');
        controllersXml.push('\t\t\t\t\t\t</accessor>');
        controllersXml.push('\t\t\t\t\t</technique_common>');
        controllersXml.push('\t\t\t\t</source>');

        // weights source
        controllersXml.push(`\t\t\t\t<source id="${weightsId}">`);
        controllersXml.push(`\t\t\t\t\t<float_array id="${weightsArrId}" count="${wCount}">${wArr.join(' ')}</float_array>`);
        controllersXml.push('\t\t\t\t\t<technique_common>');
        controllersXml.push(`\t\t\t\t\t\t<accessor source="#${weightsArrId}" count="${wCount}" stride="1">`);
        controllersXml.push('\t\t\t\t\t\t\t<param name="WEIGHT" type="float"/>');
        controllersXml.push('\t\t\t\t\t\t</accessor>');
        controllersXml.push('\t\t\t\t\t</technique_common>');
        controllersXml.push('\t\t\t\t</source>');

        // joints
        controllersXml.push('\t\t\t\t<joints>');
        controllersXml.push(`\t\t\t\t\t<input semantic="JOINT" source="#${jointsId}"/>`);
        controllersXml.push(`\t\t\t\t\t<input semantic="INV_BIND_MATRIX" source="#${bindPosesId}"/>`);
        controllersXml.push('\t\t\t\t</joints>');

        // vertex_weights
        controllersXml.push(`\t\t\t\t<vertex_weights count="${mesh.vertices.length}">`);
        controllersXml.push(`\t\t\t\t\t<input semantic="JOINT" source="#${jointsId}" offset="0"/>`);
        controllersXml.push(`\t\t\t\t\t<input semantic="WEIGHT" source="#${weightsId}" offset="1"/>`);
        controllersXml.push(`\t\t\t\t\t<vcount>${vcountArr.join(' ')}</vcount>`);
        controllersXml.push(`\t\t\t\t\t<v>${vArr.join(' ')}</v>`);
        controllersXml.push('\t\t\t\t</vertex_weights>');
        controllersXml.push('\t\t\t</skin>');
        controllersXml.push('\t\t</controller>');
      }

      // Visual scene node for this mesh
      const vsnName = 'vsn_' + meshName;
      vsNodes.push(`\t\t\t<node id="${vsnName}_id" name="${vsnName}" type="NODE">`);
      vsNodes.push(`\t\t\t\t<matrix>${DAE.matrixToString(createMatrix())}</matrix>`);

      const bindMatXml = (matName: string, uvCount: number): string[] => {
        const bm: string[] = [];
        bm.push('\t\t\t\t\t<bind_material>');
        bm.push('\t\t\t\t\t\t<technique_common>');
        let imLine = `\t\t\t\t\t\t\t<instance_material symbol="${xmlEscape(matName)}" target="#${xmlEscape(matName)}_mat_id"`;
        if (uvCount > 0) {
          imLine += '>';
          bm.push(imLine);
          bm.push('\t\t\t\t\t\t\t\t<bind_vertex_input semantic="uv" input_semantic="TEXCOORD" input_set="0"/>');
          bm.push('\t\t\t\t\t\t\t</instance_material>');
        } else {
          bm.push(imLine + '/>');
        }
        bm.push('\t\t\t\t\t\t</technique_common>');
        bm.push('\t\t\t\t\t</bind_material>');
        return bm;
      };

      if (hasController) {
        vsNodes.push(`\t\t\t\t<instance_controller url="#${controllerId}">`);
        vsNodes.push(`\t\t\t\t\t<skeleton>#${xmlEscape(mdl.skeleton[0].name ?? '')}_bone_id</skeleton>`);
        vsNodes.push(...bindMatXml(meshMaterial.name, mesh.texUVCount));
        vsNodes.push('\t\t\t\t</instance_controller>');
      } else {
        vsNodes.push(`\t\t\t\t<instance_geometry url="#${meshName}_id">`);
        vsNodes.push(...bindMatXml(meshMaterial.name, mesh.texUVCount));
        vsNodes.push('\t\t\t\t</instance_geometry>');
      }

      vsNodes.push('\t\t\t</node>');
    }

    xml.push('\t<library_geometries>');
    xml.push(...geometriesXml);
    xml.push('\t</library_geometries>');

    if (controllersXml.length > 0) {
      xml.push('\t<library_controllers>');
      xml.push(...controllersXml);
      xml.push('\t</library_controllers>');
    }

    // library_animations
    const animationsXml: string[] = [];
    if (skeletalAnimationIndex >= 0) {
      DAE.exportAnimation(animationsXml, model, mdl, skeletalAnimationIndex);
    }

    if (animationsXml.length > 0) {
      xml.push('\t<library_animations>');
      xml.push(...animationsXml);
      xml.push('\t</library_animations>');
    }

    // library_visual_scenes
    const vsName = 'vs_' + mdl.name;
    const vsId = vsName + '_id';
    xml.push('\t<library_visual_scenes>');
    xml.push(`\t\t<visual_scene id="${vsId}" name="${vsName}">`);
    // Write skeleton
    if (mdl.skeleton.length > 0) {
      DAE.writeSkeletonXml(mdl.skeleton, 0, xml, '\t\t\t');
    }
    xml.push(...vsNodes);
    xml.push('\t\t</visual_scene>');
    xml.push('\t</library_visual_scenes>');

    // scene
    xml.push('\t<scene>');
    xml.push(`\t\t<instance_visual_scene url="#${vsId}"/>`);
    xml.push('\t</scene>');
    xml.push('</COLLADA>');

    fs.writeFileSync(fileName, xml.join('\n'), 'utf-8');
  }

  /**
   * Exports model-only DAE (geometry, skeleton, skin controllers, materials, textures).
   * Does NOT emit library_animations at all.
   * Used for the split model+clips workflow.
   */
  static exportModelOnly(
    model: OModelGroup,
    fileName: string,
    modelIndex: number,
  ): void {
    const mdl = model.model[modelIndex];
    const now = new Date().toISOString().replace(/\.\d{3}/, '');

    // Save texture PNG files alongside the DAE
    const outDir = path.dirname(fileName);
    for (const tex of model.texture) {
      const pngPath = path.join(outDir, stripImageExt(tex.name) + '.png');
      savePng(tex.texture as BitmapData, pngPath);
    }

    const xml: string[] = [];
    xml.push('<?xml version="1.0" encoding="utf-8"?>');
    xml.push('<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">');

    // asset
    xml.push('\t<asset>');
    xml.push(`\t\t<created>${now}</created>`);
    xml.push(`\t\t<modified>${now}</modified>`);
    xml.push('\t\t<up_axis>Y_UP</up_axis>');
    xml.push('\t</asset>');

    // library_images
    xml.push('\t<library_images>');
    for (const tex of model.texture) {
      xml.push(`\t\t<image id="${xmlEscape(stripImageExt(tex.name))}_id" name="${xmlEscape(stripImageExt(tex.name))}">`);
      xml.push(`\t\t\t<init_from>./${xmlEscape(stripImageExt(tex.name))}.png</init_from>`);
      xml.push('\t\t</image>');
    }
    xml.push('\t</library_images>');

    // library_materials & library_effects
    const materialsXml: string[] = [];
    const effectsXml: string[] = [];
    for (const mat of mdl.material) {
      materialsXml.push(`\t\t<material id="${xmlEscape(mat.name)}_mat_id" name="${xmlEscape(mat.name)}_mat">`);
      materialsXml.push(`\t\t\t<instance_effect url="#eff_${xmlEscape(mat.name)}_id"/>`);
      materialsXml.push('\t\t</material>');

      const surfaceSid = 'img_surface_' + mat.name;
      const samplerSid = 'img_sampler_' + mat.name;

      const wrapMap: Record<number, string> = {
        [OTextureWrap.repeat]: 'WRAP',
        [OTextureWrap.mirroredRepeat]: 'MIRROR',
        [OTextureWrap.clampToEdge]: 'CLAMP',
        [OTextureWrap.clampToBorder]: 'BORDER',
      };
      const minFilterMap: Record<number, string> = {
        [OTextureMinFilter.linearMipmapLinear]: 'LINEAR_MIPMAP_LINEAR',
        [OTextureMinFilter.linearMipmapNearest]: 'LINEAR_MIPMAP_NEAREST',
        [OTextureMinFilter.nearestMipmapLinear]: 'NEAREST_MIPMAP_LINEAR',
        [OTextureMinFilter.nearestMipmapNearest]: 'NEAREST_MIPMAP_NEAREST',
      };
      const magFilterMap: Record<number, string> = {
        [OTextureMagFilter.linear]: 'LINEAR',
        [OTextureMagFilter.nearest]: 'NEAREST',
      };

      const mapper = mat.textureMapper?.[0];
      const wrapS = mapper ? (wrapMap[mapper.wrapU] ?? 'NONE') : 'NONE';
      const wrapT = mapper ? (wrapMap[mapper.wrapV] ?? 'NONE') : 'NONE';
      const minF = mapper ? (minFilterMap[mapper.minFilter] ?? 'NONE') : 'NONE';
      const magF = mapper ? (magFilterMap[mapper.magFilter] ?? 'NONE') : 'NONE';

      effectsXml.push(`\t\t<effect id="eff_${xmlEscape(mat.name)}_id" name="eff_${xmlEscape(mat.name)}">`);
      effectsXml.push('\t\t\t<profile_COMMON>');
      effectsXml.push(`\t\t\t\t<newparam sid="${xmlEscape(surfaceSid)}">`);
      effectsXml.push('\t\t\t\t\t<surface type="2D">');
      effectsXml.push(`\t\t\t\t\t\t<init_from>${xmlEscape(stripImageExt(mat.name0))}_id</init_from>`);
      effectsXml.push('\t\t\t\t\t\t<format>PNG</format>');
      effectsXml.push('\t\t\t\t\t</surface>');
      effectsXml.push('\t\t\t\t</newparam>');
      effectsXml.push(`\t\t\t\t<newparam sid="${xmlEscape(samplerSid)}">`);
      effectsXml.push('\t\t\t\t\t<sampler2D>');
      effectsXml.push(`\t\t\t\t\t\t<source>${xmlEscape(surfaceSid)}</source>`);
      effectsXml.push(`\t\t\t\t\t\t<wrap_s>${wrapS}</wrap_s>`);
      effectsXml.push(`\t\t\t\t\t\t<wrap_t>${wrapT}</wrap_t>`);
      effectsXml.push(`\t\t\t\t\t\t<minfilter>${minF}</minfilter>`);
      effectsXml.push(`\t\t\t\t\t\t<magfilter>${magF}</magfilter>`);
      effectsXml.push(`\t\t\t\t\t\t<mipfilter>${magF}</mipfilter>`);
      effectsXml.push('\t\t\t\t\t</sampler2D>');
      effectsXml.push('\t\t\t\t</newparam>');
      effectsXml.push('\t\t\t\t<technique sid="img_technique">');
      effectsXml.push('\t\t\t\t\t<phong>');
      effectsXml.push('\t\t\t\t\t\t<emission><color>0 0 0 1</color></emission>');
      effectsXml.push('\t\t\t\t\t\t<ambient><color>0 0 0 1</color></ambient>');
      effectsXml.push('\t\t\t\t\t\t<diffuse>');
      effectsXml.push(`\t\t\t\t\t\t\t<texture texture="${xmlEscape(samplerSid)}" texcoord="uv"/>`);
      effectsXml.push('\t\t\t\t\t\t</diffuse>');
      effectsXml.push('\t\t\t\t\t\t<specular><color>1 1 1 1</color></specular>');
      effectsXml.push('\t\t\t\t\t</phong>');
      effectsXml.push('\t\t\t\t</technique>');
      effectsXml.push('\t\t\t</profile_COMMON>');
      effectsXml.push('\t\t</effect>');
    }

    xml.push('\t<library_materials>');
    xml.push(...materialsXml);
    xml.push('\t</library_materials>');

    xml.push('\t<library_effects>');
    xml.push(...effectsXml);
    xml.push('\t</library_effects>');

    // Pre-compute joint names and inv bind poses
    let jointNames = '';
    let invBindPoses = '';
    for (let index = 0; index < mdl.skeleton.length; index++) {
      let transform = createMatrix();
      transform = DAE.transformSkeleton(mdl.skeleton, index, transform);
      jointNames += mdl.skeleton[index].name ?? '';
      invBindPoses += DAE.matrixToString(invertMatrix(transform));
      if (index < mdl.skeleton.length - 1) {
        jointNames += ' ';
        invBindPoses += ' ';
      }
    }

    // library_geometries + library_controllers
    const geometriesXml: string[] = [];
    const controllersXml: string[] = [];
    const vsNodes: string[] = [];
    let meshIndex = 0;

    for (const obj of mdl.mesh) {
      const meshMaterial = mdl.material[obj.materialId];
      if (!meshMaterial) continue; // skip meshes with missing material
      const meshName = 'mesh_' + meshIndex++ + '_' + obj.name;

      const mesh = MeshUtils.optimizeMesh<OVertex>(obj);
      const positions: number[] = [];
      const normals: number[] = [];
      const uv0: number[] = [];
      const uv1: number[] = [];
      const uv2: number[] = [];
      const colors: number[] = [];

      for (const vtx of mesh.vertices) {
        positions.push(vtx.position.x, vtx.position.y, vtx.position.z);
        if (mesh.hasNormal) normals.push(vtx.normal.x, vtx.normal.y, vtx.normal.z);
        if (mesh.texUVCount > 0) {
          const t = DAE.applyTextureCoordinator(vtx.texture0, meshMaterial.textureCoordinator[0]);
          uv0.push(t.x, t.y);
        }
        if (mesh.texUVCount > 1) {
          const t = DAE.applyTextureCoordinator(vtx.texture1, meshMaterial.textureCoordinator[1]);
          uv1.push(t.x, t.y);
        }
        if (mesh.texUVCount > 2) {
          const t = DAE.applyTextureCoordinator(vtx.texture2, meshMaterial.textureCoordinator[2]);
          uv2.push(t.x, t.y);
        }
        if (mesh.hasColor) {
          colors.push(
            ((vtx.diffuseColor >> 16) & 0xff) / 255,
            ((vtx.diffuseColor >> 8) & 0xff) / 255,
            (vtx.diffuseColor & 0xff) / 255,
            ((vtx.diffuseColor >> 24) & 0xff) / 255,
          );
        }
      }

      // Build geometry XML
      geometriesXml.push(`\t\t<geometry id="${meshName}_id" name="${meshName}">`);
      geometriesXml.push('\t\t\t<mesh>');

      // position source
      const posId = meshName + '_position_id';
      const posArrId = meshName + '_position_array_id';
      geometriesXml.push(`\t\t\t\t<source id="${posId}" name="${meshName}_position">`);
      geometriesXml.push(`\t\t\t\t\t<float_array id="${posArrId}" count="${positions.length}">${floatArrayStr(positions)}</float_array>`);
      geometriesXml.push('\t\t\t\t\t<technique_common>');
      geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${posArrId}" count="${mesh.vertices.length}" stride="3">`);
      geometriesXml.push('\t\t\t\t\t\t\t<param name="X" type="float"/>');
      geometriesXml.push('\t\t\t\t\t\t\t<param name="Y" type="float"/>');
      geometriesXml.push('\t\t\t\t\t\t\t<param name="Z" type="float"/>');
      geometriesXml.push('\t\t\t\t\t\t</accessor>');
      geometriesXml.push('\t\t\t\t\t</technique_common>');
      geometriesXml.push('\t\t\t\t</source>');

      // normal source
      let normalId = '';
      if (mesh.hasNormal) {
        normalId = meshName + '_normal_id';
        const normArrId = meshName + '_normal_array_id';
        geometriesXml.push(`\t\t\t\t<source id="${normalId}" name="${meshName}_normal">`);
        geometriesXml.push(`\t\t\t\t\t<float_array id="${normArrId}" count="${normals.length}">${floatArrayStr(normals)}</float_array>`);
        geometriesXml.push('\t\t\t\t\t<technique_common>');
        geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${normArrId}" count="${mesh.vertices.length}" stride="3">`);
        geometriesXml.push('\t\t\t\t\t\t\t<param name="X" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="Y" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="Z" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t</accessor>');
        geometriesXml.push('\t\t\t\t\t</technique_common>');
        geometriesXml.push('\t\t\t\t</source>');
      }

      // uv sources
      const uvIds: string[] = [];
      const uvArrays = [uv0, uv1, uv2];
      for (let i = 0; i < mesh.texUVCount; i++) {
        const uvId = meshName + '_uv' + i + '_id';
        uvIds.push(uvId);
        const uvArrId = meshName + '_uv' + i + '_array_id';
        geometriesXml.push(`\t\t\t\t<source id="${uvId}" name="${meshName}_uv${i}">`);
        geometriesXml.push(`\t\t\t\t\t<float_array id="${uvArrId}" count="${uvArrays[i].length}">${floatArrayStr(uvArrays[i])}</float_array>`);
        geometriesXml.push('\t\t\t\t\t<technique_common>');
        geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${uvArrId}" count="${mesh.vertices.length}" stride="2">`);
        geometriesXml.push('\t\t\t\t\t\t\t<param name="S" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="T" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t</accessor>');
        geometriesXml.push('\t\t\t\t\t</technique_common>');
        geometriesXml.push('\t\t\t\t</source>');
      }

      // color source
      let colorId = '';
      if (mesh.hasColor) {
        colorId = meshName + '_color_id';
        const colArrId = meshName + '_color_array_id';
        geometriesXml.push(`\t\t\t\t<source id="${colorId}" name="${meshName}_color">`);
        geometriesXml.push(`\t\t\t\t\t<float_array id="${colArrId}" count="${colors.length}">${floatArrayStr(colors)}</float_array>`);
        geometriesXml.push('\t\t\t\t\t<technique_common>');
        geometriesXml.push(`\t\t\t\t\t\t<accessor source="#${colArrId}" count="${mesh.vertices.length}" stride="4">`);
        geometriesXml.push('\t\t\t\t\t\t\t<param name="R" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="G" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="B" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t\t<param name="A" type="float"/>');
        geometriesXml.push('\t\t\t\t\t\t</accessor>');
        geometriesXml.push('\t\t\t\t\t</technique_common>');
        geometriesXml.push('\t\t\t\t</source>');
      }

      // vertices
      const verticesId = meshName + '_vertices_id';
      geometriesXml.push(`\t\t\t\t<vertices id="${verticesId}">`);
      geometriesXml.push(`\t\t\t\t\t<input semantic="POSITION" source="#${posId}"/>`);
      geometriesXml.push('\t\t\t\t</vertices>');

      // triangles
      geometriesXml.push(`\t\t\t\t<triangles material="${xmlEscape(meshMaterial.name)}" count="${Math.floor(mesh.indices.length / 3)}">`);
      geometriesXml.push(`\t\t\t\t\t<input semantic="VERTEX" source="#${verticesId}"/>`);
      if (mesh.hasNormal) geometriesXml.push(`\t\t\t\t\t<input semantic="NORMAL" source="#${normalId}"/>`);
      if (mesh.hasColor) geometriesXml.push(`\t\t\t\t\t<input semantic="COLOR" source="#${colorId}"/>`);
      for (let i = 0; i < mesh.texUVCount; i++) {
        const setAttr = i > 0 ? ` set="${i}"` : '';
        geometriesXml.push(`\t\t\t\t\t<input semantic="TEXCOORD" source="#${uvIds[i]}"${setAttr}/>`);
      }
      geometriesXml.push(`\t\t\t\t\t<p>${mesh.indices.join(' ')}</p>`);
      geometriesXml.push('\t\t\t\t</triangles>');
      geometriesXml.push('\t\t\t</mesh>');
      geometriesXml.push('\t\t</geometry>');

      // Controller (skinning)
      const hasNode = obj.vertices.length > 0 && obj.vertices[0].node.length > 0;
      const hasWeight = obj.vertices.length > 0 && obj.vertices[0].weight.length > 0;
      const hasController = hasNode && hasWeight && mdl.skeleton.length > 0;
      let controllerId = '';

      if (hasController) {
        controllerId = meshName + '_ctrl_id';

        const wArr: string[] = [];
        const vcountArr: string[] = [];
        const vArr: string[] = [];
        const wLookBack = new Float64Array(32);
        let wLookBackIndex = 0;
        let buffLen = 0;
        let wIndex = 0;
        let wCount = 0;

        for (const vtx of mesh.vertices) {
          const count = Math.min(vtx.node.length, vtx.weight.length);
          vcountArr.push(count.toString());
          for (let n = 0; n < count; n++) {
            vArr.push(vtx.node[n].toString());
            let found = false;
            let bPos = (wLookBackIndex - 1) & 0x1f;
            for (let i = 0; i < buffLen; i++) {
              if (wLookBack[bPos] === vtx.weight[n]) {
                vArr.push((wIndex - (i + 1)).toString());
                found = true;
                break;
              }
              bPos = (bPos - 1) & 0x1f;
            }
            if (!found) {
              vArr.push((wIndex++).toString());
              wArr.push(fstr(vtx.weight[n]));
              wCount++;
              wLookBack[wLookBackIndex] = vtx.weight[n];
              wLookBackIndex = (wLookBackIndex + 1) & 0x1f;
              if (buffLen < 32) buffLen++;
            }
          }
        }

        const jointsId = meshName + '_ctrl_joint_names_id';
        const jointsArrId = meshName + '_ctrl_joint_names_array_id';
        const bindPosesId = meshName + '_ctrl_inv_bind_poses_id';
        const bindPosesArrId = meshName + '_ctrl_inv_bind_poses_array_id';
        const weightsId = meshName + '_ctrl_weights_id';
        const weightsArrId = meshName + '_ctrl_weights_array_id';

        controllersXml.push(`\t\t<controller id="${controllerId}">`);
        controllersXml.push(`\t\t\t<skin source="#${meshName}_id">`);
        controllersXml.push(`\t\t\t\t<bind_shape_matrix>${DAE.matrixToString(createMatrix())}</bind_shape_matrix>`);

        // joint names source
        controllersXml.push(`\t\t\t\t<source id="${jointsId}">`);
        controllersXml.push(`\t\t\t\t\t<Name_array id="${jointsArrId}" count="${mdl.skeleton.length}">${jointNames}</Name_array>`);
        controllersXml.push('\t\t\t\t\t<technique_common>');
        controllersXml.push(`\t\t\t\t\t\t<accessor source="#${jointsArrId}" count="${mdl.skeleton.length}" stride="1">`);
        controllersXml.push('\t\t\t\t\t\t\t<param name="JOINT" type="Name"/>');
        controllersXml.push('\t\t\t\t\t\t</accessor>');
        controllersXml.push('\t\t\t\t\t</technique_common>');
        controllersXml.push('\t\t\t\t</source>');

        // inv bind poses source
        controllersXml.push(`\t\t\t\t<source id="${bindPosesId}">`);
        controllersXml.push(`\t\t\t\t\t<float_array id="${bindPosesArrId}" count="${mdl.skeleton.length * 16}">${invBindPoses}</float_array>`);
        controllersXml.push('\t\t\t\t\t<technique_common>');
        controllersXml.push(`\t\t\t\t\t\t<accessor source="#${bindPosesArrId}" count="${mdl.skeleton.length}" stride="16">`);
        controllersXml.push('\t\t\t\t\t\t\t<param name="TRANSFORM" type="float4x4"/>');
        controllersXml.push('\t\t\t\t\t\t</accessor>');
        controllersXml.push('\t\t\t\t\t</technique_common>');
        controllersXml.push('\t\t\t\t</source>');

        // weights source
        controllersXml.push(`\t\t\t\t<source id="${weightsId}">`);
        controllersXml.push(`\t\t\t\t\t<float_array id="${weightsArrId}" count="${wCount}">${wArr.join(' ')}</float_array>`);
        controllersXml.push('\t\t\t\t\t<technique_common>');
        controllersXml.push(`\t\t\t\t\t\t<accessor source="#${weightsArrId}" count="${wCount}" stride="1">`);
        controllersXml.push('\t\t\t\t\t\t\t<param name="WEIGHT" type="float"/>');
        controllersXml.push('\t\t\t\t\t\t</accessor>');
        controllersXml.push('\t\t\t\t\t</technique_common>');
        controllersXml.push('\t\t\t\t</source>');

        // joints
        controllersXml.push('\t\t\t\t<joints>');
        controllersXml.push(`\t\t\t\t\t<input semantic="JOINT" source="#${jointsId}"/>`);
        controllersXml.push(`\t\t\t\t\t<input semantic="INV_BIND_MATRIX" source="#${bindPosesId}"/>`);
        controllersXml.push('\t\t\t\t</joints>');

        // vertex_weights
        controllersXml.push(`\t\t\t\t<vertex_weights count="${mesh.vertices.length}">`);
        controllersXml.push(`\t\t\t\t\t<input semantic="JOINT" source="#${jointsId}" offset="0"/>`);
        controllersXml.push(`\t\t\t\t\t<input semantic="WEIGHT" source="#${weightsId}" offset="1"/>`);
        controllersXml.push(`\t\t\t\t\t<vcount>${vcountArr.join(' ')}</vcount>`);
        controllersXml.push(`\t\t\t\t\t<v>${vArr.join(' ')}</v>`);
        controllersXml.push('\t\t\t\t</vertex_weights>');
        controllersXml.push('\t\t\t</skin>');
        controllersXml.push('\t\t</controller>');
      }

      // Visual scene node for this mesh
      const vsnName = 'vsn_' + meshName;
      vsNodes.push(`\t\t\t<node id="${vsnName}_id" name="${vsnName}" type="NODE">`);
      vsNodes.push(`\t\t\t\t<matrix>${DAE.matrixToString(createMatrix())}</matrix>`);

      const bindMatXml = (matName: string, uvCount: number): string[] => {
        const bm: string[] = [];
        bm.push('\t\t\t\t\t<bind_material>');
        bm.push('\t\t\t\t\t\t<technique_common>');
        let imLine = `\t\t\t\t\t\t\t<instance_material symbol="${xmlEscape(matName)}" target="#${xmlEscape(matName)}_mat_id"`;
        if (uvCount > 0) {
          imLine += '>';
          bm.push(imLine);
          bm.push('\t\t\t\t\t\t\t\t<bind_vertex_input semantic="uv" input_semantic="TEXCOORD" input_set="0"/>');
          bm.push('\t\t\t\t\t\t\t</instance_material>');
        } else {
          bm.push(imLine + '/>');
        }
        bm.push('\t\t\t\t\t\t</technique_common>');
        bm.push('\t\t\t\t\t</bind_material>');
        return bm;
      };

      if (hasController) {
        vsNodes.push(`\t\t\t\t<instance_controller url="#${controllerId}">`);
        vsNodes.push(`\t\t\t\t\t<skeleton>#${xmlEscape(mdl.skeleton[0].name ?? '')}_bone_id</skeleton>`);
        vsNodes.push(...bindMatXml(meshMaterial.name, mesh.texUVCount));
        vsNodes.push('\t\t\t\t</instance_controller>');
      } else {
        vsNodes.push(`\t\t\t\t<instance_geometry url="#${meshName}_id">`);
        vsNodes.push(...bindMatXml(meshMaterial.name, mesh.texUVCount));
        vsNodes.push('\t\t\t\t</instance_geometry>');
      }

      vsNodes.push('\t\t\t</node>');
    }

    xml.push('\t<library_geometries>');
    xml.push(...geometriesXml);
    xml.push('\t</library_geometries>');

    if (controllersXml.length > 0) {
      xml.push('\t<library_controllers>');
      xml.push(...controllersXml);
      xml.push('\t</library_controllers>');
    }

    // NOTE: No library_animations emitted -- model-only export

    // library_visual_scenes
    const vsName = 'vs_' + mdl.name;
    const vsId = vsName + '_id';
    xml.push('\t<library_visual_scenes>');
    xml.push(`\t\t<visual_scene id="${vsId}" name="${vsName}">`);
    if (mdl.skeleton.length > 0) {
      DAE.writeSkeletonXml(mdl.skeleton, 0, xml, '\t\t\t');
    }
    xml.push(...vsNodes);
    xml.push('\t\t</visual_scene>');
    xml.push('\t</library_visual_scenes>');

    // scene
    xml.push('\t<scene>');
    xml.push(`\t\t<instance_visual_scene url="#${vsId}"/>`);
    xml.push('\t</scene>');
    xml.push('</COLLADA>');

    fs.writeFileSync(fileName, xml.join('\n'), 'utf-8');
  }

  /**
   * Exports a clip-only DAE containing just library_animations.
   * No geometry, controllers, images, or materials -- only animation channels
   * targeting the same bone IDs/names (`<BoneName>_bone_id/transform`) as the
   * model file so clips bind correctly when imported separately.
   */
  static exportClipOnly(
    model: OModelGroup,
    fileName: string,
    modelIndex: number,
    animationIndex: number,
  ): void {
    const mdl = model.model[modelIndex];
    const now = new Date().toISOString().replace(/\.\d{3}/, '');

    const xml: string[] = [];
    xml.push('<?xml version="1.0" encoding="utf-8"?>');
    xml.push('<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">');

    // asset
    xml.push('\t<asset>');
    xml.push(`\t\t<created>${now}</created>`);
    xml.push(`\t\t<modified>${now}</modified>`);
    xml.push('\t\t<up_axis>Y_UP</up_axis>');
    xml.push('\t</asset>');

    // library_animations only
    const animationsXml: string[] = [];
    DAE.exportAnimation(animationsXml, model, mdl, animationIndex);

    if (animationsXml.length > 0) {
      xml.push('\t<library_animations>');
      xml.push(...animationsXml);
      xml.push('\t</library_animations>');
    }

    xml.push('</COLLADA>');

    fs.writeFileSync(fileName, xml.join('\n'), 'utf-8');
  }

  // -----------------------------------------------------------------------
  // Animation export
  // -----------------------------------------------------------------------
  private static exportAnimation(
    xmlOut: string[],
    model: OModelGroup,
    mdl: OModel,
    animIndex: number,
    animationPrefix: string = '',
  ): void {
    if (!model.skeletalAnimation) return;
    if (animIndex < 0 || animIndex >= model.skeletalAnimation.list.length) return;
    const anim = model.skeletalAnimation.list[animIndex] as OSkeletalAnimation;
    if (!anim) return;

    const skeletonByName = new Map<string, OBone>();
    for (const bone of mdl.skeleton) {
      if (bone.name && !skeletonByName.has(bone.name)) skeletonByName.set(bone.name, bone);
    }

    for (let boneIndex = 0; boneIndex < anim.bone.length; boneIndex++) {
      const animBone = anim.bone[boneIndex];
      if (!animBone.name || !animBone.name.trim() || !skeletonByName.has(animBone.name)) {
        DAE.logDiagnostic('Skipping animation bone \'' + (animBone.name || '') + '\' at index ' + boneIndex + ' (not in skeleton).');
        continue;
      }

      if (!DAE.hasAnyAnimationData(animBone)) continue;

      const sampleFrames = DAE.collectSampleFrames(anim, animBone);
      if (sampleFrames.length === 0) continue;

      const skeletonBone = skeletonByName.get(animBone.name) ?? null;
      const defaultRX = skeletonBone ? skeletonBone.rotation.x : 0;
      const defaultRY = skeletonBone ? skeletonBone.rotation.y : 0;
      const defaultRZ = skeletonBone ? skeletonBone.rotation.z : 0;
      const defaultTX = skeletonBone ? skeletonBone.translation.x : 0;
      const defaultTY = skeletonBone ? skeletonBone.translation.y : 0;
      const defaultTZ = skeletonBone ? skeletonBone.translation.z : 0;

      const baseName = animationPrefix + 'anim_' + animBone.name + '_transform';

      const outputMatrices: number[] = [];
      for (let si = 0; si < sampleFrames.length; si++) {
        const frame = sampleFrames[si];
        let localTransform: OMatrix;

        if (animBone.isFullBakedFormat && animBone.transform.length > 0) {
          let idx = (Math.floor(frame)) % animBone.transform.length;
          if (idx < 0) idx += animBone.transform.length;
          localTransform = animBone.transform[idx];
        } else if (animBone.isFrameFormat) {
          const scale = DAE.sampleFrameVector(animBone.scale, frame, new OVector4(1, 1, 1, 0));
          const translation = DAE.sampleFrameVector(animBone.translation, frame, new OVector4(defaultTX, defaultTY, defaultTZ, 0));

          if (animBone.rotationQuaternion.exists) {
            const rq = DAE.sampleFrameVector(animBone.rotationQuaternion, frame, new OVector4(0, 0, 0, 1));
            localTransform = DAE.buildLocalMatrixFromQuaternion(scale.x, scale.y, scale.z, rq.x, rq.y, rq.z, rq.w, translation.x, translation.y, translation.z);
          } else {
            localTransform = DAE.buildLocalMatrix(scale.x, scale.y, scale.z, defaultRX, defaultRY, defaultRZ, translation.x, translation.y, translation.z, false);
          }
        } else {
          const sx = DAE.sampleKeyframes(animBone.scaleX, frame, 1);
          const sy = DAE.sampleKeyframes(animBone.scaleY, frame, 1);
          const sz = DAE.sampleKeyframes(animBone.scaleZ, frame, 1);
          const rx = DAE.sampleKeyframes(animBone.rotationX, frame, defaultRX);
          const ry = DAE.sampleKeyframes(animBone.rotationY, frame, defaultRY);
          const rz = DAE.sampleKeyframes(animBone.rotationZ, frame, defaultRZ);
          const tx = DAE.sampleKeyframes(animBone.translationX, frame, defaultTX);
          const ty = DAE.sampleKeyframes(animBone.translationY, frame, defaultTY);
          const tz = DAE.sampleKeyframes(animBone.translationZ, frame, defaultTZ);
          localTransform = DAE.buildLocalMatrix(sx, sy, sz, rx, ry, rz, tx, ty, tz, animBone.isAxisAngle);
        }

        DAE.appendMatrix(outputMatrices, localTransform);
      }

      const sampleTimes = sampleFrames.map(f => f / AnimationFramesPerSecond);
      const interpData = sampleTimes.map(() => 'LINEAR');

      const inputId = baseName + '_input';
      const outputId = baseName + '_output';
      const interpId = baseName + '_interpolation';
      const samplerId = baseName + '_sampler';

      xmlOut.push(`\t\t<animation id="${baseName}" name="${baseName}">`);
      // input source (time)
      xmlOut.push(`\t\t\t<source id="${inputId}">`);
      xmlOut.push(`\t\t\t\t<float_array id="${inputId}_array" count="${sampleTimes.length}">${floatArrayStr(sampleTimes)}</float_array>`);
      xmlOut.push('\t\t\t\t<technique_common>');
      xmlOut.push(`\t\t\t\t\t<accessor source="#${inputId}_array" count="${sampleTimes.length}" stride="1">`);
      xmlOut.push('\t\t\t\t\t\t<param name="TIME" type="float"/>');
      xmlOut.push('\t\t\t\t\t</accessor>');
      xmlOut.push('\t\t\t\t</technique_common>');
      xmlOut.push('\t\t\t</source>');

      // output source (matrices)
      xmlOut.push(`\t\t\t<source id="${outputId}">`);
      xmlOut.push(`\t\t\t\t<float_array id="${outputId}_array" count="${outputMatrices.length}">${floatArrayStr(outputMatrices)}</float_array>`);
      xmlOut.push('\t\t\t\t<technique_common>');
      xmlOut.push(`\t\t\t\t\t<accessor source="#${outputId}_array" count="${sampleFrames.length}" stride="16">`);
      xmlOut.push('\t\t\t\t\t\t<param name="TRANSFORM" type="float4x4"/>');
      xmlOut.push('\t\t\t\t\t</accessor>');
      xmlOut.push('\t\t\t\t</technique_common>');
      xmlOut.push('\t\t\t</source>');

      // interpolation source
      xmlOut.push(`\t\t\t<source id="${interpId}">`);
      xmlOut.push(`\t\t\t\t<Name_array id="${interpId}_array" count="${interpData.length}">${interpData.join(' ')}</Name_array>`);
      xmlOut.push('\t\t\t\t<technique_common>');
      xmlOut.push(`\t\t\t\t\t<accessor source="#${interpId}_array" count="${interpData.length}" stride="1">`);
      xmlOut.push('\t\t\t\t\t\t<param name="INTERPOLATION" type="Name"/>');
      xmlOut.push('\t\t\t\t\t</accessor>');
      xmlOut.push('\t\t\t\t</technique_common>');
      xmlOut.push('\t\t\t</source>');

      // sampler
      xmlOut.push(`\t\t\t<sampler id="${samplerId}">`);
      xmlOut.push(`\t\t\t\t<input semantic="INPUT" source="#${inputId}"/>`);
      xmlOut.push(`\t\t\t\t<input semantic="OUTPUT" source="#${outputId}"/>`);
      xmlOut.push(`\t\t\t\t<input semantic="INTERPOLATION" source="#${interpId}"/>`);
      xmlOut.push('\t\t\t</sampler>');

      // channel
      xmlOut.push(`\t\t\t<channel source="#${samplerId}" target="${xmlEscape(animBone.name)}_bone_id/transform"/>`);
      xmlOut.push('\t\t</animation>');
    }
  }

  // -----------------------------------------------------------------------
  // Skeleton helpers
  // -----------------------------------------------------------------------
  private static transformSkeleton(skeleton: OBone[], index: number, target: OMatrix): OMatrix {
    let result = mulMatrix(target, rotateXMatrix(skeleton[index].rotation.x));
    result = mulMatrix(result, rotateYMatrix(skeleton[index].rotation.y));
    result = mulMatrix(result, rotateZMatrix(skeleton[index].rotation.z));
    result = mulMatrix(result, translateMatrix(skeleton[index].translation));
    if (skeleton[index].parentId > -1) {
      result = DAE.transformSkeleton(skeleton, skeleton[index].parentId, result);
    }
    return result;
  }

  private static writeSkeletonXml(skeleton: OBone[], index: number, xml: string[], indent: string): void {
    const bone = skeleton[index];
    const boneName = bone.name ?? '';
    const nodeId = boneName + '_bone_id';

    let transform = createMatrix();
    transform = mulMatrix(transform, rotateXMatrix(bone.rotation.x));
    transform = mulMatrix(transform, rotateYMatrix(bone.rotation.y));
    transform = mulMatrix(transform, rotateZMatrix(bone.rotation.z));
    transform = mulMatrix(transform, translateMatrix(bone.translation));

    xml.push(`${indent}<node id="${xmlEscape(nodeId)}" name="${xmlEscape(boneName)}" sid="${xmlEscape(boneName)}" type="JOINT">`);
    xml.push(`${indent}\t<matrix sid="transform">${DAE.matrixToString(transform)}</matrix>`);

    for (let i = 0; i < skeleton.length; i++) {
      if (skeleton[i].parentId === index) {
        DAE.writeSkeletonXml(skeleton, i, xml, indent + '\t');
      }
    }

    xml.push(`${indent}</node>`);
  }

  // -----------------------------------------------------------------------
  // Matrix serialization -- column-major order matching C# [j,i] indexing
  // -----------------------------------------------------------------------
  private static matrixToString(mtx: OMatrix): string {
    const parts: string[] = [];
    for (let i = 0; i < 4; i++) {
      for (let j = 0; j < 4; j++) {
        parts.push(fstr(mtx.get(j, i)));
      }
    }
    return parts.join(' ');
  }

  private static appendMatrix(dest: number[], mtx: OMatrix): void {
    for (let i = 0; i < 4; i++) {
      for (let j = 0; j < 4; j++) {
        dest.push(mtx.get(j, i));
      }
    }
  }

  // -----------------------------------------------------------------------
  // Texture coordinator
  // -----------------------------------------------------------------------
  private static applyTextureCoordinator(uv: OVector2, coordinator: OTextureCoordinator): OVector2 {
    let scaleU = coordinator.scaleU;
    let scaleV = coordinator.scaleV;
    if (scaleU === 0 && scaleV === 0) { scaleU = 1; scaleV = 1; }

    const centeredU = uv.x - 0.5;
    const centeredV = uv.y - 0.5;
    const cos = Math.cos(coordinator.rotate);
    const sin = Math.sin(coordinator.rotate);
    const rotatedU = (centeredU * cos) - (centeredV * sin);
    const rotatedV = (centeredU * sin) + (centeredV * cos);
    const transformedU = (rotatedU + 0.5) * scaleU - coordinator.translateU;
    const transformedV = (rotatedV + 0.5) * scaleV - coordinator.translateV;
    return new OVector2(transformedU, transformedV);
  }

  // -----------------------------------------------------------------------
  // Animation sampling helpers
  // -----------------------------------------------------------------------
  private static hasAnyAnimationData(bone: OSkeletalAnimationBone): boolean {
    return bone.isFullBakedFormat
      || bone.isFrameFormat
      || DAE.hasKeyFrames(bone.scaleX) || DAE.hasKeyFrames(bone.scaleY) || DAE.hasKeyFrames(bone.scaleZ)
      || DAE.hasKeyFrames(bone.rotationX) || DAE.hasKeyFrames(bone.rotationY) || DAE.hasKeyFrames(bone.rotationZ)
      || DAE.hasKeyFrames(bone.translationX) || DAE.hasKeyFrames(bone.translationY) || DAE.hasKeyFrames(bone.translationZ);
  }

  private static hasKeyFrames(group: OAnimationKeyFrameGroup): boolean {
    return group.exists && group.keyFrames.length > 0;
  }

  private static collectSampleFrames(animation: OSkeletalAnimation, bone: OSkeletalAnimationBone): number[] {
    const frames: number[] = [];

    const addGroup = (g: OAnimationKeyFrameGroup) => {
      if (!g.exists || g.keyFrames.length === 0) return;
      for (const kf of g.keyFrames) frames.push(kf.frame);
    };

    addGroup(bone.scaleX); addGroup(bone.scaleY); addGroup(bone.scaleZ);
    addGroup(bone.rotationX); addGroup(bone.rotationY); addGroup(bone.rotationZ);
    addGroup(bone.translationX); addGroup(bone.translationY); addGroup(bone.translationZ);

    if (bone.isFrameFormat) {
      DAE.addFrameRange(frames, bone.scale, animation.frameSize);
      DAE.addFrameRange(frames, bone.rotationQuaternion, animation.frameSize);
      DAE.addFrameRange(frames, bone.translation, animation.frameSize);
    }

    if (bone.isFullBakedFormat && bone.transform.length > 0) {
      for (let i = 0; i < bone.transform.length; i++) frames.push(i);
    }

    if (frames.length === 0) return frames;

    frames.sort((a, b) => a - b);
    const unique: number[] = [frames[0]];
    let last = frames[0];
    for (let i = 1; i < frames.length; i++) {
      if (Math.abs(frames[i] - last) < 0.0001) continue;
      unique.push(frames[i]);
      last = frames[i];
    }

    if (bone.isFullBakedFormat) return unique;

    const denseStart = Math.max(0, Math.floor(unique[0]));
    const denseEnd = Math.max(denseStart, Math.ceil(Math.max(animation.frameSize, unique[unique.length - 1])));

    const dense: number[] = [];
    for (let f = denseStart; f <= denseEnd; f++) dense.push(f);

    for (const kf of unique) {
      if (!dense.some(d => Math.abs(d - kf) < 0.0001)) {
        dense.push(kf);
      }
    }

    dense.sort((a, b) => a - b);
    return dense;
  }

  private static addFrameRange(frames: number[], frame: OAnimationFrame, animFrameSize: number): void {
    if (!frame.exists || frame.vector.length === 0) return;
    if (frame.vector.length === 1) { frames.push(frame.startFrame); return; }
    let start = frame.startFrame;
    let end = frame.endFrame;
    if (end <= start) end = Math.max(start + frame.vector.length - 1, animFrameSize);
    const step = (end - start) / (frame.vector.length - 1);
    for (let i = 0; i < frame.vector.length; i++) frames.push(start + step * i);
  }

  private static sampleKeyframes(group: OAnimationKeyFrameGroup, frame: number, defaultValue: number): number {
    if (!group.exists || group.keyFrames.length === 0) return defaultValue;
    if (group.keyFrames.length === 1) return group.keyFrames[0].value;
    const first = group.keyFrames[0];
    const last = group.keyFrames[group.keyFrames.length - 1];
    if (frame <= first.frame) return first.value;
    if (frame >= last.frame) return last.value;
    for (let i = 0; i < group.keyFrames.length - 1; i++) {
      const left = group.keyFrames[i];
      const right = group.keyFrames[i + 1];
      if (frame > right.frame) continue;
      const delta = right.frame - left.frame;
      if (Math.abs(delta) < 0.0001) return right.value;
      const mu = (frame - left.frame) / delta;
      return left.value + (right.value - left.value) * mu;
    }
    return last.value;
  }

  private static sampleFrameVector(frameData: OAnimationFrame, frame: number, defaultValue: OVector4): OVector4 {
    if (!frameData.exists || frameData.vector.length === 0) return defaultValue;
    if (frameData.vector.length === 1) return frameData.vector[0];
    let mappedFrame = frame;
    if (frameData.endFrame > frameData.startFrame) {
      const normalized = (frame - frameData.startFrame) / (frameData.endFrame - frameData.startFrame);
      mappedFrame = normalized * (frameData.vector.length - 1);
    }
    mappedFrame = Math.max(0, Math.min(mappedFrame, frameData.vector.length - 1));
    const left = Math.floor(mappedFrame);
    const right = Math.min(left + 1, frameData.vector.length - 1);
    const mu = mappedFrame - left;
    const lv = frameData.vector[left];
    const rv = frameData.vector[right];
    return new OVector4(
      lv.x + (rv.x - lv.x) * mu,
      lv.y + (rv.y - lv.y) * mu,
      lv.z + (rv.z - lv.z) * mu,
      lv.w + (rv.w - lv.w) * mu,
    );
  }

  // -----------------------------------------------------------------------
  // Matrix builders
  // -----------------------------------------------------------------------
  private static buildLocalMatrix(
    sx: number, sy: number, sz: number,
    rx: number, ry: number, rz: number,
    tx: number, ty: number, tz: number,
    isAxisAngle: boolean,
  ): OMatrix {
    let output = createMatrix();
    output = mulMatrix(output, scaleMatrix(new OVector3(sx, sy, sz)));
    if (isAxisAngle) {
      output = mulMatrix(output, DAE.buildAxisAngleMatrix(rx, ry, rz));
    } else {
      output = mulMatrix(output, rotateZMatrix(rz));
      output = mulMatrix(output, rotateYMatrix(ry));
      output = mulMatrix(output, rotateXMatrix(rx));
    }
    output = mulMatrix(output, translateMatrix(new OVector3(tx, ty, tz)));
    return output;
  }

  private static buildLocalMatrixFromQuaternion(
    sx: number, sy: number, sz: number,
    qx: number, qy: number, qz: number, qw: number,
    tx: number, ty: number, tz: number,
  ): OMatrix {
    let output = createMatrix();
    output = mulMatrix(output, scaleMatrix(new OVector3(sx, sy, sz)));
    output = mulMatrix(output, DAE.buildQuaternionMatrix(qx, qy, qz, qw));
    output = mulMatrix(output, translateMatrix(new OVector3(tx, ty, tz)));
    return output;
  }

  private static buildAxisAngleMatrix(x: number, y: number, z: number): OMatrix {
    const angle = Math.sqrt(x * x + y * y + z * z);
    if (angle <= 0.000001) return createMatrix();
    const ax = x / angle, ay = y / angle, az = z / angle;
    // Quaternion from axis-angle
    const s = Math.sin(angle / 2);
    const qx = ax * s, qy = ay * s, qz = az * s, qw = Math.cos(angle / 2);
    return DAE.buildQuaternionMatrix(qx, qy, qz, qw);
  }

  private static buildQuaternionMatrix(x: number, y: number, z: number, w: number): OMatrix {
    let len2 = x * x + y * y + z * z + w * w;
    if (len2 <= 0.0000001) return createMatrix();
    // Normalize
    const invLen = 1 / Math.sqrt(len2);
    const qx = x * invLen, qy = y * invLen, qz = z * invLen, qw = w * invLen;
    // Build rotation matrix from quaternion
    const m = createMatrix();
    m.M11 = 1 - 2 * (qy * qy + qz * qz);
    m.M12 = 2 * (qx * qy + qz * qw);
    m.M13 = 2 * (qx * qz - qy * qw);
    m.M21 = 2 * (qx * qy - qz * qw);
    m.M22 = 1 - 2 * (qx * qx + qz * qz);
    m.M23 = 2 * (qy * qz + qx * qw);
    m.M31 = 2 * (qx * qz + qy * qw);
    m.M32 = 2 * (qy * qz - qx * qw);
    m.M33 = 1 - 2 * (qx * qx + qy * qy);
    return m;
  }

  private static logDiagnostic(message: string): void {
    if (!DAE.DiagnosticLogging) return;
    console.error('[DAE] ' + message);
  }
}
