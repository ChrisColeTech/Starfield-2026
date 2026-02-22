const { app, BrowserWindow, ipcMain, dialog, Menu } = require('electron')
const path = require('path')
const fs = require('fs')
const Store = require('electron-store')

const FRONTEND_URL = 'http://localhost:5173'

const store = new Store({
  defaults: {
    windowBounds: { width: 1400, height: 900 },
  },
})

let mainWindow

function createWindow() {
  const { width, height, x, y } = store.get('windowBounds')

  mainWindow = new BrowserWindow({
    width,
    height,
    x,
    y,
    title: 'Map Editor',
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  mainWindow.on('close', () => {
    store.set('windowBounds', mainWindow.getBounds())
  })

  mainWindow.loadURL(FRONTEND_URL)
}

// Native folder picker
ipcMain.handle('browse-folder', async (_event, defaultPath) => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openDirectory'],
    defaultPath: defaultPath || undefined,
  })
  if (result.canceled) return null
  return result.filePaths[0].replace(/\\/g, '/')
})

// Native file open
ipcMain.handle('open-file', async (_event, filters) => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile'],
    filters: filters || [],
  })
  if (result.canceled) return null
  const filePath = result.filePaths[0]
  const content = fs.readFileSync(filePath, 'utf-8')
  return { filePath: filePath.replace(/\\/g, '/'), content }
})

// Native file save
ipcMain.handle('save-file', async (_event, defaultName, filters, content) => {
  const result = await dialog.showSaveDialog(mainWindow, {
    defaultPath: defaultName,
    filters: filters || [],
  })
  if (result.canceled) return null
  fs.writeFileSync(result.filePath, content, 'utf-8')
  return result.filePath.replace(/\\/g, '/')
})

// Read a file by absolute path
ipcMain.handle('read-file', async (_event, filePath) => {
  try {
    const content = fs.readFileSync(filePath, 'utf-8')
    return content
  } catch {
    return null
  }
})

// Persistent settings
ipcMain.handle('store-get', (_event, key) => {
  return store.get(key)
})

ipcMain.handle('store-set', (_event, key, value) => {
  store.set(key, value)
})

ipcMain.handle('store-get-all', () => {
  return store.store
})

app.whenReady().then(() => {
  Menu.setApplicationMenu(null)
  createWindow()
})

app.on('window-all-closed', () => {
  app.quit()
})
