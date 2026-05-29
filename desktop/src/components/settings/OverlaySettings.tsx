import { Input } from "@/components/ui/input";
import { Slider } from "@/components/ui/slider";
import { Switch } from "@/components/ui/switch";
import { Button } from "@/components/ui/button";
import { SettingsSection } from "./SettingsSection";
import { SettingsField } from "./SettingsField";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";
import type { AppSettings } from "@/types/electron-api";

const LINE_SPACING_OPTIONS = [
  { label: "Compact", value: 1.15 },
  { label: "Default", value: 1.35 },
  { label: "Wide", value: 1.6 },
];

interface Props {
  settings: AppSettings;
  onUpdate: (partial: Partial<AppSettings>) => void;
}

export function OverlaySettingsPanel({ settings, onUpdate }: Props) {
  const ov = settings.overlay;

  const updateOverlay = (partial: Partial<typeof ov>) => {
    onUpdate({ overlay: { ...ov, ...partial } });
  };

  const isDark = ov.theme === "Dark";
  const previewBg = isDark
    ? `rgba(14, 19, 28, ${ov.opacity})`
    : `rgba(245, 247, 250, ${ov.opacity})`;
  const previewText = isDark ? "text-white" : "text-gray-900";
  const previewMuted = isDark ? "text-white/60" : "text-gray-500";

  return (
    <div className="space-y-6">
      <SettingsSection title="Appearance">
        <SettingsField label="Theme">
          <Select value={ov.theme} onValueChange={(v) => updateOverlay({ theme: v as "Dark" | "Light" })}>
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Dark">Dark</SelectItem>
              <SelectItem value="Light">Light</SelectItem>
            </SelectContent>
          </Select>
        </SettingsField>

        <SettingsField label={`Font size — ${ov.fontSize}px`}>
          <Slider
            value={[ov.fontSize]}
            onValueChange={([v]) => updateOverlay({ fontSize: v })}
            min={14}
            max={48}
            step={1}
          />
        </SettingsField>

        <SettingsField label="Line spacing">
          <div className="flex gap-1">
            {LINE_SPACING_OPTIONS.map((opt) => (
              <Button
                key={opt.label}
                variant={ov.lineHeight === opt.value ? "default" : "outline"}
                size="sm"
                className={cn("flex-1")}
                onClick={() => updateOverlay({ lineHeight: opt.value })}
              >
                {opt.label}
              </Button>
            ))}
          </div>
        </SettingsField>

        <SettingsField label={`Background opacity — ${Math.round(ov.opacity * 100)}%`}>
          <Slider
            value={[ov.opacity]}
            onValueChange={([v]) => updateOverlay({ opacity: v })}
            min={0.3}
            max={1}
            step={0.02}
          />
        </SettingsField>

        <SettingsField label="Show translation" helper="Display translated text below each transcript line" horizontal>
          <Switch checked={ov.showTranslation} onCheckedChange={(v) => updateOverlay({ showTranslation: v })} />
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Size">
        <div className="flex gap-4">
          <SettingsField label="Width" className="flex-1">
            <div className="flex items-center gap-2">
              <Input
                type="number"
                value={ov.width}
                onChange={(e) => updateOverlay({ width: parseInt(e.target.value) || 720 })}
                className="w-24"
              />
              <span className="text-xs text-muted-foreground">px</span>
            </div>
          </SettingsField>
          <SettingsField label="Height" className="flex-1">
            <div className="flex items-center gap-2">
              <Input
                type="number"
                value={ov.height}
                onChange={(e) => updateOverlay({ height: parseInt(e.target.value) || 200 })}
                className="w-24"
              />
              <span className="text-xs text-muted-foreground">px</span>
            </div>
          </SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title="Preview">
        <div
          className="rounded-lg p-4 min-h-[120px]"
          style={{ backgroundColor: previewBg }}
        >
          <p className={cn(previewText, "font-medium")} style={{ fontSize: ov.fontSize, lineHeight: ov.lineHeight }}>
            Hello, welcome to the presentation.
          </p>
          {ov.showTranslation && (
            <p className={cn(previewMuted, "mt-0.5")} style={{ fontSize: Math.max(14, ov.fontSize - 4), lineHeight: ov.lineHeight }}>
              Xin chào, chào mừng đến với bài thuyết trình.
            </p>
          )}
          <div className="mt-3">
            <p className={cn(previewText, "font-medium")} style={{ fontSize: ov.fontSize, lineHeight: ov.lineHeight }}>
              We&apos;ll discuss the new architecture.
            </p>
            {ov.showTranslation && (
              <p className={cn(previewMuted, "mt-0.5")} style={{ fontSize: Math.max(14, ov.fontSize - 4), lineHeight: ov.lineHeight }}>
                Chúng ta sẽ thảo luận kiến trúc mới.
              </p>
            )}
          </div>
        </div>
      </SettingsSection>
    </div>
  );
}
