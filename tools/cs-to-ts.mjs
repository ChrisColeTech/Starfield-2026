#!/usr/bin/env node
/**
 * C# to TypeScript converter for SwitchToolboxCli.
 *
 * Handles patterns found in the SwitchToolboxCli codebase:
 * - Classes, enums, interfaces, structs
 * - Properties (auto, required, init, expression-bodied)
 * - Type mappings (primitives, collections, nullable)
 * - Generic classes and methods
 * - Static classes and methods
 * - Extension methods
 * - Attributes (FlatBuffer, DllImport, JsonConverter, etc.)
 * - Switch expressions and pattern matching
 * - LINQ methods
 * - Binary/BinaryReader operations
 * - Math/MathF operations
 * - Tuple deconstruction
 * - Range/index operators
 * - Null-conditional/coalescing operators
 * - File-scoped and block namespaces
 * - using statements → imports
 */

import fs from 'fs'
import path from 'path'

const TYPE_MAP = {
  'uint': 'number',
  'int': 'number',
  'byte': 'number',
  'sbyte': 'number',
  'short': 'number',
  'ushort': 'number',
  'long': 'number',
  'ulong': 'number',
  'float': 'number',
  'double': 'number',
  'decimal': 'number',
  'bool': 'boolean',
  'string': 'string',
  'char': 'string',
  'void': 'void',
  'object': 'any',
  'var': 'let',
  'nint': 'number',
  'nuint': 'number',
  'Int32': 'number',
  'UInt32': 'number',
  'Int16': 'number',
  'UInt16': 'number',
  'Int64': 'number',
  'UInt64': 'number',
  'Byte': 'number',
  'SByte': 'number',
  'Single': 'number',
  'Double': 'number',
  'Boolean': 'boolean',
  'String': 'string',
  'Bitmap': 'ImageData',
  'Color': 'number',
  'Stream': 'BinaryReader',
  'MemoryStream': 'Buffer',
  'FileStream': 'Buffer',
  'Guid': 'string',
  'DateTime': 'Date',
  'TimeSpan': 'number',
  'Action': 'Function',
  'Func': 'Function',
}

const KNOWN_GENERIC_TYPES = new Set([
  'List', 'IList', 'ICollection', 'IEnumerable', 'IReadOnlyList', 'IReadOnlyCollection',
  'Dictionary', 'IDictionary', 'IReadOnlyDictionary',
  'HashSet', 'ISet', 'SortedSet',
  'Queue', 'Stack', 'LinkedList',
  'ObservableCollection',
  'Task', 'ValueTask',
  'Nullable',
  'Span', 'ReadOnlySpan', 'Memory', 'ReadOnlyMemory',
  'FlatBufferUnion',
])

function convertCsharpToTypescript(csCode, fileName) {
  // Normalize line endings to LF
  let ts = csCode.replace(/\r\n/g, '\n')
  const imports = new Map()

  ts = preprocessDirectives(ts)
  ts = convertUsingStatements(ts, imports)
  ts = convertNamespaceDeclarations(ts)
  ts = convertXmlDocs(ts)
  ts = convertAttributes(ts)
  ts = convertStructLayout(ts)
  ts = convertEnumDeclarations(ts)
  ts = convertInterfaceDeclarations(ts)
  ts = convertClassDeclarations(ts)
  ts = convertPropertyDeclarations(ts)
  ts = convertFieldDeclarations(ts)
  ts = convertConstructorDeclarations(ts)
  ts = convertMethodDeclarations(ts)
  ts = convertStringInterpolation(ts)
  ts = convertExpressions(ts)
  ts = convertStatements(ts)
  ts = convertTypes(ts)
  ts = cleanupModifiers(ts)
  ts = cleanupSyntax(ts)

  const importBlock = generateImports(imports, fileName)
  const header = `// Auto-converted from ${fileName}\n// Manual review required\n\n`

  return header + importBlock + ts
}

function preprocessDirectives(ts) {
  ts = ts.replace(/^\s*#region\s+.*$/gm, '')
  ts = ts.replace(/^\s*#endregion\s*$/gm, '')
  ts = ts.replace(/^\s*#if\s+.*$/gm, '')
  ts = ts.replace(/^\s*#else\s*$/gm, '')
  ts = ts.replace(/^\s*#elif\s+.*$/gm, '')
  ts = ts.replace(/^\s*#endif\s*$/gm, '')
  ts = ts.replace(/^\s*#pragma\s+.*$/gm, '')
  return ts
}

function convertUsingStatements(ts, imports) {
  const lines = ts.split('\n')
  const result = []

  for (const line of lines) {
    const usingMatch = line.match(/^using\s+([^;]+);\s*$/)
    if (usingMatch) {
      const usingPath = usingMatch[1].trim()
      if (!usingPath.includes('=') && !usingPath.includes('static')) {
        const parts = usingPath.split('.')
        const typeName = parts[parts.length - 1]
        imports.set(typeName, usingPath)
      }
      continue
    }
    result.push(line)
  }

  return result.join('\n')
}

function generateImports(imports, fileName) {
  if (imports.size === 0) return ''
  const lines = []
  for (const [typeName, importPath] of imports) {
    const modulePath = importPath.replace(/\./g, '/')
    lines.push(`import { ${typeName} } from '${modulePath}'`)
  }
  return lines.join('\n') + '\n\n'
}

function convertXmlDocs(ts) {
  // Convert /// <summary> blocks to /** */
  ts = ts.replace(/^(\s*)\/{3}\s*<summary>\s*\n((?:\s*\/{3}.*\n)*?)\s*\/{3}\s*<\/summary>\s*$/gm,
    (_, indent, body) => {
      const lines = body.split('\n')
        .map(l => l.replace(/^\s*\/{3}\s?/, '').trimEnd())
        .filter(l => l)
      if (lines.length === 1) return `${indent}/** ${lines[0]} */`
      return `${indent}/**\n${lines.map(l => `${indent} * ${l}`).join('\n')}\n${indent} */`
    }
  )
  // Remove any remaining /// lines
  ts = ts.replace(/^\s*\/{3}.*$/gm, '')
  return ts
}

function convertStringInterpolation(ts) {
  // Convert C# $"...{expr}..." to JS `...${expr}...`
  ts = ts.replace(/\$"([^"]*)"/g, (_, content) => {
    // Replace {expr} with ${expr}, but not {{}} (escaped braces)
    let result = content
      .replace(/\{\{/g, '\x00LBRACE\x00')
      .replace(/\}\}/g, '\x00RBRACE\x00')
      .replace(/\{([^}]+)\}/g, '\${$1}')
      .replace(/\x00LBRACE\x00/g, '{')
      .replace(/\x00RBRACE\x00/g, '}')
    // Remove C# format specifiers like :F3, :X16, :N, etc.
    result = result.replace(/\$\{([^}]+):([A-Za-z]\d*)\}/g, '\${$1}')
    return '`' + result + '`'
  })
  return ts
}

function convertNamespaceDeclarations(ts) {
  ts = ts.replace(/^namespace\s+([\w.]+)\s*;?\s*$/gm, '// namespace $1')
  ts = ts.replace(/namespace\s+([\w.]+)\s*\{/g, '// namespace $1 {')
  return ts
}

function convertAttributes(ts) {
  ts = ts.replace(/^\s*\[assembly:[\s\S]*?\]\s*$/gm, '')
  ts = ts.replace(/^\s*\[module:[\s\S]*?\]\s*$/gm, '')
  ts = ts.replace(/^\s*\[(?:FlatBuffer\w+|JsonConverter|StructLayout|DllImport|Flags|Serializable|Obsolete|Conditional|Caller\w+)\b[^\]]*\]\s*$/gm, '')
  ts = ts.replace(/\[(?:FlatBuffer\w+|JsonConverter|JsonIgnore|JsonProperty|StructLayout|DllImport|Flags|Obsolete|Required|Key|Index|Range)\b[^\]]*\]\s*/g, '')
  return ts
}

function convertStructLayout(ts) {
  ts = ts.replace(/^\s*\[StructLayout\([^\]]+\]\s*$/gm, '')
  return ts
}

function convertEnumDeclarations(ts) {
  ts = ts.replace(
    /(?:public|internal|private)?\s*(?:sealed\s+)?enum\s+(\w+)(\s*:\s*(\w+))?\s*\{([^}]+)\}/gs,
    (_, name, _fullTypeAnnotation, _typeName, body) => {
      const tsBody = convertEnumBody(body)
      return 'const ' + name + ' = {\n' + tsBody + '\n} as const';
    }
  );
  return ts;
}

