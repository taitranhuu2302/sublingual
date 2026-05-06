import { EqualApproximately, SlidersHorizontal } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef } from 'react';

import { AppShell } from '@/components/layout/app-shell';
import { MetricBars } from '@/components/shared/metric-bars';
import { StatusPill } from '@/components/shared/status-pill';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { getBackendWsAudioUrl } from '@/config/backend-config';
import { RendererWebSocketClient } from '@/services/websocket-client';
import { useDashboardStore } from '@/stores/dashboard-store';
import { useSessionStore } from '@/stores/session-store';

export function DashboardPage() {
  const audioContextRef = useRef<AudioContext | null>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const sourceNodeRef = useRef<MediaStreamAudioSourceNode | null>(null);
  const workletNodeRef = useRef<AudioWorkletNode | null>(null);
  const chunkCounterRef = useRef<number>(0);
  const wsClientRef = useRef<RendererWebSocketClient | null>(null);

  const {
    selectedDeviceId,
    devices,
    isStreaming,
    telemetryLines,
    meterLevel,
    setDevices,
    setSelectedDeviceId,
    setStreaming,
    appendTelemetry,
    setMeterLevel,
    resetTelemetry,
  } = useDashboardStore();
  const {
    status: sessionStatus,
    startSession,
    stopSession,
    addSubtitle,
    updatePartial,
    setError,
    clearSubtitles,
    setStatus,
  } = useSessionStore();

  const meterHeights = useMemo(() => {
    const activeHeight = Math.max(4, Math.round(meterLevel * 100));
    return Array.from({ length: 12 }, (_value, index) => {
      const isActive = index < Math.round((meterLevel || 0) * 12);
      return isActive ? activeHeight : 6;
    });
  }, [meterLevel]);

  const stopStreaming = useCallback(async () => {
    wsClientRef.current?.stop();
    wsClientRef.current = null;
    workletNodeRef.current?.disconnect();
    sourceNodeRef.current?.disconnect();
    mediaStreamRef.current?.getTracks().forEach((track) => track.stop());
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      await audioContextRef.current.close();
    }

    workletNodeRef.current = null;
    sourceNodeRef.current = null;
    mediaStreamRef.current = null;
    audioContextRef.current = null;
    chunkCounterRef.current = 0;
    setMeterLevel(0);
    setStreaming(false);
    stopSession();
    appendTelemetry('> Session ended. Audio tracks stopped, context closed.');
  }, [appendTelemetry, setMeterLevel, setStreaming, stopSession]);

  const refreshDevices = useCallback(async () => {
    const mediaDevices = await navigator.mediaDevices.enumerateDevices();
    const inputs = mediaDevices
      .filter((device) => device.kind === 'audioinput')
      .map((device, index) => ({
        id: device.deviceId,
        label: device.label || `Microphone ${index + 1}`,
      }));
    setDevices(inputs);
    appendTelemetry(`> Found ${inputs.length} audio input device(s).`);
  }, [appendTelemetry, setDevices]);

  useEffect(() => {
    void refreshDevices();
  }, [refreshDevices]);

  useEffect(
    () => () => {
      void stopStreaming();
    },
    [stopStreaming],
  );

  const startStreaming = useCallback(async () => {
    try {
      if (!selectedDeviceId) {
        appendTelemetry('> No microphone selected.');
        return;
      }

      resetTelemetry();
      clearSubtitles();
      setError(null);
      startSession();
      appendTelemetry('> Requesting microphone access...');
      appendTelemetry('> Connecting to backend WebSocket...');

      const websocketClient = new RendererWebSocketClient({
        url: getBackendWsAudioUrl(),
        onOpen: () => {
          setStatus('streaming');
          appendTelemetry('> WebSocket connected.');
        },
        onClose: () => {
          appendTelemetry('> WebSocket closed.');
        },
        onPartial: (text) => {
          updatePartial(text);
        },
        onFinal: ({ original, translated, timestamp }) => {
          addSubtitle({
            id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
            original,
            translated,
            timestamp,
          });
        },
        onErrorMessage: (message) => {
          setError(message);
          appendTelemetry(`> Backend error: ${message}`);
        },
        onConnectionError: (message) => {
          setError(message);
          appendTelemetry(`> Connection error: ${message}`);
        },
      });
      wsClientRef.current = websocketClient;
      websocketClient.connect();

      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          deviceId: { exact: selectedDeviceId },
          sampleRate: 16_000,
          channelCount: 1,
        },
      });
      mediaStreamRef.current = stream;

      const audioContext = new AudioContext();
      audioContextRef.current = audioContext;

      await audioContext.audioWorklet.addModule(
        new URL('../workers/pcm-processor.worklet.ts', import.meta.url)
          .toString(),
      );
      appendTelemetry('> AudioWorklet loaded.');

      const sourceNode = audioContext.createMediaStreamSource(stream);
      sourceNodeRef.current = sourceNode;

      const workletNode = new AudioWorkletNode(audioContext, 'pcm-processor', {
        processorOptions: {
          targetSampleRate: 16_000,
          chunkSize: 4_096,
        },
      });
      workletNodeRef.current = workletNode;

      workletNode.port.onmessage = (event: MessageEvent) => {
        if (event.data.type === 'rms') {
          setMeterLevel(Math.min(1, Number(event.data.payload)));
        }

        if (event.data.type === 'pcm-chunk') {
          chunkCounterRef.current += 1;
          wsClientRef.current?.sendBinaryChunk(event.data.payload as ArrayBuffer);
          if (chunkCounterRef.current % 8 === 0) {
            appendTelemetry(
              `> Emitting audio chunk #${chunkCounterRef.current} (~250ms).`,
            );
          }
        }
      };

      sourceNode.connect(workletNode);
      workletNode.connect(audioContext.destination);

      setStreaming(true);
      appendTelemetry('> Streaming started at 16kHz mono.');
      await refreshDevices();
    } catch (error) {
      const message =
        error instanceof Error ? error.message : 'Unknown audio error.';
      appendTelemetry(`> Audio error: ${message}`);
      await stopStreaming();
      setError(message);
    }
  }, [
    addSubtitle,
    appendTelemetry,
    clearSubtitles,
    refreshDevices,
    resetTelemetry,
    selectedDeviceId,
    setError,
    setMeterLevel,
    setStatus,
    setStreaming,
    startSession,
    stopStreaming,
    updatePartial,
  ]);

  const toggleStreaming = useCallback(async () => {
    if (isStreaming) {
      await stopStreaming();
      return;
    }
    await startStreaming();
  }, [isStreaming, startStreaming, stopStreaming]);

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
                  <Select
                    value={selectedDeviceId ?? ''}
                    onValueChange={setSelectedDeviceId}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select microphone" />
                    </SelectTrigger>
                    <SelectContent>
                      {devices.map((device) => (
                        <SelectItem key={device.id} value={device.id}>
                          {device.label}
                        </SelectItem>
                      ))}
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
                <StatusPill
                  label={sessionStatus}
                  tone={sessionStatus === 'error' ? 'danger' : isStreaming ? 'success' : 'warning'}
                />
                <Badge variant="secondary">JSON</Badge>
                <Badge>WebSockets</Badge>
              </div>
            </CardHeader>
            <CardContent>
              <div className="rounded-lg border border-border/60 bg-input p-3 font-mono text-sm text-muted-foreground">
                {telemetryLines.map((line) => (
                  <p key={line.id}>{line.message}</p>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="lg:col-span-4">
          <Card className="h-full border-border/80 bg-card/80 backdrop-blur-sm">
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Live Monitor</CardTitle>
              <Badge variant="outline">{Math.round(meterLevel * 100)}%</Badge>
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
