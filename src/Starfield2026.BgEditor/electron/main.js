const { app, BrowserWindow, ipcMain, dialog, Menu } = require('electron')
const path = require('path')
const fs = require('fs')
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
    lastActivePage: '/',
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
  mainWindow.webContents.openDevTools()

  // Forward renderer console to terminal
  mainWindow.webContents.on('console-message', (_e, _level, message) => {
    console.log(`[renderer] ${message}`)
  })
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

// Native file picker
ipcMain.handle('browse-file', async (_event, defaultPath, filters) => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile'],
    defaultPath: defaultPath || undefined,
    filters: filters || [{ name: 'JSON', extensions: ['json'] }],
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

// Screenshot capture
ipcMain.handle('capture-screenshot', async (_event, outputPath) => {
  if (!mainWindow) return { error: 'No window' }
  try {
    const image = await mainWindow.webContents.capturePage()
    const png = image.toPNG()
    const dir = path.dirname(outputPath)
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true })
    fs.writeFileSync(outputPath, png)
    return { ok: true, path: outputPath, size: png.length }
  } catch (err) {
    return { error: err.message }
  }
})

app.whenReady().then(() => {
  Menu.setApplicationMenu(null)
  createWindow()
})

app.on('window-all-closed', () => {
  app.quit()
})

// Kill frontend + backend when Electron exits (Windows)
app.on('will-quit', () => {
  try {
    require('child_process').execSync(
      'npx -y kill-port 5173 3001',
      { stdio: 'ignore', timeout: 5000 }
    )
  } catch (_) { }
})
