interface FileFilter {
  name: string
  extensions: string[]
}

interface OpenFileResult {
  filePath: string
  content: string
}

interface ElectronAPI {
  browseFolder: (defaultPath?: string) => Promise<string | null>
  openFile: (filters?: FileFilter[]) => Promise<OpenFileResult | null>
  saveFile: (defaultName: string, filters: FileFilter[], content: string) => Promise<string | null>
  readFile: (filePath: string) => Promise<string | null>
  storeGet: (key: string) => Promise<unknown>
  storeSet: (key: string, value: unknown) => Promise<void>
  storeGetAll: () => Promise<Record<string, unknown>>
}

interface Window {
  electronAPI: ElectronAPI
}
