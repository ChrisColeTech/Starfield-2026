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
            console.log(`[WS] Load model: modelType=${modelType} path=${event.path}`)
            const store = useEditorStore.getState()
            if (modelType === 'manifest') {
                store.loadManifestFromPath(event.path)
            } else if (modelType === 'folder') {
                store.loadFolder(event.path)
            } else if (modelType === 'dae') {
                const dir = event.path.replace(/[\\/][^\\/]+$/, '')
                store.loadManifestFromPath(dir + '/manifest.json')
            }
            showToast(`Loading model from ${event.path}`)
            break
        }

        case 'render:progress':
            console.log(`[WS] Render progress: ${event.angle} — ${event.status}`)
            break

        case 'render:complete':
            console.log(`[WS] Render complete: ${event.files?.length} files to ${event.outputDir}`)
            showToast(`Rendered ${event.files?.length || 0} angles to ${event.outputDir}`)
            break

        default:
            console.log(`[WS] Unknown event: ${event.type}`)
    }
}

// Simple toast — dispatches a custom DOM event picked up by ToastContainer
function showToast(message: string) {
    window.dispatchEvent(new CustomEvent('app:toast', { detail: { message } }))
}
