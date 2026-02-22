import { access } from "node:fs/promises";
import sharp from "sharp";
import { ResolvedCharacter } from "./resolver";
import { ANIMATION_ROWS_LAYOUT, CUSTOM_ANIMATIONS } from "./customAnimations";
import { FRAME_SIZE } from "./constants";

interface DrawOperation {
  itemId: string;
  layerNum: number;
  zPos: number;
  selectionIndex: number;
  animation: string;
  yOffset: number;
  absolutePath: string;
}

interface CustomAreaDrawItem {
  type: "custom_sprite" | "extracted_frames";
  itemId: string;
  layerNum: number;
  zPos: number;
  selectionIndex: number;
  absolutePath: string;
}

interface CompositeOperation {
  input: string | Buffer;
  left: number;
  top: number;
}

export async function renderStandardSpritesheet(character: ResolvedCharacter, outputPath: string): Promise<void> {
  const standardDrawOps = flattenDrawOperations(character);
  const customDirectPaths = character.customLayers.map((layer) => layer.absolutePath);
  await validateSpriteFiles(standardDrawOps.map((op) => op.absolutePath).concat(customDirectPaths));

  const compositeOps = await buildCompositeOperations(character, standardDrawOps);

  await sharp({
    create: {
      width: character.dimensions.width,
      height: character.dimensions.height,
      channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 }
    }
  })
    .composite(compositeOps)
    .png()
    .toFile(outputPath);
}

async function buildCompositeOperations(
  character: ResolvedCharacter,
  standardDrawOps: DrawOperation[]
): Promise<CompositeOperation[]> {
  const operations: CompositeOperation[] = standardDrawOps.map((op) => ({
    input: op.absolutePath,
    top: op.yOffset,
    left: 0
  }));

  const standardByAnimation = new Map<string, DrawOperation[]>();
  for (const operation of standardDrawOps) {
    const list = standardByAnimation.get(operation.animation) ?? [];
    list.push(operation);
    standardByAnimation.set(operation.animation, list);
  }

  const extractCache = new Map<string, Buffer>();
  for (const customArea of character.customAnimations) {
    const customItems: CustomAreaDrawItem[] = [];

    for (const customLayer of character.customLayers) {
      if (customLayer.customAnimation !== customArea.name) {
        continue;
      }
      customItems.push({
        type: "custom_sprite",
        itemId: customLayer.itemId,
        layerNum: customLayer.layerNum,
        zPos: customLayer.zPos,
        selectionIndex: customLayer.selectionIndex,
        absolutePath: customLayer.absolutePath
      });
    }

    for (const standardOperation of standardByAnimation.get(customArea.baseAnimation) ?? []) {
      customItems.push({
        type: "extracted_frames",
        itemId: standardOperation.itemId,
        layerNum: standardOperation.layerNum,
        zPos: standardOperation.zPos,
        selectionIndex: standardOperation.selectionIndex,
        absolutePath: standardOperation.absolutePath
      });
    }

    customItems.sort((a, b) => {
      return (
        a.zPos - b.zPos ||
        a.selectionIndex - b.selectionIndex ||
        a.itemId.localeCompare(b.itemId) ||
        a.layerNum - b.layerNum ||
        a.type.localeCompare(b.type)
      );
    });

    for (const item of customItems) {
      if (item.type === "custom_sprite") {
        operations.push({
          input: item.absolutePath,
          left: 0,
          top: customArea.yOffset
        });
        continue;
      }

      const cacheKey = `${customArea.name}::${item.absolutePath}`;
      let extracted = extractCache.get(cacheKey);
      if (!extracted) {
        extracted = await extractFramesToCustomAnimation(customArea.name, item.absolutePath);
        extractCache.set(cacheKey, extracted);
      }

      operations.push({
        input: extracted,
        left: 0,
        top: customArea.yOffset
      });
    }
  }

  return operations;
}

function flattenDrawOperations(character: ResolvedCharacter): DrawOperation[] {
  const operations: DrawOperation[] = [];

  for (const layer of character.layers) {
    for (const animation of layer.animations) {
      operations.push({
        itemId: layer.itemId,
        layerNum: layer.layerNum,
        zPos: layer.zPos,
        selectionIndex: layer.selectionIndex,
        animation: animation.animation,
        yOffset: animation.yOffset,
        absolutePath: animation.absolutePath
      });
    }
  }

  operations.sort((a, b) => {
    return (
      a.zPos - b.zPos ||
      a.selectionIndex - b.selectionIndex ||
      a.itemId.localeCompare(b.itemId) ||
      a.layerNum - b.layerNum ||
      a.yOffset - b.yOffset ||
      a.animation.localeCompare(b.animation)
    );
  });

  return operations;
}

