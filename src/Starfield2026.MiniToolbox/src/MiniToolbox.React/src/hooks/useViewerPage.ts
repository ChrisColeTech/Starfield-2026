import { useEffect, useMemo, useState, useCallback } from 'react';
import { useStore } from '../store/exportStore';
import { api } from '../services/apiClient';
import type { ModelInfo, RenderSettings } from '../types';

export function useViewerPage() {
    // ── Store state ──
    const viewerFolder = useStore((s) => s.viewerFolder);
    const selectedClip = useStore((s) => s.selectedClip);
    const setSelectedClip = useStore((s) => s.setSelectedClip);
    const clipList = useStore((s) => s.clipList);
    const setClipList = useStore((s) => s.setClipList);
    const clipPanelOpen = useStore((s) => s.clipPanelOpen);
    const toggleClipPanel = useStore((s) => s.toggleClipPanel);
    const propsPanelOpen = useStore((s) => s.propsPanelOpen);
    const togglePropsPanel = useStore((s) => s.togglePropsPanel);

    // ── Render settings ──
    const [showWireframe, setShowWireframe] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(true);
    const [showGrid, setShowGrid] = useState(true);
    const [showTextures, setShowTextures] = useState(true);
    const [lightIntensity, setLightIntensity] = useState(1.2);

    const renderSettings: RenderSettings = {
        showWireframe,
        showSkeleton,
        showGrid,
        showTextures,
        lightIntensity,
    };

    // ── Model info ──
    const [modelInfo, setModelInfo] = useState<ModelInfo | null>(null);

    const handleModelLoaded = useCallback((info: ModelInfo) => {
        setModelInfo(info);
    }, []);

    // ── Effects ──

    // Load clip list when folder changes
    useEffect(() => {
        if (!viewerFolder) return;
        api.listDaeFiles(viewerFolder).then(setClipList).catch(() => setClipList([]));
    }, [viewerFolder, setClipList]);

    // Clear model info when clip changes
    useEffect(() => {
        setModelInfo(null);
    }, [selectedClip]);

    // ── Derived state ──
    const sortedClips = useMemo(() => [...clipList].sort(), [clipList]);

    // ── Handlers ──
    const refreshClips = useCallback(() => {
        if (viewerFolder) {
            api.listDaeFiles(viewerFolder).then(setClipList);
        }
    }, [viewerFolder, setClipList]);

    return {
        // Store
        viewerFolder,
        selectedClip,
        setSelectedClip,
        clipList,
        clipPanelOpen,
        toggleClipPanel,
        propsPanelOpen,
        togglePropsPanel,

        // Render
        renderSettings,
        setShowWireframe,
        setShowSkeleton,
        setShowGrid,
        setShowTextures,
        lightIntensity,
        setLightIntensity,

        // Model
        modelInfo,
        handleModelLoaded,

        // Derived
        sortedClips,

        // Handlers
        refreshClips,
    };
}
