import { useEffect, useRef } from 'react'
import { useEditorStore } from '../store/editorStore'

const WS_URL = 'ws://localhost:3001/ws'
const RECONNECT_DELAY = 3000

/**
 * Connects to the backend WebSocket and dispatches events to the store.
 * Automatically reconnects on disconnect.
 */
export function useBackendEvents() {
    const wsRef = useRef<WebSocket | null>(null)

    useEffect(() => {
        let alive = true

        function connect() {
            if (!alive) return
            const ws = new WebSocket(WS_URL)
            wsRef.current = ws

            ws.onopen = () => {
                console.log('[WS] Connected to backend')
            }

            ws.onmessage = (e) => {
                try {
                    const event = JSON.parse(e.data)
                    handleEvent(event)
                } catch {
                    console.warn('[WS] Failed to parse message:', e.data)
                }
            }

            ws.onclose = () => {
                console.log('[WS] Disconnected, reconnecting...')
                wsRef.current = null
                if (alive) setTimeout(connect, RECONNECT_DELAY)
            }

            ws.onerror = () => {
                ws.close()
            }
        }

        connect()

        return () => {
            alive = false
            wsRef.current?.close()
        }
    }, [])
}

function handleEvent(event: any) {
    switch (event.type) {
        case 'model:load': {
            const modelType = event.modelType || 'manifest'
            console.log(`[WS] Load model: modelType=${modelType} dir=${event.dir}`)
            const store = useEditorStore.getState()

            if (event.manifests && Array.isArray(event.manifests)) {
                // Folder or single load — populate model browser + auto-load first
                store.loadManifestList(event.manifests, event.dir)
            } else if (event.manifest) {
                // Legacy fallback
                store.loadManifestData(event.manifest)
            }
            const count = event.manifests?.length || 1
            showToast(`Loaded ${count} model(s) from ${event.dir}`)
            break
        }

        case 'model:compare': {
            console.log(`[WS] Compare models: ${event.manifests?.length} models`)
            const store = useEditorStore.getState()
            if (event.manifests && Array.isArray(event.manifests)) {
                store.loadManifestList(event.manifests)
            }
            showToast(`Comparing ${event.manifests?.length || 0} models`)
            break
        }

        case 'render:progress':
            console.log(`[WS] Render progress: ${event.angle} — ${event.status}`)
            break

        case 'render:complete':
            console.log(`[WS] Render complete: ${event.files?.length} files to ${event.outputDir}`)
            showToast(`Rendered ${event.files?.length || 0} angles to ${event.outputDir}`)
            break

        case 'screenshot:capture': {
            console.log(`[WS] Screenshot request: ${event.outputPath}`)
            const api = (window as any).electronAPI
            if (api?.captureScreenshot) {
                api.captureScreenshot(event.outputPath).then((result: any) => {
                    fetch('http://localhost:3001/api/screenshot/result', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ requestId: event.requestId, ...result }),
                    }).catch(() => { })
                })
            } else {
                fetch('http://localhost:3001/api/screenshot/result', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ requestId: event.requestId, error: 'Not running in Electron' }),
                }).catch(() => { })
            }
            break
        }

        default:
            console.log(`[WS] Unknown event: ${event.type}`)
    }
}

// Simple toast — dispatches a custom DOM event picked up by ToastContainer
function showToast(message: string) {
    window.dispatchEvent(new CustomEvent('app:toast', { detail: { message } }))
}
