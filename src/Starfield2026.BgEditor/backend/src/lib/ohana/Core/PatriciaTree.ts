/**
 * Patricia Tree (radix tree) implementation ported from OhanaCli.Formats.PatriciaTree (C#).
 * Used for efficient name-based lookups in 3DS resource containers.
 */

export class PatriciaTreeNode {
  index: number = 0;
  referenceBit: number = 0;
  name: string = '';
  left: PatriciaTreeNode = null!;
  right: PatriciaTreeNode = null!;
}

export class PatriciaTree {
  nodeCount: number = 0;
  maxLength: number = 0;
  nodes: PatriciaTreeNode[] = [];
  rootNode: PatriciaTreeNode = new PatriciaTreeNode();

  constructor(keys: string[]) {
    this.rootNode.left = this.rootNode;
    this.rootNode.right = this.rootNode;
    this.rootNode.referenceBit = -1;

    for (const key of keys) {
      if (key.length > this.maxLength) this.maxLength = key.length;
    }
    for (const key of keys) {
      this.nodes.push(this.insert(key));
    }
  }

  private insert(key: string): PatriciaTreeNode {
    let rootNode = this.rootNode;
    let leftNode = rootNode.left;
    let bit = (this.maxLength << 3) - 1;

    while (rootNode.referenceBit > leftNode.referenceBit) {
      rootNode = leftNode;
      if (this.getBit(key, leftNode.referenceBit))
        leftNode = leftNode.right;
      else
        leftNode = leftNode.left;
    }

    while (this.getBit(leftNode.name, bit) === this.getBit(key, bit)) bit--;

    rootNode = this.rootNode;
    leftNode = rootNode.left;

    while (
      rootNode.referenceBit > leftNode.referenceBit &&
      leftNode.referenceBit > bit
    ) {
      rootNode = leftNode;
      if (this.getBit(key, leftNode.referenceBit))
        leftNode = leftNode.right;
      else
        leftNode = leftNode.left;
    }

    const output = new PatriciaTreeNode();
    output.name = key;
    output.referenceBit = bit;

    if (this.getBit(key, bit)) {
      output.left = leftNode;
      output.right = output;
    } else {
      output.left = output;
      output.right = leftNode;
    }

    output.index = ++this.nodeCount;
    return output;
  }

  private getBit(name: string, bit: number): boolean {
    const position = bit >> 3;
    const charBit = bit & 7;
    if (name == null || position >= name.length) return false;
    return ((name.charCodeAt(position) >> charBit) & 1) > 0;
  }
}