function convertEnumBody(body) {
  const lines = body.split('\n')
  const result = []

  for (let line of lines) {
    line = line.trim()
    if (!line || line.startsWith('//')) continue

    const match = line.match(/^(\w+)\s*=\s*([^,]+),?\s*(?:\/\/.*)?$/)
    if (match) {
      const [, name, value] = match
      let tsValue = value.trim()
      tsValue = tsValue.replace(/\b0x([0-9A-Fa-f]+)\b/g, '0x$1')
      tsValue = tsValue.replace(/(\d+)u\b/g, '$1')
      tsValue = tsValue.replace(/(\d+)L\b/gi, '$1')
      result.push(`  ${name} = ${tsValue},`)
    } else if (line.match(/^\w+,?\s*$/)) {
      result.push(`  ${line.replace(',', '')},`)
    }
  }

  return result.join('\n')
}

function convertInterfaceDeclarations(ts) {
  ts = ts.replace(
    /(?:public|internal|private)?\s*interface\s+(\w+)(?:\s*:\s*([\w.,\s<>]+))?\s*\{/g,
    (_, name, bases) => {
      if (bases) {
        const baseList = bases.split(',').map(b => b.trim()).filter(b => b)
        return `export interface ${name}${baseList.length ? ` extends ${baseList.map(mapType).join(', ')}` : ''} {`
      }
      return `export interface ${name} {`
    }
  )
  return ts
}

function convertClassDeclarations(ts) {
  ts = ts.replace(
    /(?:public|internal|private|protected)?\s*(?:static\s+)?(?:sealed\s+)?(?:abstract\s+)?(?:partial\s+)?class\s+(\w+)(?:<([^>]+)>)?(?:\s*:\s*([\w.,\s<>\?]+))?\s*\{/g,
    (_, name, generics, bases) => {
      let result = 'export '
      const genericParams = generics ? `<${generics.split(',').map(g => {
        const parts = g.trim().split(/\s+where\b/)
        return parts[0].trim()
      }).join(', ')}>` : ''

      result += `class ${name}${genericParams}`

      if (bases) {
        const baseList = bases.split(',').map(b => b.trim()).filter(b => b)
        if (baseList.length > 0) {
          const ext = baseList[0]
          const impls = baseList.slice(1)
          result += ` extends ${mapType(ext)}`
          if (impls.length > 0) {
            result += ` implements ${impls.map(mapType).join(', ')}`
          }
        }
      }

      return result + ' {'
    }
  )

  ts = ts.replace(
    /(?:public|internal|private)?\s*struct\s+(\w+)(?:\s*:\s*([\w.,\s]+))?\s*\{/g,
    (_, name, bases) => {
      if (bases && bases.includes('struct')) {
        return `export class ${name} {`
      }
      return `export class ${name} {`
    }
  )

  ts = ts.replace(
    /(?:public|internal|private)?\s*record\s+(\w+)(?:<([^>]+)>)?(?:\s*:\s*([\w.,\s<>]+))?\s*(?:\([^)]*\)|\{)/g,
    (_, name, generics, bases) => {
      const genericParams = generics ? `<${generics.split(',').map(g => g.trim().split(/\s+where\b/)[0].trim()).join(', ')}>` : ''
      let result = `export class ${name}${genericParams}`
      if (bases) {
        const baseList = bases.split(',').map(b => b.trim()).filter(b => b)
        if (baseList.length > 0) {
          result += ` extends ${mapType(baseList[0])}`
        }
      }
      return result + ' {'
    }
  )

  return ts
}

function convertFieldDeclarations(ts) {
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:const\s+)?(\w+(?:<[\w,\s\[\]<>\?]+>)?)\s+(\w+)\s*=\s*new\s+List<(\w+)>\s*\(\s*\)\s*;/gm,
    (_, indent, _type, name, innerType) => {
      return `${indent}${name}: ${mapType(innerType)}[] = []`
    }
  )

  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:const\s+)?(\w+(?:<[\w,\s<>\?]+>)?)\s+(\w+)\s*=\s*new\s+Dictionary<(\w+),\s*(\w+)>\s*\(\s*\)\s*;/gm,
    (_, indent, _type, name, keyType, valueType) => {
      return `${indent}${name}: Map<${mapType(keyType)}, ${mapType(valueType)}> = new Map()`
    }
  )

  // Standard field with assignment. Match = but NOT =>
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:const\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?)\s+(\w+)\s*=\s*(?![=>])(.+);/gm,
    (_, indent, type, name, value) => {
      const tsType = mapType(type)
      const tsValue = convertValue(value.trim())
      return `${indent}${name}: ${tsType} = ${tsValue}`
    }
  )

  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*;/gm,
    (_, indent, type, name) => {
      const tsType = mapType(type)
      const def = defaultFor(tsType)
      return `${indent}${name}: ${tsType}${def ? ` = ${def}` : ''}`
    }
  )

  return ts
}

function convertPropertyDeclarations(ts) {
  ts = ts.replace(
    /(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?required\s+(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?)\s+(\w+)\s*\{\s*get;\s*init;\s*\}/g,
    (_, type, name) => {
      return `${name}: ${mapType(type)}`
    }
  )

  ts = ts.replace(
    /(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*\{\s*get;\s*(?:private\s+)?set;\s*\}/g,
    (_, type, name) => {
      return `${name}: ${mapType(type)}${defaultFor(mapType(type)) ? ` = ${defaultFor(mapType(type))}` : ''}`
    }
  )

  ts = ts.replace(
    /(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*\{\s*get;\s*\}/g,
    (_, type, name) => {
      return `${name}: ${mapType(type)}`
    }
  )

  ts = ts.replace(
    /(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*=>\s*([^;]+);/g,
    (_, type, name, expr) => {
      return `get ${name}(): ${mapType(type)} { return ${convertExpression(expr.trim())} }`
    }
  )

  ts = ts.replace(
    /(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*\{\s*get\s*=>\s*([^;]+);\s*set\s*=>\s*([^;]+);\s*\}/g,
    (_, type, name, getter, setter) => {
      return `get ${name}(): ${mapType(type)} { return ${convertExpression(getter.trim())} } set ${name}(v: ${mapType(type)}) { ${convertExpression(setter.trim())} }`
    }
  )

  return ts
}

function convertConstructorDeclarations(ts) {
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(\w+)\s*\(([^)]*)\)\s*(?::\s*this\([^)]*\))?(\s*)\{/gm,
    (match, indent, className, params, space) => {
      if (className[0] === className[0].toUpperCase()) {
        return `${indent}constructor(${convertParams(params)})${space}{`
      }
      return match
    }
  )

  return ts
}

function convertMethodDeclarations(ts) {
  // Arrow-bodied methods
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)?\s*(?:static\s+)?(?:override\s+)?(?:async\s+)?(?:unsafe\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?(?:\?)?)\s+(\w+)(?:<[^>]+>)?\s*\(([^)]*)\)(?:\s*where\s+[^{]+)?(\s*)=>\s*([^;]+);/gm,
    (_, indent, returnType, name, params, _space, arrowBody) => {
      const tsReturn = mapType(returnType)
      const tsParams = convertParams(params).replace(/this\s+/, '')
      return `${indent}${name}(${tsParams}): ${tsReturn} { return ${convertExpression(arrowBody.trim())} }`
    }
  )

  // Standard methods
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)?\s*(?:static\s+)?(?:override\s+)?(?:async\s+)?(?:unsafe\s+)?(\w+(?:<[\w,\s<>\?]+>)?(?:\[\])?(?:\?)?)\s+(\w+)(?:<[^>]+>)?\s*\(([^)]*)\)(?:\s*where\s+[^{]+)?(\s*)\{/gm,
    (_, indent, returnType, name, params, space) => {
      if (name === 'constructor' || name === 'get' || name === 'set' || name === 'has') {
        return `${indent}${name}(${convertParams(params)}) {`
      }

      const tsReturn = mapType(returnType)
      const tsParams = convertParams(params).replace(/this\s+/, '')
      return `${indent}${name}(${tsParams}): ${tsReturn}${space}{`
    }
  )

  return ts
}

function convertExpressions(ts) {
  ts = convertSwitchExpressions(ts)
  ts = convertPatternMatching(ts)
  ts = convertLinqMethods(ts)
  ts = convertBinaryOperations(ts)
  ts = convertMathOperations(ts)
  ts = convertStringOperations(ts)
  ts = convertCollectionOperations(ts)
  ts = convertTupleOperations(ts)
  ts = convertNullOperations(ts)
  ts = convertTypeOperations(ts)
  ts = convertNewExpressions(ts)
  ts = convertLambdas(ts)
  ts = convertMiscExpressions(ts)

  return ts
}