async function validateSpriteFiles(absolutePaths: string[]): Promise<void> {
  const checks = await Promise.all(
    absolutePaths.map(async (absolutePath) => {
      try {
        await access(absolutePath);
        return null;
      } catch {
        return absolutePath;
      }
    })
  );

  const missing = checks.filter((value): value is string => value !== null);
  if (missing.length > 0) {
    const uniqueMissing = Array.from(new Set(missing)).sort((a, b) => a.localeCompare(b));
    const listed = uniqueMissing.slice(0, 20).map((filePath) => `- ${filePath}`).join("\n");
    const suffix = uniqueMissing.length > 20 ? `\n- ...and ${uniqueMissing.length - 20} more` : "";
    throw new Error(`Missing sprite files:\n${listed}${suffix}`);
  }
}

async function extractFramesToCustomAnimation(customAnimationName: string, absolutePath: string): Promise<Buffer> {
  const definition = CUSTOM_ANIMATIONS[customAnimationName];
  if (!definition) {
    throw new Error(`Missing custom animation definition for '${customAnimationName}'.`);
  }

  const metadata = await sharp(absolutePath).metadata();
  const sourceHeight = metadata.height;
  if (!sourceHeight) {
    throw new Error(`Unable to read image dimensions for '${absolutePath}'.`);
  }

  const isSingleAnimationSource = sourceHeight <= FRAME_SIZE * 4;
  const directionRows: Record<string, number> = { n: 0, w: 1, s: 2, e: 3 };

  const columns = definition.frames.at(0)?.length ?? 0;
  const areaWidth = definition.frameSize * columns;
  const areaHeight = definition.frameSize * definition.frames.length;
  const frameInset = Math.floor((definition.frameSize - FRAME_SIZE) / 2);
  const frameCache = new Map<string, Buffer>();
  const composites: CompositeOperation[] = [];

  for (const [rowIndex, row] of definition.frames.entries()) {
    for (const [columnIndex, frameSpec] of row.entries()) {
      const [rowNameRaw, sourceColumnRaw] = frameSpec.split(",");
      const rowName = (rowNameRaw ?? "").trim();
      const sourceColumn = Number.parseInt((sourceColumnRaw ?? "").trim(), 10);
      if (!Number.isInteger(sourceColumn) || sourceColumn < 0) {
        throw new Error(`Invalid frame column '${sourceColumnRaw}' in custom animation '${customAnimationName}'.`);
      }

      let sourceRow: number;
      if (isSingleAnimationSource) {
        const direction = rowName.split("-").at(1) ?? "";
        const mapped = directionRows[direction];
        if (mapped === undefined) {
          throw new Error(
            `Unable to resolve direction '${direction}' from frame '${frameSpec}' in custom animation '${customAnimationName}'.`
          );
        }
        sourceRow = mapped;
      } else {
        const mapped = ANIMATION_ROWS_LAYOUT[rowName];
        if (mapped === undefined) {
          throw new Error(
            `Missing row mapping for '${rowName}' in custom animation '${customAnimationName}'.`
          );
        }
        sourceRow = mapped;
      }

      const sourceX = sourceColumn * FRAME_SIZE;
      const sourceY = sourceRow * FRAME_SIZE;
      const frameCacheKey = `${sourceX}:${sourceY}`;

      let frameBuffer = frameCache.get(frameCacheKey);
      if (!frameBuffer) {
        frameBuffer = await sharp(absolutePath)
          .extract({
            left: sourceX,
            top: sourceY,
            width: FRAME_SIZE,
            height: FRAME_SIZE
          })
          .png()
          .toBuffer();
        frameCache.set(frameCacheKey, frameBuffer);
      }

      composites.push({
        input: frameBuffer,
        left: columnIndex * definition.frameSize + frameInset,
        top: rowIndex * definition.frameSize + frameInset
      });
    }
  }

  return sharp({
    create: {
      width: areaWidth,
      height: areaHeight,
      channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 }
    }
  })
    .composite(composites)
    .png()
    .toBuffer();
}
