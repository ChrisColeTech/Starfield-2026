/**
 * Mesh utility functions.
 * Ported from OhanaCli.Formats.Models.MeshUtils (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import type { Color } from '../PICA200/PICACommandReader.js';

/** Minimal OModel-like interface for bounds calculation */
interface HasBounds {
  minVector: { x: number; y: number; z: number };
  maxVector: { x: number; y: number; z: number };
}

/** Minimal OVertex-like interface for bounds calculation */
interface HasPosition {
  position: { x: number; y: number; z: number };
}

/** Minimal OMesh-like interface for the optimizer */
interface MeshLike<V extends HasPosition = HasPosition> {
  vertices: V[];
  hasNormal: boolean;
  hasTangent: boolean;
  hasColor: boolean;
  hasNode: boolean;
  hasWeight: boolean;
  texUVCount: number;
}

export interface optimizedMesh<V extends HasPosition = HasPosition> {
  vertices: V[];
  indices: number[];
  hasNormal: boolean;
  hasTangent: boolean;
  hasColor: boolean;
  hasNode: boolean;
  hasWeight: boolean;
  texUVCount: number;
}

const optimizerLookBack = 32;

export class MeshUtils {
  /** Calculates the minimum and maximum vector values for a Model. */
  static calculateBounds(mdl: HasBounds, vertex: HasPosition): void {
    if (vertex.position.x < mdl.minVector.x) mdl.minVector.x = vertex.position.x;
    if (vertex.position.x > mdl.maxVector.x) mdl.maxVector.x = vertex.position.x;
    if (vertex.position.y < mdl.minVector.y) mdl.minVector.y = vertex.position.y;
    if (vertex.position.y > mdl.maxVector.y) mdl.maxVector.y = vertex.position.y;
    if (vertex.position.z < mdl.minVector.z) mdl.minVector.z = vertex.position.z;
    if (vertex.position.z > mdl.maxVector.z) mdl.maxVector.z = vertex.position.z;
  }

  /** Reads a Color (RGBA bytes) from the data. Returns { r, g, b, a }. */
  static getColor(input: BinaryReader): Color {
    const r = input.readByte();
    const g = input.readByte();
    const b = input.readByte();
    const a = input.readByte();
    return { r, g, b, a };
  }

  /** Reads a Color stored in float format from the data. Returns { r, g, b, a }. */
  static getColorFloat(input: BinaryReader): Color {
    const r = Math.min(255, Math.max(0, Math.floor(input.readFloat() * 0xff)));
    const g = Math.min(255, Math.max(0, Math.floor(input.readFloat() * 0xff)));
    const b = Math.min(255, Math.max(0, Math.floor(input.readFloat() * 0xff)));
    const a = Math.min(255, Math.max(0, Math.floor(input.readFloat() * 0xff)));
    return { r, g, b, a };
  }

  /** Clamps a float value between 0 and 255 and returns as integer. */
  static saturate(value: number): number {
    if (value > 0xff) return 0xff;
    if (value < 0) return 0;
    return Math.floor(value);
  }

  /**
   * Creates an index buffer for a mesh, trying to reuse vertices where possible.
   */
  static optimizeMesh<V extends HasPosition>(mesh: MeshLike<V>): optimizedMesh<V> {
    const output: optimizedMesh<V> = {
      vertices: [],
      indices: [],
      hasNormal: mesh.hasNormal,
      hasTangent: mesh.hasTangent,
      hasColor: mesh.hasColor,
      hasNode: mesh.hasNode,
      hasWeight: mesh.hasWeight,
      texUVCount: mesh.texUVCount,
    };

    for (let i = 0; i < mesh.vertices.length; i++) {
      let found = false;
      for (let j = 1; j <= optimizerLookBack; j++) {
        const p = output.vertices.length - j;
        if (p < 0 || p >= output.vertices.length) break;
        // Simple reference equality check; for deep equality a custom compare would be needed
        if (output.vertices[p] === mesh.vertices[i]) {
          output.indices.push(p);
          found = true;
          break;
        }
      }

      if (!found) {
        output.vertices.push(mesh.vertices[i]);
        output.indices.push(output.vertices.length - 1);
      }
    }

    return output;
  }

  /** Gets total optimized vertex count across all meshes. */
  static getOptimizedVertCount(om: MeshLike[]): number {
    let cnt = 0;
    for (const v of om) {
      cnt += MeshUtils.optimizeMesh(v).vertices.length;
    }
    return cnt;
  }
}