function convertSwitchExpressions(ts) {
  ts = ts.replace(
    /(\w+)\s+switch\s*\{/g,
    'switch ($1) {'
  )

  ts = ts.replace(
    /=>\s*(\w+)\s+switch\s*\{/g,
    '=> { switch ($1) {'
  )

  ts = ts.replace(
    /case\s+(\w+)\s+when\s+([^:]+):/g,
    'case $1 if ($2):'
  )

  ts = ts.replace(
    /case\s+null:/g,
    'case null:'
  )

  ts = ts.replace(
    /case\s+([A-Z]\w+)\s+(\w+):/g,
    'case $1 $2:'
  )

  ts = ts.replace(
    /case\s+(\d+)\s*\.\.\s*(\d+):/g,
    'case $1...$2:'
  )

  ts = ts.replace(
    /case\s+(\w+):/g,
    'case $1:'
  )

  ts = ts.replace(
    /_ =>/g,
    'default:')

  return ts
}

function convertPatternMatching(ts) {
  ts = ts.replace(
    /(\w+)\s+is\s+([A-Z]\w+)(?:\s+(\w+))?/g,
    (_, varName, typeName, varAlias) => {
      if (varAlias) {
        return `${varName} instanceof ${typeName}`
      }
      return `${varName} instanceof ${typeName}`
    }
  )

  ts = ts.replace(
    /(\w+)\s+is\s+null/g,
    '$1 === null'
  )

  ts = ts.replace(
    /(\w+)\s+is\s+not\s+null/g,
    '$1 !== null'
  )

  ts = ts.replace(
    /(\w+)\s+is\s+(\d+\.\.\d+)/g,
    '($1 >= $2.start && $1 <= $2.end)'
  )

  return ts
}

function convertLinqMethods(ts) {
  ts = ts.replace(/\.Select\(/g, '.map(')
  ts = ts.replace(/\.Where\(/g, '.filter(')
  ts = ts.replace(/\.FirstOrDefault\(([^)]*)\)/g, (match, defaultVal) => {
    if (defaultVal) {
      return `.find(() => true) ?? ${defaultVal}`
    }
    return '.find(() => true)'
  })
  ts = ts.replace(/\.First\(/g, '.find(')
  ts = ts.replace(/\.FirstOrDefault\(\)/g, '[0]')
  ts = ts.replace(/\.First\(\)/g, '[0]')
  ts = ts.replace(/\.Any\(/g, '.some(')
  ts = ts.replace(/\.Any\(\)/g, '.length > 0')
  ts = ts.replace(/\.All\(/g, '.every(')
  ts = ts.replace(/\.Count\(\)/g, '.length')
  ts = ts.replace(/\.Count\(/g, (match) => {
    return '.filter('
  })
  ts = ts.replace(/\.Sum\(/g, '.reduce((a, b) => a + ')
  ts = ts.replace(/\.Average\(/g, '.reduce((a, b) => a + ')
  ts = ts.replace(/\.Min\(/g, '.reduce((a, b) => Math.min(a, b)')
  ts = ts.replace(/\.Max\(/g, '.reduce((a, b) => Math.max(a, b)')
  ts = ts.replace(/\.OrderBy\(/g, '.sort((a, b) => ')
  ts = ts.replace(/\.OrderByDescending\(([^)]+)\)/g, '.sort((a, b) => { const fn = $1; return fn(b) - fn(a) })')
  ts = ts.replace(/\.ThenBy\(/g, '.thenBy(')
  ts = ts.replace(/\.ThenByDescending\(/g, '.thenByDescending(')
  ts = ts.replace(/\.Skip\(/g, '.slice(')
  ts = ts.replace(/\.Take\(/g, '.slice(0, ')
  ts = ts.replace(/\.SkipWhile\(/g, '.sliceWhile(')
  ts = ts.replace(/\.TakeWhile\(/g, '.takeWhile(')
  ts = ts.replace(/\.Distinct\(\)/g, '.filter((v, i, a) => a.indexOf(v) === i)')
  ts = ts.replace(/\.Reverse\(\)/g, '.reverse()')
  ts = ts.replace(/\.ToList\(\)/g, '')
  ts = ts.replace(/\.ToArray\(\)/g, '')
  ts = ts.replace(/\.AsEnumerable\(\)/g, '')
  ts = ts.replace(/\.Cast<(\w+)>\(\)/g, ' as $1[]')
  ts = ts.replace(/\.OfType<(\w+)>\(\)/g, '.filter((x): x is $1 => x instanceof $1)')
  ts = ts.replace(/\.GroupBy\(/g, '.groupBy(')
  ts = ts.replace(/\.ToDictionary\(/g, '.toDictionary(')
  ts = ts.replace(/\.ToLookup\(/g, '.toLookup(')
  ts = ts.replace(/\.Aggregate\(/g, '.reduce(')
  ts = ts.replace(/\.Union\(/g, '.concat(')
  ts = ts.replace(/\.Intersect\(/g, '.intersect(')
  ts = ts.replace(/\.Except\(/g, '.except(')
  ts = ts.replace(/\.Concat\(/g, '.concat(')
  ts = ts.replace(/\.Zip\(/g, '.zip(')
  ts = ts.replace(/\.SequenceEqual\(/g, '.sequenceEqual(')
  ts = ts.replace(/\.Single\(\)/g, '[0]')
  ts = ts.replace(/\.SingleOrDefault\(\)/g, '[0]')
  ts = ts.replace(/\.Last\(\)/g, '.at(-1)')
  ts = ts.replace(/\.LastOrDefault\(\)/g, '.at(-1)')
  ts = ts.replace(/\.ElementAt\(/g, '[')
  ts = ts.replace(/\.ElementAtOrDefault\(/g, '[')
  ts = ts.replace(/\.Contains\(/g, '.includes(')
  ts = ts.replace(/\.Prepend\(/g, '.unshift(')
  ts = ts.replace(/\.Append\(/g, '.push(')

  return ts
}

function convertBinaryOperations(ts) {
  ts = ts.replace(/BitConverter\.ToSingle\(([^,]+),\s*([^)]+)\)/g, '$1.readFloatLE($2)')
  ts = ts.replace(/BitConverter\.ToDouble\(([^,]+),\s*([^)]+)\)/g, '$1.readDoubleLE($2)')
  ts = ts.replace(/BitConverter\.ToUInt16\(([^,]+),\s*([^)]+)\)/g, '$1.readUInt16LE($2)')
  ts = ts.replace(/BitConverter\.ToInt16\(([^,]+),\s*([^)]+)\)/g, '$1.readInt16LE($2)')
  ts = ts.replace(/BitConverter\.ToUInt32\(([^,]+),\s*([^)]+)\)/g, '$1.readUInt32LE($2)')
  ts = ts.replace(/BitConverter\.ToInt32\(([^,]+),\s*([^)]+)\)/g, '$1.readInt32LE($2)')
  ts = ts.replace(/BitConverter\.ToUInt64\(([^,]+),\s*([^)]+)\)/g, '$1.readBigUInt64LE($2)')
  ts = ts.replace(/BitConverter\.ToInt64\(([^,]+),\s*([^)]+)\)/g, '$1.readBigInt64LE($2)')
  ts = ts.replace(/BitConverter\.GetBytes\(/g, 'Buffer.from(')

  ts = ts.replace(/BitConverter\.UInt16BitsToHalf\(/g, 'halfToFloat(')
  ts = ts.replace(/BitConverter\.HalfToUInt16Bits\(/g, 'floatToHalf(')
  ts = ts.replace(/BitConverter\.SingleToUInt32Bits\(/g, 'floatToBits(')
  ts = ts.replace(/BitConverter\.UInt32BitsToSingle\(/g, 'bitsToFloat(')

  ts = ts.replace(/Buffer\.BlockCopy\(([^,]+),\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^)]+)\)/g, '$1.copy($3, $4, $2, $2 + $5)')

  ts = ts.replace(/\.ReadUInt32\(\)/g, '.readUInt32()')
  ts = ts.replace(/\.ReadInt32\(\)/g, '.readInt32()')
  ts = ts.replace(/\.ReadUInt16\(\)/g, '.readUInt16()')
  ts = ts.replace(/\.ReadInt16\(\)/g, '.readInt16()')
  ts = ts.replace(/\.ReadByte\(\)/g, '.readByte()')
  ts = ts.replace(/\.ReadSByte\(\)/g, '.readSByte()')
  ts = ts.replace(/\.ReadSingle\(\)/g, '.readFloat()')
  ts = ts.replace(/\.ReadDouble\(\)/g, '.readDouble()')
  ts = ts.replace(/\.ReadBoolean\(\)/g, '.readBoolean()')
  ts = ts.replace(/\.ReadChar\(\)/g, '.readChar()')
  ts = ts.replace(/\.ReadString\(\)/g, '.readString()')
  ts = ts.replace(/\.ReadBytes\(([^)]+)\)/g, '.readBytes($1)')
  ts = ts.replace(/\.Read\(([^,]+),\s*(\d+),\s*([^)]+)\)/g, '.read($1, $2, $3)')

  ts = ts.replace(/\.BaseStream\.Seek\(([^,]+),\s*SeekOrigin\.Begin\)/g, '.seek($1)')
  ts = ts.replace(/\.BaseStream\.Seek\(([^,]+),\s*SeekOrigin\.Current\)/g, '.seekRelative($1)')
  ts = ts.replace(/\.BaseStream\.Seek\(([^,]+),\s*SeekOrigin\.End\)/g, '.seekFromEnd($1)')
  ts = ts.replace(/\.BaseStream\.Position/g, '.position')
  ts = ts.replace(/\.BaseStream\.Length/g, '.length')
  ts = ts.replace(/\.BaseStream\.Read\(/g, '.read(')

  ts = ts.replace(/(\w+)\.Seek\(([^,]+),\s*SeekOrigin\.Begin\)/g, '$1.seek($2)')
  ts = ts.replace(/(\w+)\.Seek\(([^,]+),\s*SeekOrigin\.Current\)/g, '$1.seekRelative($2)')
  ts = ts.replace(/(\w+)\.Seek\(([^,]+),\s*SeekOrigin\.End\)/g, '$1.seekFromEnd($2)')

  return ts
}

function convertMathOperations(ts) {
  ts = ts.replace(/MathF\.Sqrt\(/g, 'Math.sqrt(')
  ts = ts.replace(/MathF\.Sin\(/g, 'Math.sin(')
  ts = ts.replace(/MathF\.Cos\(/g, 'Math.cos(')
  ts = ts.replace(/MathF\.Tan\(/g, 'Math.tan(')
  ts = ts.replace(/MathF\.Asin\(/g, 'Math.asin(')
  ts = ts.replace(/MathF\.Acos\(/g, 'Math.acos(')
  ts = ts.replace(/MathF\.Atan\(/g, 'Math.atan(')
  ts = ts.replace(/MathF\.Atan2\(/g, 'Math.atan2(')
  ts = ts.replace(/MathF\.Floor\(/g, 'Math.floor(')
  ts = ts.replace(/MathF\.Ceiling\(/g, 'Math.ceil(')
  ts = ts.replace(/MathF\.Round\(/g, 'Math.round(')
  ts = ts.replace(/MathF\.Abs\(/g, 'Math.abs(')
  ts = ts.replace(/MathF\.Max\(/g, 'Math.max(')
  ts = ts.replace(/MathF\.Min\(/g, 'Math.min(')
  ts = ts.replace(/MathF\.Pow\(/g, 'Math.pow(')
  ts = ts.replace(/MathF\.Log\(/g, 'Math.log(')
  ts = ts.replace(/MathF\.Log10\(/g, 'Math.log10(')
  ts = ts.replace(/MathF\.Log2\(/g, 'Math.log2(')
  ts = ts.replace(/MathF\.Exp\(/g, 'Math.exp(')
  ts = ts.replace(/MathF\.Sign\(/g, 'Math.sign(')
  ts = ts.replace(/MathF\.Truncate\(/g, 'Math.trunc(')

  ts = ts.replace(/Math\.Clamp\(([^,]+),\s*([^,]+),\s*([^)]+)\)/g, 'Math.min(Math.max($1, $2), $3)')
  ts = ts.replace(/Math\.Max\(/g, 'Math.max(')
  ts = ts.replace(/Math\.Min\(/g, 'Math.min(')
  ts = ts.replace(/Math\.Sqrt\(/g, 'Math.sqrt(')
  ts = ts.replace(/Math\.Abs\(/g, 'Math.abs(')
  ts = ts.replace(/Math\.Floor\(/g, 'Math.floor(')
  ts = ts.replace(/Math\.Ceiling\(/g, 'Math.ceil(')
  ts = ts.replace(/Math\.Round\(/g, 'Math.round(')
  ts = ts.replace(/Math\.Pow\(/g, 'Math.pow(')
  ts = ts.replace(/Math\.Sin\(/g, 'Math.sin(')
  ts = ts.replace(/Math\.Cos\(/g, 'Math.cos(')
  ts = ts.replace(/Math\.Tan\(/g, 'Math.tan(')
  ts = ts.replace(/Math\.Log\(/g, 'Math.log(')
  ts = ts.replace(/Math\.Exp\(/g, 'Math.exp(')

  return ts
}

function convertStringOperations(ts) {
  ts = ts.replace(/string\.Empty/g, "''")
  ts = ts.replace(/string\.IsNullOrEmpty\(([^)]+)\)/g, '!$1')
  ts = ts.replace(/string\.IsNullOrWhiteSpace\(([^)]+)\)/g, '!$1?.trim()')
  ts = ts.replace(/string\.Join\(([^,]+),\s*([^)]+)\)/g, '$2.join($1)')
  ts = ts.replace(/string\.Split\(([^,)]+)(?:,\s*([^)]+))?\)/g, '$0.split($1)')
  ts = ts.replace(/string\.Format\("([^"]+)"((?:,\s*[^)]+)*)\)/g, (_, fmt, args) => {
    if (!args) return `'${fmt}'`
    const argList = args.replace(/^\s*,\s*/, '').split(/\s*,\s*/)
    let result = fmt
    argList.forEach((arg, i) => {
      const pattern = new RegExp(`\\{${i}(?::[^}]+)?\\}`, 'g')
      result = result.replace(pattern, `\${${arg.trim()}}`)
    })
    return '`' + result + '`'
  })
  ts = ts.replace(/string\.Compare\(([^,]+),\s*([^,]+),\s*([^)]+)\)/g, '$1.localeCompare($2)')
  ts = ts.replace(/string\.CompareOrdinal\(([^,]+),\s*([^)]+)\)/g, '($1 < $2 ? -1 : $1 > $2 ? 1 : 0)')
  ts = ts.replace(/string\.Concat\(/g, '[].concat(')
  ts = ts.replace(/string\.Copy\(([^)]+)\)/g, '$1')
  ts = ts.replace(/string\.Equals\(([^,]+),\s*([^,]+),\s*StringComparison\.OrdinalIgnoreCase\)/g, '$1.toLowerCase() === $2.toLowerCase()')
  ts = ts.replace(/string\.Equals\(([^,]+),\s*([^)]+)\)/g, '$1 === $2')

  ts = ts.replace(/\.Length\b/g, '.length')
  ts = ts.replace(/\.ToUpper\(\)/g, '.toUpperCase()')
  ts = ts.replace(/\.ToLower\(\)/g, '.toLowerCase()')
  ts = ts.replace(/\.Trim\(\)/g, '.trim()')
  ts = ts.replace(/\.TrimStart\(\)/g, '.trimStart()')
  ts = ts.replace(/\.TrimEnd\(\)/g, '.trimEnd()')
  ts = ts.replace(/\.StartsWith\(([^)]+)\)/g, '.startsWith($1)')
  ts = ts.replace(/\.EndsWith\(([^,)]+)(?:,\s*StringComparison\.\w+)?\)/g, '.endsWith($1)')
  ts = ts.replace(/\.Contains\(([^)]+)\)/g, '.includes($1)')
  ts = ts.replace(/\.IndexOf\(([^)]+)\)/g, '.indexOf($1)')
  ts = ts.replace(/\.LastIndexOf\(([^)]+)\)/g, '.lastIndexOf($1)')
  ts = ts.replace(/\.Substring\(([^,)]+)(?:,\s*([^)]+))?\)/g, '.substring($1)')
  ts = ts.replace(/\.Replace\(([^,]+),\s*([^)]+)\)/g, '.replace($1, $2)')
  ts = ts.replace(/\.Split\(([^)]+)\)/g, '.split($1)')
  ts = ts.replace(/\.PadLeft\(([^,)]+)(?:,\s*'([^)]+)')?\)/g, '.padStart($1)')
  ts = ts.replace(/\.PadRight\(([^,)]+)(?:,\s*'([^)]+)')?\)/g, '.padEnd($1)')
  ts = ts.replace(/\.ToCharArray\(\)/g, ".split('')")
  ts = ts.replace(/\.ToString\(\)/g, '.toString()')
  ts = ts.replace(/\.Equals\(([^,]+),\s*StringComparison\.OrdinalIgnoreCase\)/g, '.toLowerCase() === $1.toLowerCase()')
  ts = ts.replace(/\.Equals\(([^)]+)\)/g, ' === $1')
  ts = ts.replace(/\.CompareTo\(([^)]+)\)/g, '.localeCompare($1)')
  ts = ts.replace(/\.Insert\(([^,]+),\s*([^)]+)\)/g, '.slice(0, $1) + $2 + .slice($1)')
  ts = ts.replace(/\.Remove\(([^,)]+)(?:,\s*([^)]+))?\)/g, '.slice(0, $1)')

  return ts
}

function convertCollectionOperations(ts) {
  ts = ts.replace(/new\s+List<(\w+)>\s*\(\)/g, '[] as $1[]')
  ts = ts.replace(/new\s+List<(\w+)>\s*\{([^}]*)\}/g, '[$2] as $1[]')
  ts = ts.replace(/new\s+Dictionary<(\w+),\s*(\w+)>\s*\(\)/g, 'new Map<$1, $2>()')
  ts = ts.replace(/new\s+HashSet<(\w+)>\s*\(\)/g, 'new Set<$1>()')
  ts = ts.replace(/new\s+Queue<(\w+)>\s*\(\)/g, '[] as $1[]')
  ts = ts.replace(/new\s+Stack<(\w+)>\s*\(\)/g, '[] as $1[]')

  ts = ts.replace(/\.Add\(([^)]+)\)/g, '.push($1)')
  ts = ts.replace(/\.AddRange\(([^)]+)\)/g, '.push(...$1)')
  ts = ts.replace(/\.Remove\(([^)]+)\)/g, '.splice($.indexOf($1), 1)')
  ts = ts.replace(/\.RemoveAt\(([^)]+)\)/g, '.splice($1, 1)')
  ts = ts.replace(/\.RemoveAll\(([^)]+)\)/g, '.filter(x => !($1)(x))')
  ts = ts.replace(/\.Clear\(\)/g, '.length = 0')
  ts = ts.replace(/\.Insert\(([^,]+),\s*([^)]+)\)/g, '.splice($1, 0, $2)')
  ts = ts.replace(/\.InsertRange\(([^,]+),\s*([^)]+)\)/g, '.splice($1, 0, ...$2)')
  ts = ts.replace(/\.Reverse\(\)/g, '.reverse()')
  ts = ts.replace(/\.Sort\(\)/g, '.sort()')
  ts = ts.replace(/\.Sort\(([^)]+)\)/g, '.sort($1)')
  ts = ts.replace(/\.ToArray\(\)/g, '')
  ts = ts.replace(/\.ToList\(\)/g, '')
  ts = ts.replace(/\.AsReadOnly\(\)/g, '')
  ts = ts.replace(/\.Find\(([^)]+)\)/g, '.find($1)')
  ts = ts.replace(/\.FindAll\(([^)]+)\)/g, '.filter($1)')
  ts = ts.replace(/\.FindIndex\(([^)]+)\)/g, '.findIndex($1)')
  ts = ts.replace(/\.FindLast\(([^)]+)\)/g, '.findLast($1)')
  ts = ts.replace(/\.FindLastIndex\(([^)]+)\)/g, '.findLastIndex($1)')
  ts = ts.replace(/\.Exists\(([^)]+)\)/g, '.some($1)')
  ts = ts.replace(/\.TrueForAll\(([^)]+)\)/g, '.every($1)')
  ts = ts.replace(/\.ForEach\(([^)]+)\)/g, '.forEach($1)')
  ts = ts.replace(/\.GetRange\(([^,]+),\s*([^)]+)\)/g, '.slice($1, $1 + $2)')
  ts = ts.replace(/\.CopyTo\(([^)]+)\)/g, '.slice()')
  ts = ts.replace(/\.BinarySearch\(([^)]+)\)/g, '.binarySearch($1)')
  ts = ts.replace(/\.IndexOf\(([^)]+)\)/g, '.indexOf($1)')
  ts = ts.replace(/\.LastIndexOf\(([^)]+)\)/g, '.lastIndexOf($1)')

  // Dictionary-style indexer → Map.set/get only for known dictionary variables
  // Removed: overly aggressive [x] → .get(x) conversion was destroying all array access
  // These are best handled manually during review
  ts = ts.replace(/\.TryGetValue\(([^,]+),\s*out\s+(\w+)\)/g, '($2 = $1.get($1)) !== undefined')
  ts = ts.replace(/\.ContainsKey\(([^)]+)\)/g, '.has($1)')
  ts = ts.replace(/\.ContainsValue\(([^)]+)\)/g, '.hasValue($1)')
  ts = ts.replace(/\.Add\(([^,]+),\s*([^)]+)\)/g, '.set($1, $2)')
  ts = ts.replace(/\.Keys\b/g, '.keys()')
  ts = ts.replace(/\.Values\b/g, '.values()')
  ts = ts.replace(/\.Count\b/g, '.size')

  return ts
}

function convertTupleOperations(ts) {
  ts = ts.replace(/var\\s+\\([^)]+\\)\\s*=/g, 'const [$1] =');
  ts = ts.replace(/Tuple\\.Create\\(([^)]+)\\)/g, '[$1]');
  ts = ts.replace(/ValueTuple\\.Create\\(([^)]+)\\)/g, '[$1]');
  return ts;
}

function convertNullOperations(ts) {
  ts = ts.replace(/(\w+)\?\?/g, '$1 ||')
  ts = ts.replace(/(\w+)\?\.\[/g, '$1?.[')
  ts = ts.replace(/(\w+)\?\.(\w+)/g, '$1?.$2')
  ts = ts.replace(/(\w+)\?\?\?/g, '$1 ??')

  return ts
}

function convertTypeOperations(ts) {
  ts = ts.replace(/typeof\((\w+)\)/g, '$1')
  ts = ts.replace(/nameof\(([^)]+)\)/g, "'$1'")
  ts = ts.replace(/sizeof\((\w+)\)/g, 'sizeof($1)')
  ts = ts.replace(/default\((\w+)\)/g, 'default$1Value')
  ts = ts.replace(/default\b/g, 'undefined')
  ts = ts.replace(/\bis\s+null\b/g, '=== null')
  ts = ts.replace(/\bis\s+not\s+null\b/g, '!== null')
  ts = ts.replace(/\bis\s+(\w+)\b/g, 'instanceof $1')

  ts = ts.replace(/\(int\)\s*(\w+)/g, 'Math.floor($1)')
  ts = ts.replace(/\(float\)\s*(\w+)/g, 'Number($1)')
  ts = ts.replace(/\(double\)\s*(\w+)/g, 'Number($1)')
  ts = ts.replace(/\(uint\)\s*(\w+)/g, '($1 >>> 0)')
  ts = ts.replace(/\(long\)\s*(\w+)/g, 'BigInt($1)')
  ts = ts.replace(/\(ulong\)\s*(\w+)/g, 'BigInt($1)')
  ts = ts.replace(/\(byte\)\s*(\w+)/g, '($1 & 0xFF)')
  ts = ts.replace(/\(sbyte\)\s*(\w+)/g, '($1 << 24 >> 24)')
  ts = ts.replace(/\(short\)\s*(\w+)/g, '($1 << 16 >> 16)')
  ts = ts.replace(/\(ushort\)\s*(\w+)/g, '($1 & 0xFFFF)')
  ts = ts.replace(/\(char\)\s*(\w+)/g, 'String.fromCharCode($1)')
  ts = ts.replace(/\(bool\)\s*(\w+)/g, '!!$1')
  ts = ts.replace(/\(string\)\s*(\w+)/g, 'String($1)')

  return ts
}

function convertNewExpressions(ts) {
  ts = ts.replace(/new\s+(\w+)\s*\[\s*\]/g, '[] as $1[]')
  ts = ts.replace(/new\s+(\w+)\[(\d+)\]/g, 'new Array<$1>($2).fill(null)')
  ts = ts.replace(/new\s+(\w+)\[(\w+)\]/g, 'new Array<$1>($2).fill(null)')
  ts = ts.replace(/new\s+(\w+)<([\w,\s<>]+)>\s*\(\s*\)/g, 'new $1<$2>()')
  ts = ts.replace(/new\s+(\w+)<([\w,\s]+)>\s*\(\s*\)/g, 'new $1<$2>()')
  ts = ts.replace(/new\s+MemoryStream\(([^)]+)\)/g, 'BinaryReader.fromBuffer($1)')
  ts = ts.replace(/new\s+BinaryReader\(([^)]+)\)/g, '$1')
  ts = ts.replace(/new\s+FileStream\(([^,]+),\s*FileMode\.Open\)/g, 'fs.openSync($1)')
  ts = ts.replace(/new\s+StreamReader\(([^)]+)\)/g, 'fs.readFileSync($1, "utf8")')
  ts = ts.replace(/new\s+StreamWriter\(([^)]+)\)/g, 'fs.createWriteStream($1)')

  return ts
}

