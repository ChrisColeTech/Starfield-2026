const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronAPI', {
    browseFolder: (title) => ipcRenderer.invoke('browse-folder', title),

    // Persistent store
    storeGet: (key) => ipcRenderer.invoke('store-get', key),
    storeGetAll: () => ipcRenderer.invoke('store-get-all'),
    storeSet: (key, value) => ipcRenderer.invoke('store-set', key, value),
    storeDelete: (key) => ipcRenderer.invoke('store-delete', key),
});
