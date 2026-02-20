/**
 * Seeded pseudo-random number generator (mulberry32).
 * Deterministic: same seed always produces the same sequence.
 */
export class SeededRandom {
  private state: number;

  constructor(seed: number) {
    this.state = seed;
  }

  /** Returns a float in [0, 1) */
  next(): number {
    this.state |= 0;
    this.state = (this.state + 0x6d2b79f5) | 0;
    let t = Math.imul(this.state ^ (this.state >>> 15), 1 | this.state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }

  /** Returns an integer in [min, max] inclusive */
  int(min: number, max: number): number {
    return Math.floor(this.next() * (max - min + 1)) + min;
  }

  /** Returns a random element from an array */
  pick<T>(array: T[]): T {
    return array[this.int(0, array.length - 1)];
  }

  /** Returns a float in [min, max) */
  float(min: number, max: number): number {
    return this.next() * (max - min) + min;
  }
}