function convertLambdas(ts) {
  ts = ts.replace(/\(([^)]*)\)\s*=>\s*/g, '($1) => ')
  ts = ts.replace(/(\w+)\s*=>\s*/g, '($1) => ')

  return ts
}

function convertMiscExpressions(ts) {
  ts = ts.replace(/\bchecked\s*\{/g, '{')
  ts = ts.replace(/\bunchecked\s*\{/g, '{')
  ts = ts.replace(/\bunsafe\s*\{/g, '{')
  ts = ts.replace(/\bunsafe\s+/g, '')
  ts = ts.replace(/\bfixed\s*\([^)]*\)\s*\{/g, '{')
  ts = ts.replace(/\bstackalloc\s+(\w+)\[(\w+)\]/g, 'new $1[$2]')
  ts = ts.replace(/\bsizeof\((\w+)\)/g, 'sizeof($1)')

  ts = ts.replace(/(\d+)f\b/gi, '$1')
  ts = ts.replace(/(\d+\.\d+)f\b/gi, '$1')
  ts = ts.replace(/(\d+)u\b/gi, '$1')
  ts = ts.replace(/(\d+)U\b/g, '$1')
  ts = ts.replace(/(\d+)L\b/g, '$1')
  ts = ts.replace(/(\d+)l\b/g, '$1')
  ts = ts.replace(/(\d+)UL\b/g, '$1')
  ts = ts.replace(/(\d+)ul\b/g, '$1')
  ts = ts.replace(/(\d+)m\b/g, '$1')
  ts = ts.replace(/(\d+)d\b/g, '$1')

  ts = ts.replace(/(\w+)\[(\d+)\.\.(\d+)\]/g, '$1.slice($2, $3)')
  ts = ts.replace(/(\w+)\[\.\.(\d+)\]/g, '$1.slice(0, $2)')
  ts = ts.replace(/(\w+)\[(\d+)\.\.\]/g, '$1.slice($1)')
  ts = ts.replace(/(\w+)\[\^(\d+)\]/g, '$1[$1.length - $2]')
  ts = ts.replace(/(\w+)\[(\d+)\.\.\^(\d+)\]/g, '$1.slice($2, $1.length - $3)')

  ts = ts.replace(/Console\.WriteLine\(([^)]*)\)/g, 'console.log($1)')
  ts = ts.replace(/Console\.Write\(([^)]*)\)/g, 'process.stdout.write(String($1))')
  ts = ts.replace(/Console\.Error\.WriteLine\(([^)]*)\)/g, 'console.error($1)')
  ts = ts.replace(/Console\.ReadLine\(\)/g, 'await readline()')
  ts = ts.replace(/Debug\.Log\(([^)]*)\)/g, 'console.log($1)')
  ts = ts.replace(/Debug\.LogWarning\(([^)]*)\)/g, 'console.warn($1)')
  ts = ts.replace(/Debug\.LogError\(([^)]*)\)/g, 'console.error($1)')
  ts = ts.replace(/Trace\.WriteLine\(([^)]*)\)/g, 'console.log($1)')

  ts = ts.replace(/File\.ReadAllText\(([^)]+)\)/g, 'fs.readFileSync($1, "utf8")')
  ts = ts.replace(/File\.ReadAllLines\(([^)]+)\)/g, 'fs.readFileSync($1, "utf8").split(/\\n/)')
  ts = ts.replace(/File\.ReadAllBytes\(([^)]+)\)/g, 'fs.readFileSync($1)')
  ts = ts.replace(/File\.WriteAllText\(([^,]+),\s*([^)]+)\)/g, 'fs.writeFileSync($1, $2)')
  ts = ts.replace(/File\.WriteAllBytes\(([^,]+),\s*([^)]+)\)/g, 'fs.writeFileSync($1, $2)')
  ts = ts.replace(/File\.WriteAllLines\(([^,]+),\s*([^)]+)\)/g, 'fs.writeFileSync($1, $2.join("\\n"))')
  ts = ts.replace(/File\.Exists\(([^)]+)\)/g, 'fs.existsSync($1)')
  ts = ts.replace(/File\.Delete\(([^)]+)\)/g, 'fs.unlinkSync($1)')
  ts = ts.replace(/File\.Copy\(([^,]+),\s*([^)]+)\)/g, 'fs.copyFileSync($1, $2)')
  ts = ts.replace(/File\.Move\(([^,]+),\s*([^)]+)\)/g, 'fs.renameSync($1, $2)')

  ts = ts.replace(/Directory\.CreateDirectory\(([^)]+)\)/g, 'fs.mkdirSync($1, { recursive: true })')
  ts = ts.replace(/Directory\.Exists\(([^)]+)\)/g, 'fs.existsSync($1)')
  ts = ts.replace(/Directory\.Delete\(([^,)]+)(?:,\s*true)?\)/g, 'fs.rmSync($1, { recursive: true })')
  ts = ts.replace(/Directory\.GetFiles\(([^)]+)\)/g, 'fs.readdirSync($1)')
  ts = ts.replace(/Directory\.GetDirectories\(([^)]+)\)/g, 'fs.readdirSync($1, { withFileTypes: true }).filter(d => d.isDirectory()).map(d => d.name)')
  ts = ts.replace(/Directory\.GetCurrentDirectory\(\)/g, 'process.cwd()')
  ts = ts.replace(/Directory\.SetCurrentDirectory\(([^)]+)\)/g, 'process.chdir($1)')

  ts = ts.replace(/Path\.Combine\(([^,]+),\s*([^)]+)\)/g, 'path.join($1, $2)')
  ts = ts.replace(/Path\.GetDirectoryName\(([^)]+)\)/g, 'path.dirname($1)')
  ts = ts.replace(/Path\.GetFileName\(([^)]+)\)/g, 'path.basename($1)')
  ts = ts.replace(/Path\.GetFileNameWithoutExtension\(([^)]+)\)/g, 'path.parse($1).name')
  ts = ts.replace(/Path\.GetExtension\(([^)]+)\)/g, 'path.extname($1)')
  ts = ts.replace(/Path\.GetFullPath\(([^)]+)\)/g, 'path.resolve($1)')
  ts = ts.replace(/Path\.GetTempPath\(\)/g, 'os.tmpdir()')
  ts = ts.replace(/Path\.GetRandomFileName\(\)/g, 'crypto.randomBytes(8).toString("hex")')

  ts = ts.replace(/Environment\.CurrentDirectory/g, 'process.cwd()')
  ts = ts.replace(/Environment\.NewLine/g, '"\\n"')
  ts = ts.replace(/Environment\.GetFolderPath\([^)]+\)/g, 'os.homedir()')
  ts = ts.replace(/Environment\.GetCommandLineArgs\(\)/g, 'process.argv')
  ts = ts.replace(/AppContext\.BaseDirectory/g, '__dirname')
  ts = ts.replace(/AppDomain\.CurrentDomain\.BaseDirectory/g, '__dirname')
  ts = ts.replace(/Assembly\.GetExecutingAssembly\(\)\.Location/g, '__filename')

  ts = ts.replace(/Guid\.NewGuid\(\)/g, 'crypto.randomUUID()')
  ts = ts.replace(/Guid\.Parse\(([^)]+)\)/g, '$1')
  ts = ts.replace(/Guid\.Empty/g, '"00000000-0000-0000-0000-000000000000"')

  ts = ts.replace(/DateTime\.UtcNow/g, 'new Date()')
  ts = ts.replace(/DateTime\.Now/g, 'new Date()')
  ts = ts.replace(/DateTime\.Today/g, 'new Date()')

  ts = ts.replace(/Array\.Empty<(\w+)>\(\)/g, '[] as $1[]')
  ts = ts.replace(/Array\.Resize\(ref\s+(\w+),\s*([^)]+)\)/g, '$1.length = $2')
  ts = ts.replace(/Array\.Copy\(([^,]+),\s*([^,]+),\s*([^)]+)\)/g, '$1.copy($2, 0, 0, $3)')
  ts = ts.replace(/Array\.IndexOf\(([^,]+),\s*([^)]+)\)/g, '$1.indexOf($2)')
  ts = ts.replace(/Array\.Find\(([^,]+),\s*([^)]+)\)/g, '$1.find($2)')
  ts = ts.replace(/Array\.FindAll\(([^,]+),\s*([^)]+)\)/g, '$1.filter($2)')
  ts = ts.replace(/Array\.Sort\(([^)]+)\)/g, '$1.sort()')
  ts = ts.replace(/Array\.Reverse\(([^)]+)\)/g, '$1.reverse()')
  ts = ts.replace(/Array\.Clear\(([^,]+),\s*([^,]+),\s*([^)]+)\)/g, '$1.fill(undefined, $2, $2 + $3)')

  ts = ts.replace(/Convert\.ToUInt16\(([^)]+)\)/g, '($1 >>> 0) & 0xFFFF')
  ts = ts.replace(/Convert\.ToInt16\(([^)]+)\)/g, '($1 << 16 >> 16)')
  ts = ts.replace(/Convert\.ToUInt32\(([^)]+)\)/g, '$1 >>> 0')
  ts = ts.replace(/Convert\.ToInt32\(([^)]+)\)/g, '$1 | 0')
  ts = ts.replace(/Convert\.ToUInt64\(([^)]+)\)/g, 'BigInt($1)')
  ts = ts.replace(/Convert\.ToInt64\(([^)]+)\)/g, 'BigInt($1)')
  ts = ts.replace(/Convert\.ToByte\(([^)]+)\)/g, '$1 & 0xFF')
  ts = ts.replace(/Convert\.ToSByte\(([^)]+)\)/g, '($1 << 24 >> 24)')
  ts = ts.replace(/Convert\.ToSingle\(([^)]+)\)/g, 'Number($1)')
  ts = ts.replace(/Convert\.ToDouble\(([^)]+)\)/g, 'Number($1)')
  ts = ts.replace(/Convert\.ToBoolean\(([^)]+)\)/g, '!!$1')
  ts = ts.replace(/Convert\.ToString\(([^)]+)\)/g, 'String($1)')
  ts = ts.replace(/Convert\.ToChar\(([^)]+)\)/g, 'String.fromCharCode($1)')
  ts = ts.replace(/Convert\.FromBase64String\(([^)]+)\)/g, 'Buffer.from($1, "base64")')
  ts = ts.replace(/Convert\.ToBase64String\(([^)]+)\)/g, '$1.toString("base64")')
  ts = ts.replace(/Convert\.ToHexString\(([^)]+)\)/g, '$1.toString("hex")')
  ts = ts.replace(/Convert\.FromHexString\(([^)]+)\)/g, 'Buffer.from($1, "hex")')

  return ts
}

