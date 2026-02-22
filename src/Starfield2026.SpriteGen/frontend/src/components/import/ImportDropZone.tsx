import { useState, useRef, useCallback } from 'react';
import { Upload, Trash2, Save, X } from 'lucide-react';
import { useStore } from '../../store';
import { SectionHeader } from '../common/SectionHeader';
import { api } from '../../services/apiClient';

export function ImportDropZone() {
  const [expanded, setExpanded] = useState(true);
  const [dragOver, setDragOver] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const importFiles = useStore((s) => s.importFiles);
  const importBaseName = useStore((s) => s.importBaseName);
  const addImportFiles = useStore((s) => s.addImportFiles);
  const removeImportFile = useStore((s) => s.removeImportFile);
  const clearImportFiles = useStore((s) => s.clearImportFiles);
  const setImportBaseName = useStore((s) => s.setImportBaseName);
  const isLoading = useStore((s) => s.isLoading);
  const setLoading = useStore((s) => s.setLoading);
  const setError = useStore((s) => s.setError);
  const setGalleryItems = useStore((s) => s.setGalleryItems);

  const handleSaveFrames = async () => {
    setLoading(true);
    setError(null);
    try {
      const frameData = await Promise.all(
        importFiles.map(async (f, i) => ({
          index: i,
          content: await f.file.text(),
          filename: f.file.name,
        }))
      );
      await api.importFrames(importBaseName, frameData);
      clearImportFiles();
      const gallery = await api.getGallery();
      setGalleryItems(gallery.items);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Import failed');
    } finally {
      setLoading(false);
    }
  };

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragOver(false);
      const imageFiles = Array.from(e.dataTransfer.files).filter((f) =>
        f.type.startsWith('image/')
      );
      if (imageFiles.length > 0) addImportFiles(imageFiles);
    },
    [addImportFiles]
  );

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      addImportFiles(Array.from(e.target.files));
      e.target.value = '';
    }
  };

  return (
    <>
      <input
        ref={fileInputRef}
        type="file"
        accept="image/svg+xml,image/png,image/jpeg,image/webp"
        multiple
        className="hidden"
        onChange={handleFileSelect}
      />

      <SectionHeader
        label="Import Frames"
        expanded={expanded}
        onToggle={() => setExpanded(!expanded)}
        badge={importFiles.length || undefined}
      />

      {expanded && (
        <div className="p-[8px] flex flex-col gap-[6px]">
          {/* Drop zone */}
          <div
            onDrop={handleDrop}
            onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
            onDragLeave={() => setDragOver(false)}
            onClick={() => fileInputRef.current?.click()}
            className="flex flex-col items-center gap-[4px] py-[12px] rounded-[3px] cursor-pointer transition-colors"
            style={{
              border: dragOver ? '1px dashed var(--color-accent)' : '1px dashed var(--color-border)',
              background: dragOver ? 'rgba(86,156,214,0.08)' : 'transparent',
            }}
          >
            <Upload size={16} className="text-text-disabled" />
            <span className="text-[10px] text-text-secondary">
              Drop images or click to browse
            </span>
          </div>

          {/* File list */}
          {importFiles.length > 0 && (
            <>
              {/* Base name */}
              <label className="text-[10px] uppercase tracking-[0.5px] text-text-secondary mt-[2px]">
                Base Name
              </label>
              <input
                type="text"
                value={importBaseName}
                onChange={(e) => setImportBaseName(e.target.value)}
                placeholder="tile_flower_yellow"
                className="w-full px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[12px] outline-none"
              />

              {/* Add more */}
              <button
                onClick={() => fileInputRef.current?.click()}
                className="flex items-center justify-center gap-[4px] w-full px-[6px] py-[4px] bg-input border border-border rounded-[3px] text-text text-[11px] cursor-pointer hover:bg-hover"
              >
                <Upload size={12} />
                Add Files...
              </button>

              {/* Queued files */}
              <div className="flex flex-col gap-[1px] mt-[2px]">
                {importFiles.map((f, i) => (
                  <div
                    key={i}
                    className="flex items-center gap-[4px] px-[4px] py-[2px] rounded-[2px] hover:bg-hover group"
                  >
                    <img
                      src={f.preview}
                      alt=""
                      className="sprite-render w-[18px] h-[18px] rounded-[2px]"
                      style={{ background: '#0f0f23' }}
                    />
                    <span className="flex-1 text-[10px] text-text truncate">
                      {importBaseName}_{i}.svg
                    </span>
                    <button
                      onClick={() => removeImportFile(i)}
                      className="text-text-disabled hover:text-danger cursor-pointer opacity-0 group-hover:opacity-100"
                    >
                      <X size={11} />
                    </button>
                  </div>
                ))}
              </div>

              {/* Actions */}
              <div className="flex gap-[4px] mt-[2px]">
                <button
                  disabled={isLoading}
                  onClick={handleSaveFrames}
                  className="flex-1 flex items-center justify-center gap-[4px] px-[6px] py-[4px] rounded-[3px] text-text text-[11px] font-semibold cursor-pointer border-none disabled:opacity-60 disabled:cursor-default"
                  style={{ background: 'var(--color-active)' }}
                >
                  <Save size={12} />
                  Save Frames
                </button>
                <button
                  onClick={clearImportFiles}
                  title="Clear all"
                  className="flex items-center justify-center gap-[4px] px-[6px] py-[4px] bg-input border border-border rounded-[3px] text-danger text-[11px] cursor-pointer hover:bg-hover"
                >
                  <Trash2 size={12} />
                </button>
              </div>
            </>
          )}
        </div>
      )}
    </>
  );
}
