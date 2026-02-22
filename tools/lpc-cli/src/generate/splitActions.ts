import { mkdir, rm } from "node:fs/promises";
import { resolve } from "node:path";
import sharp from "sharp";
import { FRAME_SIZE, SPRITESHEET_HEIGHT, SPRITESHEET_WIDTH, STANDARD_ANIMATIONS } from "./constants";
import { ResolvedCharacter } from "./resolver";

export interface ActionExportArea {
  name: string;
  left: number;
  top: number;
  width: number;
  height: number;
}

interface ExportSplitActionsOptions {
  spritesheetPath: string;
  outputDir: string;
  character: ResolvedCharacter;
}

export async function exportSplitActionSprites(options: ExportSplitActionsOptions): Promise<string[]> {
  const image = sharp(options.spritesheetPath, { failOn: "error" });
  const metadata = await image.metadata();
  const sourceWidth = metadata.width;
  const sourceHeight = metadata.height;

  if (!sourceWidth || !sourceHeight) {
    throw new Error(`Unable to read spritesheet dimensions from '${options.spritesheetPath}'.`);
  }

  if (sourceWidth < options.character.dimensions.width || sourceHeight < options.character.dimensions.height) {
    throw new Error(
      `Spritesheet dimensions ${sourceWidth}x${sourceHeight} do not match resolved dimensions ${options.character.dimensions.width}x${options.character.dimensions.height}.`
    );
  }

  const areas = buildActionExportAreas(options.character);
  validateActionAreaNames(areas);

  await rm(options.outputDir, { recursive: true, force: true });
  await mkdir(options.outputDir, { recursive: true });

  const generatedPaths: string[] = [];

  for (const area of areas) {
    if (area.left < 0 || area.top < 0 || area.width <= 0 || area.height <= 0) {
      throw new Error(
        `Invalid export bounds for action '${area.name}': left=${area.left}, top=${area.top}, width=${area.width}, height=${area.height}.`
      );
    }
    if (area.left + area.width > sourceWidth || area.top + area.height > sourceHeight) {
      throw new Error(
        `Action '${area.name}' bounds exceed spritesheet size ${sourceWidth}x${sourceHeight}: left=${area.left}, top=${area.top}, width=${area.width}, height=${area.height}.`
      );
    }

    const outputPath = resolve(options.outputDir, `${area.name}.png`);
    await sharp(options.spritesheetPath)
      .extract({ left: area.left, top: area.top, width: area.width, height: area.height })
      .png()
      .toFile(outputPath);
    generatedPaths.push(outputPath);
  }

  return generatedPaths;
}

export function buildActionExportAreas(character: Pick<ResolvedCharacter, "customAnimations">): ActionExportArea[] {
  const areas: ActionExportArea[] = [];

  for (const [index, animation] of STANDARD_ANIMATIONS.entries()) {
    const currentOffset = animation.yOffset;
    const nextOffset = STANDARD_ANIMATIONS[index + 1]?.yOffset ?? SPRITESHEET_HEIGHT;
    const segmentHeight = nextOffset - currentOffset;

    if (segmentHeight <= 0 || segmentHeight % FRAME_SIZE !== 0) {
      throw new Error(
        `Invalid standard animation range for '${animation.name}': yOffset=${currentOffset}, nextOffset=${nextOffset}.`
      );
    }

    areas.push({
      name: animation.name,
      left: 0,
      top: currentOffset,
      width: SPRITESHEET_WIDTH,
      height: segmentHeight
    });
  }

  for (const customArea of character.customAnimations) {
    if (customArea.width <= 0 || customArea.height <= 0) {
      throw new Error(
        `Invalid custom animation area for '${customArea.name}': width=${customArea.width}, height=${customArea.height}.`
      );
    }
    areas.push({
      name: customArea.name,
      left: 0,
      top: customArea.yOffset,
      width: customArea.width,
      height: customArea.height
    });
  }

  return areas;
}

export function validateActionAreaNames(areas: ActionExportArea[]): void {
  const unique = new Set<string>();
  for (const area of areas) {
    if (!/^[a-zA-Z0-9._-]+$/.test(area.name)) {
      throw new Error(
        `Action name '${area.name}' contains unsupported characters. Allowed: letters, digits, dot, underscore, dash.`
      );
    }
    if (unique.has(area.name)) {
      throw new Error(`Action export name collision detected for '${area.name}'.`);
    }
    unique.add(area.name);
  }
}
