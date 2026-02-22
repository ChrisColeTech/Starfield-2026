import { useNavigate } from 'react-router-dom';
import { useStore } from '../store/exportStore';
import type { MenuDefinition } from '../types';

export function useHeaderMenu() {
    const navigate = useNavigate();
    const setViewerFolder = useStore((s) => s.setViewerFolder);

    // ── Handlers ──

    const handleOpenFolder = () => {
        const electronAPI = (window as any).electronAPI;
        if (electronAPI?.browseFolder) {
            electronAPI.browseFolder('Select DAE Folder').then((path: string | null) => {
                if (path) {
                    setViewerFolder(path);
                    navigate('/');
                }
            });
        } else {
            const path = prompt('Enter path to folder containing .dae files:');
            if (path) {
                setViewerFolder(path);
                navigate('/');
            }
        }
    };

    const handleOpenFile = () => {
        const electronAPI = (window as any).electronAPI;
        if (electronAPI?.browseFile) {
            electronAPI.browseFile('Open DAE File', [{ name: 'COLLADA', extensions: ['dae'] }]).then((filePath: string | null) => {
                if (filePath) {
                    const sep = filePath.includes('/') ? '/' : '\\';
                    const lastSep = filePath.lastIndexOf(sep);
                    const folder = filePath.substring(0, lastSep);
                    const file = filePath.substring(lastSep + 1);
                    setViewerFolder(folder);
                    useStore.getState().setSelectedClip(file);
                    navigate('/');
                }
            });
        } else {
            const filePath = prompt('Enter full path to a .dae file:');
            if (filePath) {
                const sep = filePath.includes('/') ? '/' : '\\';
                const lastSep = filePath.lastIndexOf(sep);
                const folder = filePath.substring(0, lastSep);
                const file = filePath.substring(lastSep + 1);
                setViewerFolder(folder);
                useStore.getState().setSelectedClip(file);
                navigate('/');
            }
        }
    };

    const handleOpenArchive = () => {
        const electronAPI = (window as any).electronAPI;
        if (electronAPI?.browseFile) {
            electronAPI.browseFile('Open Archive', [{ name: 'Archives', extensions: ['trpfs', 'trpfd'] }]).then((filePath: string | null) => {
                if (filePath) {
                    useStore.getState().setArcPath(filePath);
                    navigate('/export');
                }
            });
        } else {
            const filePath = prompt('Enter path to archive file (.trpfs/.trpfd):');
            if (filePath) {
                useStore.getState().setArcPath(filePath);
                navigate('/export');
            }
        }
    };

    const handleOpenOutputFolder = () => {
        const electronAPI = (window as any).electronAPI;
        if (electronAPI?.browseFolder) {
            electronAPI.browseFolder('Select Output Folder').then((path: string | null) => {
                if (path) useStore.getState().setOutputDir(path);
            });
        }
    };

    // ── Menu definitions ──

    const menus: MenuDefinition[] = [
        {
            label: 'File',
            items: [
                {
                    label: 'Open File (dae, obj, fbx)...',
                    shortcut: 'Ctrl+O',
                    onClick: handleOpenFile,
                },
                {
                    label: 'Open Folder...',
                    shortcut: 'Ctrl+Shift+O',
                    onClick: handleOpenFolder,
                },
                { separator: true, label: '' },
                {
                    label: 'Model Viewer',
                    shortcut: 'Ctrl+1',
                    onClick: () => navigate('/'),
                },
                {
                    label: 'Batch Export',
                    shortcut: 'Ctrl+2',
                    onClick: () => navigate('/export'),
                },
                { separator: true, label: '' },
                { label: 'Preferences', disabled: true },
            ],
        },
        {
            label: 'Viewer',
            items: [
                { label: 'Play / Pause', shortcut: 'Space', disabled: true },
                { label: 'Stop', disabled: true },
                { label: 'Previous Frame', shortcut: ',', disabled: true },
                { label: 'Next Frame', shortcut: '.', disabled: true },
                { separator: true, label: '' },
                { label: 'Reset Camera', disabled: true },
                { separator: true, label: '' },
                { label: 'Import Model...', disabled: true },
                { label: 'Export Model...', disabled: true },
            ],
        },
        {
            label: 'Archive',
            items: [
                {
                    label: 'Open Archive (trpfs, trpfd)...',
                    onClick: handleOpenArchive,
                },
                {
                    label: 'Set Output Directory...',
                    onClick: handleOpenOutputFolder,
                },
            ],
        },
        {
            label: 'Help',
            items: [
                { label: 'About SwitchToolbox', disabled: true },
            ],
        },
    ];

    return { menus };
}
