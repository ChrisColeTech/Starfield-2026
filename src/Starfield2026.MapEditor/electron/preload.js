const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  browseFolder: (defaultPath) => ipcRenderer.invoke('browse-folder', defaultPath),
  openFile: (filters) => ipcRenderer.invoke('open-file', filters),
  saveFile: (defaultName, filters, content) => ipcRenderer.invoke('save-file', defaultName, filters, content),
  readFile: (filePath) => ipcRenderer.invoke('read-file', filePath),
  storeGet: (key) => ipcRenderer.invoke('store-get', key),
  storeSet: (key, value) => ipcRenderer.invoke('store-set', key, value),
  storeGetAll: () => ipcRenderer.invoke('store-get-all'),
})
