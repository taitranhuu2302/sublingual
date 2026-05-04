import { Eye, LayoutTemplate, Monitor, Type } from "lucide-react";

import { AppShell } from "@/components/layout/app-shell";
import { SectionCard } from "@/components/layout/section-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Slider } from "@/components/ui/slider";

export function CaptionsPage() {
  return (
    <AppShell
      activePage="captions"
      title="Captions Overlay"
      description="Configure and preview live subtitle overlays."
    >
      <div className="grid grid-cols-12 gap-6">
        <div className="col-span-12 lg:col-span-5">
          <SectionCard
            title="Overlay Controls"
            description="Tune readability and display behavior."
            icon={<LayoutTemplate className="size-4 text-primary" />}
            className="border-border/80 bg-card/80 backdrop-blur-sm"
            contentClassName="space-y-6"
          >
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label>Font Size</Label>
                <Badge variant="outline">24px</Badge>
              </div>
              <Slider defaultValue={[24]} min={12} max={48} step={1} />
            </div>
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label>Background Opacity</Label>
                <Badge variant="outline">60%</Badge>
              </div>
              <Slider defaultValue={[60]} min={0} max={100} step={1} />
            </div>
            <div className="space-y-2">
              <Label>Line Spacing</Label>
              <Slider defaultValue={[140]} min={100} max={200} step={5} />
            </div>
            <Button className="w-full">
              <Type className="mr-2 size-4" />
              Apply Overlay Preset
            </Button>
          </SectionCard>
        </div>

        <div className="col-span-12 lg:col-span-7">
          <SectionCard
            title="Live Overlay Preview"
            description="Preview subtitle styling before going live."
            icon={<Eye className="size-4 text-tertiary" />}
            className="border-border/80 bg-card/80 backdrop-blur-sm"
          >
            <div className="relative flex min-h-80 items-end overflow-hidden rounded-xl border border-border/70 bg-input p-6">
              <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,rgba(76,215,246,0.18),transparent_62%)]" />
              <div className="relative z-10 w-full rounded-lg border border-border/70 bg-background/70 p-4 backdrop-blur-sm">
                <p className="text-base text-foreground">
                  We are starting the stream in a few seconds.
                </p>
                <p className="mt-1 text-lg text-tertiary">
                  Iniciaremos la transmision en unos segundos.
                </p>
              </div>
            </div>
            <div className="mt-4 flex items-center justify-between text-xs text-muted-foreground">
              <span className="inline-flex items-center gap-1">
                <Monitor className="size-3.5" />
                1920x1080 safe area
              </span>
              <span>Bottom Anchor</span>
            </div>
          </SectionCard>
        </div>
      </div>
    </AppShell>
  );
}
