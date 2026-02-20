import { ChevronDown, ChevronRight } from 'lucide-react';

interface SectionHeaderProps {
  label: string;
  expanded: boolean;
  onToggle: () => void;
  badge?: number | string;
}

export function SectionHeader({ label, expanded, onToggle, badge }: SectionHeaderProps) {
  return (
    <button
      onClick={onToggle}
      className="flex items-center w-full h-[22px] px-[8px] bg-surface border-b border-border text-[11px] font-bold uppercase tracking-[0.5px] text-text cursor-pointer select-none hover:bg-hover"
    >
      {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
      <span className="ml-[4px]">{label}</span>
      {badge !== undefined && (
        <span className="ml-auto text-[10px] font-normal text-text-secondary">{badge}</span>
      )}
    </button>
  );
}
