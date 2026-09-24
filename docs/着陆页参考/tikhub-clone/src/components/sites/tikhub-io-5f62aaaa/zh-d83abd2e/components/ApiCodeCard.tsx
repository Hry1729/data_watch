import type { CodeWindow, CodeLanguage } from "../../shared/types";

const TRANSFORMS = [
  "translateY(0px) scale(1)",
  "translateY(40px) scale(0.97)",
  "translateY(70px) scale(0.94)",
];

export function ApiCodeCard({
  window: w,
  language,
  offset,
  isTop,
}: {
  window: CodeWindow;
  language: CodeLanguage;
  offset: number;
  isTop: boolean;
}) {
  return (
    <div
      className="absolute inset-x-0 rounded-2xl overflow-hidden border border-white/10 bg-[#1E1E2E] text-[#cdd6f4] shadow-2xl transition-all duration-500"
      style={{
        transform: TRANSFORMS[offset] ?? TRANSFORMS[2],
        opacity: isTop ? 1 : 0.92,
        zIndex: 30 - offset,
      }}
    >
      {/* macOS dots + filename */}
      <div className="flex items-center gap-1.5 px-4 py-3 border-b border-white/5">
        <span className="h-3 w-3 rounded-full bg-[#f38ba8]" />
        <span className="h-3 w-3 rounded-full bg-[#f9e2af]" />
        <span className="h-3 w-3 rounded-full bg-[#a6e3a1]" />
        <span className="ml-4 text-xs text-[#9399b2] font-mono">
          {w.filename}
        </span>
        <span className="ml-auto text-xs text-[#9399b2] font-mono">
          {w.platform}
        </span>
      </div>
      {/* code body */}
      <pre className="p-5 font-mono text-xs leading-relaxed overflow-x-auto h-[400px]">
        <code>{w.samples[language]}</code>
      </pre>
    </div>
  );
}
