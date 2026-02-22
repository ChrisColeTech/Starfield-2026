// Main entry point for SwitchToolboxLib
// Exports all modules from the library

// Utils
export * from './Utils/index.js';

// Flatbuffers - Common types
export * from './Flatbuffers/Common/index.js';

// Flatbuffers - TR Model types
export * from './Flatbuffers/TR/Model/index.js';

// Flatbuffers - TR Animation types (separate namespace to avoid conflicts)
export * as TRAnimation from './Flatbuffers/TR/Animation/index.js';

// Flatbuffers - TR ResourceDictionary
export * from './Flatbuffers/TR/ResourceDictionary/index.js';

// Flatbuffers - Gfx2
export * from './Flatbuffers/Gfx2/Material.js';

// Flatbuffers - GF Animation (separate namespace)
export * as GFAnimation from './Flatbuffers/GF/Animation/GfAnimation.js';

// Archive
export * from './Archive/index.js';

// Decoders
export * from './Decoders/index.js';

// Exporters
export * from './Exporters/index.js';

// Texture
export * from './Texture/index.js';

// Main Program (CLI entry point)
export { main } from './Program.js';
