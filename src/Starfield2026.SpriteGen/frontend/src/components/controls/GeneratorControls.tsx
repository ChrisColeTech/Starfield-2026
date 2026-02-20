import { useState } from 'react';
import { Dices, Play, Save, Download } from 'lucide-react';
import { useStore } from '../../store';
import { SectionHeader } from '../common/SectionHeader';
import { api } from '../../services/apiClient';
import type { GeneratorType } from '../../types/generator';

const GENERATOR_OPTIONS: { value: GeneratorType; label: string }[] = [
  { value: 'grass', label: 'Grass (Multi-tile)' },
  { value: 'grass-single', label: 'Grass (Single)' },
  { value: 'flower', label: 'Flower' },
  { value: 'tree-green', label: 'Tree (Green)' },
  { value: 'tree-autumn', label: 'Tree (Autumn)' },
  { value: 'bush', label: 'Bush' },
];

const VARIANT_MAP: Record<string, { value: string; label: string; color: string }[]> = {
  flower: [
    { value: 'red', label: 'Red', color: '#e74c3c' },
    { value: 'blue', label: 'Blue', color: '#3498db' },
    { value: 'yellow', label: 'Yellow', color: '#f1c40f' },
    { value: 'pink', label: 'Pink', color: '#e91e8c' },
    { value: 'white', label: 'White', color: '#ecf0f1' },
  ],
  'tree-green': [
    { value: 'green', label: 'Green', color: '#2d8c3f' },
    { value: 'autumn', label: 'Autumn', color: '#e67e22' },
  ],
};

const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '3px 6px',
  background: '#3c3c3c',
  border: '1px solid #2d2d2d',
  borderRadius: 3,
  color: '#e0e0e0',
  fontSize: 12,
  outline: 'none',
};

const btnStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 4,
  padding: '4px 6px',
  background: '#3c3c3c',
  border: '1px solid #2d2d2d',
  borderRadius: 3,
  color: '#e0e0e0',
  fontSize: 11,
  cursor: 'pointer',
};

export function GeneratorControls() {
  const [expanded, setExpanded] = useState(true);

  const selectedType = useStore((s) => s.selectedType);
  const selectedVariant = useStore((s) => s.selectedVariant);
  const seed = useStore((s) => s.seed);
  const frameCount = useStore((s) => s.frameCount);
  const playbackSpeed = useStore((s) => s.playbackSpeed);
  const isLoading = useStore((s) => s.isLoading);
  const setType = useStore((s) => s.setType);
  const setVariant = useStore((s) => s.setVariant);
  const setSeed = useStore((s) => s.setSeed);
  const setFrameCount = useStore((s) => s.setFrameCount);
  const setPlaybackSpeed = useStore((s) => s.setPlaybackSpeed);
  const randomizeSeed = useStore((s) => s.randomizeSeed);
  const setFrames = useStore((s) => s.setFrames);
  const setLoading = useStore((s) => s.setLoading);
  const setError = useStore((s) => s.setError);
  const setGalleryItems = useStore((s) => s.setGalleryItems);

  const variants = VARIANT_MAP[selectedType];

  const handleGenerate = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.generate(selectedType, seed, selectedVariant ?? undefined, frameCount);
      setFrames(result.frames);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Generation failed');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    setLoading(true);
    setError(null);
    try {
      await api.save(selectedType, seed, selectedVariant ?? undefined);
      const gallery = await api.getGallery();
      setGalleryItems(gallery.items);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <SectionHeader label="Generate" expanded={expanded} onToggle={() => setExpanded(!expanded)} />
      {expanded && (
        <div className="p-[8px] flex flex-col gap-[6px]">
          {/* Type */}
          <label className="text-[10px] uppercase tracking-[0.5px] text-text-secondary">Type</label>
          <select
            value={selectedType}
            onChange={(e) => setType(e.target.value as GeneratorType)}
            style={inputStyle}
          >
            {GENERATOR_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>

          {/* Variant */}
          {variants && (
            <>
              <label className="text-[10px] uppercase tracking-[0.5px] text-text-secondary mt-[2px]">Variant</label>
              <div className="grid grid-cols-5 gap-[2px]">
                {variants.map((v) => (
                  <button
                    key={v.value}
                    onClick={() => setVariant(v.value)}
                    title={v.label}
                    className="cursor-pointer"
                    style={{
                      width: '100%',
                      aspectRatio: '1',
                      borderRadius: 3,
                      background: v.color,
                      border: selectedVariant === v.value ? '2px solid #fff' : '2px solid #2d2d2d',
                      opacity: selectedVariant === v.value ? 1 : 0.7,
                    }}
                  />
                ))}
              </div>
            </>
          )}

          {/* Seed */}
          <label className="text-[10px] uppercase tracking-[0.5px] text-text-secondary mt-[2px]">Seed</label>
          <div className="flex gap-[4px]">
            <input
              type="number"
              value={seed}
              onChange={(e) => setSeed(Number(e.target.value))}
              style={{ ...inputStyle, fontVariantNumeric: 'tabular-nums' }}
            />
            <button onClick={randomizeSeed} style={btnStyle} title="Randomize seed">
              <Dices size={13} />
            </button>
          </div>

          {/* Frames */}
          <label className="text-[10px] uppercase tracking-[0.5px] text-text-secondary mt-[2px]">Frames</label>
          <div className="flex items-center gap-[6px]">
            <input
              type="range"
              min={1}
              max={12}
              value={frameCount}
              onChange={(e) => setFrameCount(Number(e.target.value))}
              className="flex-1"
              style={{ accentColor: '#094771' }}
            />
            <span className="text-[11px] text-text tabular-nums" style={{ minWidth: 18, textAlign: 'right' }}>
              {frameCount}
            </span>
          </div>

          {/* Playback Speed */}
          <label className="text-[10px] uppercase tracking-[0.5px] text-text-secondary mt-[2px]">Speed</label>
          <div className="flex items-center gap-[6px]">
            <input
              type="range"
              min={50}
              max={500}
              step={50}
              value={playbackSpeed}
              onChange={(e) => setPlaybackSpeed(Number(e.target.value))}
              className="flex-1"
              style={{ accentColor: '#094771' }}
            />
            <span className="text-[11px] text-text tabular-nums" style={{ minWidth: 36, textAlign: 'right' }}>
              {playbackSpeed}ms
            </span>
          </div>

          {/* Actions */}
          <div className="flex flex-col gap-[4px] mt-[4px]">
            <button
              disabled={isLoading}
              onClick={handleGenerate}
              style={{
                ...btnStyle,
                background: '#094771',
                border: '1px solid #094771',
                width: '100%',
                padding: '6px',
                fontSize: 12,
                fontWeight: 600,
                opacity: isLoading ? 0.6 : 1,
              }}
            >
              <Play size={13} />
              {isLoading ? 'Generating...' : 'Generate Preview'}
            </button>
            <div className="flex gap-[4px]">
              <button onClick={handleSave} style={{ ...btnStyle, flex: 1 }}>
                <Save size={12} />
                Save
              </button>
              <button disabled style={{ ...btnStyle, flex: 1, opacity: 0.4 }}>
                <Download size={12} />
                Export
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
