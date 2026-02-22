export const FRAME_SIZE = 64;
export const SPRITESHEET_WIDTH = 13 * FRAME_SIZE;
export const SPRITESHEET_HEIGHT = 54 * FRAME_SIZE;

export interface StandardAnimationDef {
  name: string;
  folderName: string;
  yOffset: number;
}

export const STANDARD_ANIMATIONS: StandardAnimationDef[] = [
  { name: "spellcast", folderName: "spellcast", yOffset: 0 },
  { name: "thrust", folderName: "thrust", yOffset: 4 * FRAME_SIZE },
  { name: "walk", folderName: "walk", yOffset: 8 * FRAME_SIZE },
  { name: "slash", folderName: "slash", yOffset: 12 * FRAME_SIZE },
  { name: "shoot", folderName: "shoot", yOffset: 16 * FRAME_SIZE },
  { name: "hurt", folderName: "hurt", yOffset: 20 * FRAME_SIZE },
  { name: "climb", folderName: "climb", yOffset: 21 * FRAME_SIZE },
  { name: "idle", folderName: "idle", yOffset: 22 * FRAME_SIZE },
  { name: "jump", folderName: "jump", yOffset: 26 * FRAME_SIZE },
  { name: "sit", folderName: "sit", yOffset: 30 * FRAME_SIZE },
  { name: "emote", folderName: "emote", yOffset: 34 * FRAME_SIZE },
  { name: "run", folderName: "run", yOffset: 38 * FRAME_SIZE },
  { name: "combat_idle", folderName: "combat_idle", yOffset: 42 * FRAME_SIZE },
  { name: "backslash", folderName: "backslash", yOffset: 46 * FRAME_SIZE },
  { name: "halfslash", folderName: "halfslash", yOffset: 50 * FRAME_SIZE }
];
