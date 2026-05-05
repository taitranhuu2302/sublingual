import { EqualApproximately, SlidersHorizontal } from "lucide-react";

import { AppShell } from "@/components/layout/app-shell";
import { MetricBars } from "@/components/shared/metric-bars";
import { StatusPill } from "@/components/shared/status-pill";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useDashboardStore } from "@/stores/dashboard-store";

const meterHeights = [20, 40, 60, 80, 95, 75, 50, 35, 45, 65, 85, 100];

export function DashboardPage() {
  const {
    primarySource,
    secondarySource,
    isStreaming,
    telemetryLines,
    setPrimarySource,
    setSecondarySource,
    toggleStreaming,
  } = useDashboardStore();

  return (
    <AppShell
      activePage="dashboard"
      title="Session Dashboard"
      description="Configure your audio inputs and monitor levels before streaming."
    >
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-8">
          <Card className="border-border/80 bg-card/80 backdrop-blur-sm">
            <CardHeader className="flex flex-row items-start justify-between border-b border-border/50">
              <div>
                <CardTitle>Audio Configuration</CardTitle>
                <CardDescription>Select active capture sources.</CardDescription>
              </div>
              <SlidersHorizontal className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="space-y-6 pt-6">
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <p className="text-sm text-muted-foreground">Primary Source</p>
                  <Select value={primarySource} onValueChange={setPrimarySource}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="studio-mic">Studio Microphone (USB)</SelectItem>
                      <SelectItem value="array">Internal Array Mic</SelectItem>
                      <SelectItem value="interface">External Audio Interface</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <p className="text-sm text-muted-foreground">Secondary Source</p>
                  <Select value={secondarySource} onValueChange={setSecondarySource}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="system-audio">System Audio</SelectItem>
                      <SelectItem value="browser-output">Browser Output</SelectItem>
                      <SelectItem value="none">None</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <Button size="lg" className="w-full" onClick={toggleStreaming}>
                {isStreaming ? "Stop Stream Session" : "Start Stream Session"}
              </Button>
            </CardContent>
          </Card>

          <Card className="border-border/80 bg-card/80 backdrop-blur-sm">
            <CardHeader className="flex flex-row items-start justify-between">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <EqualApproximately className="size-4 text-primary" />
                  Telemetry Output
                </CardTitle>
                <CardDescription>Streaming pipeline status.</CardDescription>
              </div>
              <div className="space-x-2">
                <StatusPill label={isStreaming ? "Streaming" : "Idle"} tone={isStreaming ? "success" : "warning"} />
                <Badge variant="secondary">JSON</Badge>
                <Badge>WebSockets</Badge>
              </div>
            </CardHeader>
            <CardContent>
              <div className="rounded-lg border border-border/60 bg-input p-3 font-mono text-sm text-muted-foreground">
                {telemetryLines.map((line) => (
                  <p key={line}>{line}</p>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="lg:col-span-4">
          <Card className="h-full border-border/80 bg-card/80 backdrop-blur-sm">
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Live Monitor</CardTitle>
              <Badge variant="outline">-12 dB</Badge>
            </CardHeader>
            <CardContent className="space-y-4">
              <MetricBars bars={meterHeights} />
              <div className="flex justify-between text-xs uppercase tracking-wide text-muted-foreground">
                <span>L</span>
                <span>Peak Level</span>
                <span>R</span>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </AppShell>
  );
}
