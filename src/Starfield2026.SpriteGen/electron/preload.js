const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
    browseFolder: (defaultPath) => ipcRenderer.invoke('browse-folder', defaultPath),
    storeGet: (key) => ipcRenderer.invoke('store-get', key),
    storeSet: (key, value) => ipcRenderer.invoke('store-set', key, value),
    storeGetAll: () => ipcRenderer.invoke('store-get-all'),
})
