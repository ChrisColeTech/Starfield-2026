import { useState, useEffect, useCallback } from 'react'

interface Toast {
    id: number
    message: string
}

let nextId = 0

export function ToastContainer() {
    const [toasts, setToasts] = useState<Toast[]>([])

    const addToast = useCallback((message: string) => {
        const id = nextId++
        setToasts(prev => [...prev, { id, message }])
        setTimeout(() => {
            setToasts(prev => prev.filter(t => t.id !== id))
        }, 5000)
    }, [])

    useEffect(() => {
        const handler = (e: Event) => {
            const detail = (e as CustomEvent).detail
            if (detail?.message) addToast(detail.message)
        }
        window.addEventListener('app:toast', handler)
        return () => window.removeEventListener('app:toast', handler)
    }, [addToast])

    if (toasts.length === 0) return null

    return (
        <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
            {toasts.map(t => (
                <div
                    key={t.id}
                    className="bg-card border border-border text-foreground px-4 py-3 rounded-lg shadow-lg text-sm max-w-sm"
                    style={{ animation: 'slideIn 0.3s ease-out' }}
                >
                    {t.message}
                </div>
            ))}
            <style>{`
        @keyframes slideIn {
          from { transform: translateX(100%); opacity: 0; }
          to { transform: translateX(0); opacity: 1; }
        }
      `}</style>
        </div>
    )
}
