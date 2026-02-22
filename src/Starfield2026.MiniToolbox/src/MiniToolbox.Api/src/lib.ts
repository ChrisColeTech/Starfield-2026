// Core library barrel — re-exports all toolkit modules

// Utils
export * from './utils/index.js';

// Flatbuffers - Common types
export * from './flatbuffers/Common/index.js';

// Flatbuffers - TR Model types
export * from './flatbuffers/TR/Model/index.js';

// Flatbuffers - TR Animation types (separate namespace to avoid conflicts)
export * as TRAnimation from './flatbuffers/TR/Animation/index.js';

// Flatbuffers - TR ResourceDictionary
export * from './flatbuffers/TR/ResourceDictionary/index.js';

// Flatbuffers - Gfx2
export * from './flatbuffers/Gfx2/Material.js';

// Flatbuffers - GF Animation (separate namespace)
export * as GFAnimation from './flatbuffers/GF/Animation/GfAnimation.js';

// Archive
export * from './archive/index.js';

// Decoders
export * from './decoders/index.js';

// Exporters
export * from './exporters/index.js';

// Bntx / Texture
export * from './bntx/index.js';

// CLI entry point
export { main, exportModel } from './cli.js';
