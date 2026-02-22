export class FileInfo {
  PackIndex: bigint = 0n;
  UnusedTable: number = 0;
}

export class PackInfo {
  FileSize: bigint = 0n;
  FileCount: bigint = 0n;
}

export class FileDescriptor {
  FileHashes: bigint[] = [];
  PackNames: string[] = [];
  FileInfo: FileInfo[] = [];
  PackInfo: PackInfo[] = [];
}

export class CustomFileDescriptor {
  FileHashes: bigint[] = [];
  PackNames: string[] = [];
  FileInfo: FileInfo[] = [];
  PackInfo: PackInfo[] = [];
  UnusedHashes: bigint[] = [];
  UnusedFileInfo: FileInfo[] = [];
}
