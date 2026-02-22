import type { IBinaryConverter } from './IBinaryConverter.js';

/**
 * 1:1 port of BinaryConverterCache.cs
 * Caches IBinaryConverter instances by their constructor type.
 */
export class BinaryConverterCache {
    private static _cache = new Map<new () => IBinaryConverter, IBinaryConverter>();

    static getConverter(type: new () => IBinaryConverter): IBinaryConverter {
        let converter = this._cache.get(type);
        if (!converter) {
            converter = new type();
            this._cache.set(type, converter);
        }
        return converter;
    }
}
