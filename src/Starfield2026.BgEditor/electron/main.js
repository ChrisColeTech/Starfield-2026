const { app, BrowserWindow, ipcMain, dialog, Menu } = require('electron')
const path = require('path')
const Store = require('electron-store')

const FRONTEND_URL = 'http://localhost:5173'

const store = new Store({
  defaults: {
    manifestInputDir: '',
    manifestOutputDir: '',
    manifestSameAsInput: true,
    manifestOverwrite: true,
    manifestFormats: { fbx: true, dae: true, obj: true },
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
    title: 'BG Editor',
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