function convertStatements(ts) {
  ts = ts.replace(/using\s*\(([^)]+)\)\s*\{/g, '{')
  ts = ts.replace(/using\s+(\w+)\s*=/g, 'const $1 =')
  ts = ts.replace(/using\s+var\s+(\w+)\s*=/g, 'const $1 =')
  ts = ts.replace(/using\s+(\w+)\s+(\w+)\s*=/g, 'const $2 =')

  ts = ts.replace(/foreach\s*\(\s*(?:var|let|const|[\w<>?]+)\s+(\w+)\s+in\s+([^)]+)\)/g, 'for (const $1 of $2)')

  ts = ts.replace(/for\s*\(\s*(?:int|uint|long|ulong)\s+(\w+)\s*=\s*(\d+)\s*;\s*\1\s*<\s*([^;]+)\s*;\s*\1(\+\+|\+\= 1)\s*\)/g, 'for (let $1 = $2; $1 < $3; $1++)')
  ts = ts.replace(/for\s*\(\s*(?:int|uint|long|ulong)\s+(\w+)\s*=\s*(\d+)\s*;\s*\1\s*<=\s*([^;]+)\s*;\s*\1(\+\+|\+\= 1)\s*\)/g, 'for (let $1 = $2; $1 <= $3; $1++)')
  ts = ts.replace(/for\s*\(\s*(?:int|uint|long|ulong)\s+(\w+)\s*=\s*([^;]+)\s*;\s*\1\s*<\s*([^;]+)\s*;\s*\1\s*\+=\s*([^)]+)\)/g, 'for (let $1 = $2; $1 < $3; $1 += $4)')

  ts = ts.replace(/lock\s*\(([^)]+)\)\s*\{/g, '{')

  ts = ts.replace(/try\s*\{/g, 'try {')
  ts = ts.replace(/catch\s*\((\w+)\s+(\w+)\)\s*\{/g, 'catch ($2) {')
  ts = ts.replace(/catch\s*\{/g, 'catch {')
  ts = ts.replace(/finally\s*\{/g, 'finally {')

  ts = ts.replace(/throw\s+new\s+(\w+)Exception\(([^)]*)\)/g, 'throw new Error($2)')
  ts = ts.replace(/throw\s+new\s+(\w+)\(([^)]*)\)/g, 'throw new $1($2)')
  ts = ts.replace(/throw;/g, 'throw e')

  ts = ts.replace(/yield\s+return\s+/g, 'yield ')
  ts = ts.replace(/yield\s+break;/g, 'return')

  ts = ts.replace(/async\s+void\s+(\w+)\s*\(/g, 'async $1(')
  ts = ts.replace(/async\s+Task\s+(\w+)\s*\(/g, 'async $1(')
  ts = ts.replace(/async\s+Task<(\w+)>\s+(\w+)\s*\(/g, 'async $2(')
  ts = ts.replace(/await\s+/g, 'await ')

  ts = ts.replace(/(\w+)\.Dispose\(\)/g, '')
  ts = ts.replace(/(\w+)\.Close\(\)/g, '')
  ts = ts.replace(/(\w+)\.Flush\(\)/g, '')

  return ts
}

function convertTypes(ts) {
  ts = ts.replace(/\bbyte\[\]/g, 'Buffer')
  ts = ts.replace(/\bint\[\]/g, 'number[]')
  ts = ts.replace(/\buint\[\]/g, 'number[]')
  ts = ts.replace(/\bfloat\[\]/g, 'number[]')
  ts = ts.replace(/\bdouble\[\]/g, 'number[]')
  ts = ts.replace(/\bbool\[\]/g, 'boolean[]')
  ts = ts.replace(/\bstring\[\]/g, 'string[]')
  ts = ts.replace(/\bchar\[\]/g, 'string[]')
  ts = ts.replace(/\bshort\[\]/g, 'number[]')
  ts = ts.replace(/\bushort\[\]/g, 'number[]')
  ts = ts.replace(/\blong\[\]/g, 'number[]')
  ts = ts.replace(/\bulong\[\]/g, 'number[]')

  // Primitives in method bodies
  ts = ts.replace(/\b(?:int|uint|byte|sbyte|short|ushort|long|ulong|float|double|decimal)\s+(\w+)\s*=/g, 'let $1 =')
  ts = ts.replace(/\b(?:string|bool|var)\s+(\w+)\s*=/g, 'let $1 =')

  return ts
}

function cleanupModifiers(ts) {
  ts = ts.replace(/required\\s+/g, '');
  ts = ts.replace(/\\bpublic\\s+/g, '')
  ts = ts.replace(/\\bprivate\\s+/g, '')
  ts = ts.replace(/\\bprotected\\s+/g, '')
  ts = ts.replace(/\\binternal\\s+/g, '')
  ts = ts.replace(/\\bvirtual\\s+/g, '')
  ts = ts.replace(/\\boverride\\s+/g, '')
  ts = ts.replace(/\\bsealed\\s+/g, '')
  ts = ts.replace(/\\breadonly\\s+/g, '')
  ts = ts.replace(/\\bpartial\\s+/g, '')
  ts = ts.replace(/\\babstract\\s+/g, '')
  ts = ts.replace(/\\bnew\\s+(?=\\w+\\s+\\w+\\s*[;(=])/g, '')
  ts = ts.replace(/\\bconst\\s+/g, 'readonly ')
  ts = ts.replace(/\\bvolatile\\s+/g, '')
  ts = ts.replace(/\\bextern\\s+/g, '')
  ts = ts.replace(/\\bref\\s+/g, '')
  ts = ts.replace(/\\bout\\s+/g, '')
  ts = ts.replace(/\\bin\\s+(?=\\w+\\s+\\w+)/g, '')
  ts = ts.replace(/\\bparams\\s+/g, '...')
  ts = ts.replace(/\\bstatic\\s+(?=\\(\\))/g, '')
  // Normalize extra spaces left by modifier removal
  ts = ts.replace(/[ \\t]+/g, ' ')
  return ts
}

function cleanupSyntax(ts) {
  ts = ts.replace(/\n\s*\n\s*\n/g, '\n\n')
  ts = ts.replace(/\{\s*\}/g, '{}')
  // Fix double braces from method bodies
  ts = ts.replace(/\)\s*\{\{/gm, ') {')
  // Convert C# new() shorthand based on type context
  // This is tricky without a full parser, but we can catch common cases
  ts = ts.replace(/:\s*Map<([^>]+)>\s*=\s*new\(\)/g, ': Map<$1> = new Map()')
  ts = ts.replace(/:\s*Set<([^>]+)>\s*=\s*new\(\)/g, ': Set<$1> = new Set()')
  ts = ts.replace(/=\s*new\(\)/g, '= {}')

  // Fix double assignment from property { get; } = value patterns
  ts = ts.replace(/=\s*''\s*=\s*''/g, "= ''")
  ts = ts.replace(/=\s*\[\]\s*=\s*\[\]/g, '= []')
  // Fix File.Exists -> fs.existsSync (ensuring we don't match .some on arrays)
  ts = ts.replace(/File\.some\(/g, 'fs.existsSync(')
  ts = ts.replace(/Directory\.some\(/g, 'fs.existsSync(')
  // Fix remaining readonly in for-of patterns
  ts = ts.replace(/for\s*\(\s*readonly\s+(\w+)\s+of\b/g, 'for (const $1 of')
  // Remove trailing C# semicolons in converted lines
  ts = ts.replace(/;\s*;/g, ';')
  // Normalize spacing around =>
  ts = ts.replace(/\s*=>\s*/g, ' => ')
  return ts
}

function mapType(csType) {
  if (!csType) return 'any'
  csType = csType.trim()

  if (csType.endsWith('?')) {
    return mapType(csType.slice(0, -1)) + ' | null'
  }

  if (csType.endsWith('[]')) {
    return mapType(csType.slice(0, -2)) + '[]'
  }

  if (csType.includes('[') && csType.includes(']') && !csType.includes('<')) {
    return mapType(csType.replace(/\[.*?\]/g, '')) + '[]'
  }

  const genericMatch = csType.match(/^(\w+)<(.+)>$/)
  if (genericMatch) {
    const outer = genericMatch[1]
    const inner = genericMatch[2]

    if (outer === 'List' || outer === 'IList' || outer === 'ICollection' ||
      outer === 'IEnumerable' || outer === 'IReadOnlyList' || outer === 'IReadOnlyCollection') {
      return mapType(inner) + '[]'
    }
    if (outer === 'Dictionary' || outer === 'IDictionary' || outer === 'IReadOnlyDictionary') {
      const parts = inner.split(',').map(p => mapType(p.trim()))
      return `Map<${parts.join(', ')}>`
    }
    if (outer === 'HashSet' || outer === 'ISet' || outer === 'SortedSet') {
      return `Set<${mapType(inner)}>`
    }
    if (outer === 'Nullable') {
      return mapType(inner) + ' | null'
    }
    if (outer === 'Task' || outer === 'ValueTask') {
      return `Promise<${mapType(inner)}>`
    }
    if (outer === 'FlatBufferUnion') {
      return `FlatBufferUnion<${inner.split(',').map(p => mapType(p.trim())).join(', ')}>`
    }
    if (outer === 'Func') {
      const parts = inner.split(',').map(p => p.trim())
      if (parts.length === 1) {
        return `() => ${mapType(parts[0])}`
      }
      const args = parts.slice(0, -1).map(p => mapType(p)).join(', ')
      const ret = mapType(parts[parts.length - 1])
      return `(${args}) => ${ret}`
    }
    if (outer === 'Action') {
      if (!inner) return '() => void'
      const parts = inner.split(',').map(p => mapType(p.trim()))
      return `(${parts.join(', ')}) => void`
    }
    if (outer === 'Span' || outer === 'ReadOnlySpan') {
      return `${mapType(inner)}[]`
    }
    if (outer === 'Memory' || outer === 'ReadOnlyMemory') {
      return `${mapType(inner)}[]`
    }

    return `${outer}<${inner.split(',').map(p => mapType(p.trim())).join(', ')}>`
  }

  if (TYPE_MAP[csType]) return TYPE_MAP[csType]

  if (csType.match(/^[A-Z]\w*$/)) {
    return csType
  }

  return csType
}

function convertParams(params) {
  if (!params || !params.trim()) return ''
  return params.split(',').map(p => {
    p = p.trim()
    if (!p) return ''

    p = p.replace(/\b(ref|out|in|params)\s+/, '')

    const defMatch = p.match(/^([\w.<>\[\]?]+)\s+(\w+)\s*=\s*(.+)$/)
    if (defMatch) {
      const [, type, name, def] = defMatch
      return `${name}: ${mapType(type)} = ${convertValue(def.trim())}`
    }

    const parts = p.match(/^([\w.<>\[\]?]+)\s+(\w+)$/)
    if (parts) {
      return `${parts[2]}: ${mapType(parts[1])}`
    }
    return p
  }).join(', ')
}

function convertValue(value) {
  if (value === 'null') return 'null'
  if (value === 'true' || value === 'false') return value
  if (value === 'string.Empty') return "''"
  if (value === 'default') return 'undefined'

  value = value.replace(/(\d+(?:\.\d+)?)f$/i, '$1')
  value = value.replace(/(\d+)u$/i, '$1')
  value = value.replace(/(\d+)L$/i, '$1')
  value = value.replace(/(\d+)m$/i, '$1')
  value = value.replace(/(\d+)d$/i, '$1')

  value = value.replace(/new\s+List<(\w+)>\s*\(\)/, '[] as $1[]')
  value = value.replace(/new\s+Dictionary<(\w+),\s*(\w+)>\s*\(\)/, 'new Map<$1, $2>()')
  value = value.replace(/new\s+HashSet<(\w+)>\s*\(\)/, 'new Set<$1>()')
  value = value.replace(/Array\.Empty<(\w+)>\(\)/, '[] as $1[]')
  value = value.replace(/string\.Empty/g, "''")

  return value
}

function convertExpression(expr) {
  expr = convertValue(expr)
  return expr
}

function defaultFor(tsType) {
  if (!tsType) return null
  if (tsType === 'number') return '0'
  if (tsType === 'boolean') return 'false'
  if (tsType === 'string') return "''"
  if (tsType.endsWith('[]')) return '[]'
  if (tsType.includes('| null')) return 'null'
  if (tsType.startsWith('Map<')) return 'new Map()'
  if (tsType.startsWith('Set<')) return 'new Set()'
  return null
}

const args = process.argv.slice(2)

if (args[0] === '--dir') {
  const inputDir = args[1]
  const outputDir = args[2]

  if (!inputDir || !outputDir) {
    console.error('Usage: node cs-to-ts.mjs --dir <inputDir> <outputDir>')
    process.exit(1)
  }

  function walkDir(dir) {
    const entries = fs.readdirSync(dir, { withFileTypes: true })
    const files = []
    for (const entry of entries) {
      const full = path.join(dir, entry.name)
      if (entry.isDirectory()) {
        files.push(...walkDir(full))
      } else if (entry.name.endsWith('.cs')) {
        files.push(full)
      }
    }
    return files
  }

  const csFiles = walkDir(inputDir)
  let converted = 0

  for (const csFile of csFiles) {
    const relPath = path.relative(inputDir, csFile)
    const tsPath = path.join(outputDir, relPath.replace(/\.cs$/, '.ts'))

    const dir = path.dirname(tsPath)
    fs.mkdirSync(dir, { recursive: true })

    const csCode = fs.readFileSync(csFile, 'utf-8')
    const tsCode = convertCsharpToTypescript(csCode, relPath)
    fs.writeFileSync(tsPath, tsCode)

    console.log(`  ${relPath} → ${path.relative(outputDir, tsPath)}`)
    converted++
  }

  console.log(`\nConverted ${converted} files to ${outputDir}`)
} else if (args[0] === '--help' || args.length === 0) {
  console.log('C# to TypeScript converter for SwitchToolboxCli')
  console.log('')
  console.log('Usage:')
  console.log('  node cs-to-ts.mjs <input.cs> [output.ts]    Convert single file')
  console.log('  node cs-to-ts.mjs --dir <inputDir> <outDir> Convert directory tree')
} else {
  const inputFile = args[0]
  const outputFile = args[1] || inputFile.replace(/\.cs$/, '.ts')

  const csCode = fs.readFileSync(inputFile, 'utf-8')
  const tsCode = convertCsharpToTypescript(csCode, path.basename(inputFile))
  fs.writeFileSync(outputFile, tsCode)

  console.log(`Converted ${inputFile} → ${outputFile}`)
}
