export class PackedFile {
  Field_00: number = 0;
  EncryptionType: number = 0;
  Level: number = 0;
  FileSize: bigint = 0n;
  FileBuffer: Buffer = Buffer.alloc(0);
}

export class PackedArchive {
  FileHashes: bigint[] = [];
  FileEntry: PackedFile[] = [];
}
