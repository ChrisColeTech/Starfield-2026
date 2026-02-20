interface ElectronAPI {
  browseFolder: (defaultPath?: string) => Promise<string | null>
  storeGet: (key: string) => Promise<unknown>
  storeSet: (key: string, value: unknown) => Promise<void>
  storeGetAll: () => Promise<Record<string, unknown>>
}

interface Window {
  electronAPI: ElectronAPI
}
