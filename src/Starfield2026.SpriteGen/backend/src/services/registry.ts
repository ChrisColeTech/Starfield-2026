import type { GeneratorService, GeneratorType } from '../types/index.js';
import { GrassGenerator } from './grassGenerator.js';
import { FlowerGenerator } from './flowerGenerator.js';
import { TreeGenerator } from './treeGenerator.js';
import { BushGenerator } from './bushGenerator.js';

const generators: Map<string, GeneratorService> = new Map();

function register(gen: GeneratorService) {
  generators.set(gen.type, gen);
}

// Register all built-in generators
register(new GrassGenerator());
register(new FlowerGenerator());
register(new TreeGenerator());
register(new BushGenerator());

export function getGenerator(type: GeneratorType): GeneratorService | undefined {
  return generators.get(type);
}

export function getAllGenerators(): GeneratorService[] {
  return [...generators.values()];
}
