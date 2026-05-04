import {
  Keyboard,
  Languages,
  Mic,
  Palette,
  SlidersHorizontal,
  Sparkles,
} from "lucide-react";

import { AppShell } from "@/components/layout/app-shell";
import { SectionCard } from "@/components/layout/section-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Slider } from "@/components/ui/slider";

export function SettingsPage() {
  return (
    <AppShell
      activePage="settings"
      title="Configuration"
      description="Manage your translation engines, visual overlays, and system preferences."
    >
      <div className="grid grid-cols-12 gap-6">
        <div className="col-span-12 lg:col-span-6">
          <SectionCard
            title="Speech-to-Text Engine"
            description="Select the processing core for audio transcription."
            icon={<Mic className="size-4 text-primary" />}
            className="border-border/80 bg-card/80 backdrop-blur-sm"
            contentClassName="space-y-4"
          >
            <RadioGroup defaultValue="vosk" className="space-y-3">
              <label className="flex cursor-pointer items-center justify-between rounded-lg border border-primary/40 bg-primary/10 p-4">
                <div className="flex items-center gap-3">
                  <RadioGroupItem value="vosk" id="stt-vosk" />
                  <div>
                    <p className="text-sm font-medium">Vosk (Offline)</p>
                    <p className="text-xs text-muted-foreground">
                      Zero latency, fully local processing.
                    </p>
                  </div>
                </div>
                <Badge>Active</Badge>
              </label>
              <label className="flex cursor-pointer items-center justify-between rounded-lg border border-border p-4">
                <div className="flex items-center gap-3">
                  <RadioGroupItem value="whisper" id="stt-whisper" />
                  <div>
                    <p className="text-sm font-medium">Whisper (Cloud)</p>
                    <p className="text-xs text-muted-foreground">
                      High accuracy, requires internet connection.
                    </p>
                  </div>
                </div>
              </label>
            </RadioGroup>
          </SectionCard>
        </div>

        <div className="col-span-12 lg:col-span-6">
          <SectionCard
            title="Translation Engine"
            description="Configure the neural network for real-time translation."
            icon={<Languages className="size-4 text-secondary" />}
            className="border-border/80 bg-card/80 backdrop-blur-sm"
            contentClassName="space-y-4"
          >
            <RadioGroup defaultValue="libre" className="space-y-3">
              <label className="flex cursor-pointer items-center justify-between rounded-lg border border-border p-4">
                <div className="flex items-center gap-3">
                  <RadioGroupItem value="argos" id="trans-argos" />
                  <div>
                    <p className="text-sm font-medium">Argos Translate</p>
                    <p className="text-xs text-muted-foreground">
                      Open-source offline translation based on OpenNMT.
                    </p>
                  </div>
                </div>
              </label>
              <label className="flex cursor-pointer items-center justify-between rounded-lg border border-secondary/40 bg-secondary/10 p-4">
                <div className="flex items-center gap-3">
                  <RadioGroupItem value="libre" id="trans-libre" />
                  <div>
                    <p className="text-sm font-medium">LibreTranslate</p>
                    <p className="text-xs text-muted-foreground">
                      Self-hosted API with robust language pair support.
                    </p>
                  </div>
                </div>
                <Badge variant="secondary">Active</Badge>
              </label>
            </RadioGroup>
          </SectionCard>
        </div>

        <div className="col-span-12">
          <SectionCard
            title="Overlay Customization"
            description="Adjust visual presentation of the live caption stream."
            icon={<Palette className="size-4 text-tertiary" />}
            className="border-border/80 bg-card/80 backdrop-blur-sm"
            contentClassName="grid grid-cols-1 gap-6 lg:grid-cols-2"
          >
            <div className="space-y-6">
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <Label>Typography Scale</Label>
                  <span className="text-xs text-muted-foreground">24px</span>
                </div>
                <Slider defaultValue={[24]} min={12} max={48} step={1} />
              </div>

              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <Label>Backdrop Opacity</Label>
                  <span className="text-xs text-muted-foreground">60%</span>
                </div>
                <Slider defaultValue={[60]} min={0} max={100} step={1} />
              </div>

              <div className="space-y-3">
                <Label>Text Colors</Label>
                <div className="flex gap-3">
                  <button className="size-8 rounded-md border border-border bg-white" />
                  <button className="size-8 rounded-md border border-secondary bg-tertiary" />
                  <button className="size-8 rounded-md border border-border bg-background" />
                </div>
              </div>
            </div>

            <div className="relative flex min-h-56 items-end overflow-hidden rounded-lg border border-border bg-input p-4">
              <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,rgba(59,130,246,0.15),transparent_60%)]" />
              <div className="relative z-10 w-full space-y-2 rounded-lg border border-border/60 bg-background/70 p-4 backdrop-blur-sm">
                <p className="text-base text-foreground">
                  This is the original transcribed text stream.
                </p>
                <p className="text-lg text-tertiary">
                  Esto es el flujo de texto traducido en vivo.
                </p>
                <p className="pt-2 text-center text-xs uppercase tracking-wider text-muted-foreground">
                  Live Preview
                </p>
              </div>
            </div>
          </SectionCard>
        </div>

        <div className="col-span-12">
          <SectionCard
            title="Global Shortcuts"
            description="Manage system-wide hotkeys for rapid control."
            icon={<Keyboard className="size-4 text-foreground" />}
            className="border-border/80 bg-card/80 backdrop-blur-sm"
            contentClassName="space-y-3"
          >
            {[
              ["Toggle Recording Stream", "Ctrl + Shift + R"],
              ["Toggle Translation Display", "Alt + T"],
              ["Clear Current Subtitles", "Esc"],
              ["Open Settings Menu", "Ctrl + ,"],
            ].map(([action, shortcut]) => (
              <div
                key={action}
                className="flex items-center justify-between rounded-lg border border-border/70 bg-input p-3"
              >
                <span className="text-sm">{action}</span>
                <kbd className="rounded border border-border bg-card px-2 py-1 font-mono text-xs">
                  {shortcut}
                </kbd>
              </div>
            ))}
          </SectionCard>
        </div>
      </div>

      <div className="mt-8 flex justify-end gap-3 border-t border-border/60 pt-6">
        <Button variant="outline">Revert Defaults</Button>
        <Button>
          <SlidersHorizontal className="mr-2 size-4" />
          Save Configuration
        </Button>
        <Button variant="secondary">
          <Sparkles className="mr-2 size-4" />
          Apply Preset
        </Button>
      </div>
    </AppShell>
  );
}
