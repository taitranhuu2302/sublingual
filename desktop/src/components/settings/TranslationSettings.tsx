import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { SettingsSection } from "./SettingsSection";
import { SettingsField } from "./SettingsField";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { RefreshCw, FolderOpen, FolderSearch, RotateCw, Trash2 } from "lucide-react";
import { useTranslateService } from "@/hooks/use-translate-service";
import type { AppSettings, TranslationResult } from "@/types/electron-api";

const LANGUAGES = [
  { value: "vi", label: "Vietnamese" },
  { value: "en", label: "English" },
  { value: "ja", label: "Japanese" },
  { value: "ko", label: "Korean" },
  { value: "zh", label: "Chinese" },
  { value: "fr", label: "French" },
  { value: "de", label: "German" },
  { value: "es", label: "Spanish" },
];

interface Props {
  settings: AppSettings;
  onUpdate: (partial: Partial<AppSettings>) => void;
}

const statusBadge: Record<string, { label: string; variant: "default" | "secondary" | "destructive" | "outline" }> = {
  running: { label: "Running", variant: "default" },
  starting: { label: "Starting...", variant: "secondary" },
  stopped: { label: "Stopped", variant: "destructive" },
  error: { label: "Error", variant: "destructive" },
};

export function TranslationSettings({ settings, onUpdate }: Props) {
  const ts = settings.translation;
  const [testText, setTestText] = useState("Hello, how are you today?");
  const [testResult, setTestResult] = useState<TranslationResult | null>(null);
  const [testError, setTestError] = useState("");
  const [testing, setTesting] = useState(false);

  const { status, logs, restart, clearLogs } = useTranslateService();

  const updateTranslation = (partial: Partial<typeof ts>) => {
    onUpdate({ translation: { ...ts, ...partial } });
  };

  const updateLocal = (partial: Partial<typeof ts.local>) => {
    onUpdate({ translation: { ...ts, local: { ...ts.local, ...partial } } });
  };

  const runTest = async () => {
    setTesting(true);
    setTestError("");
    setTestResult(null);
    try {
      const result = await window.electronAPI.translation.translate(
        testText,
        settings.speechToText.sourceLanguage,
        ts.targetLanguage
      );
      setTestResult(result);
    } catch (err) {
      setTestError(err instanceof Error ? err.message : String(err));
    } finally {
      setTesting(false);
    }
  };

  const browseModelsDir = async () => {
    const dir = await window.electronAPI.settings.browseDirectory("Select Translation Models Directory");
    if (dir) updateLocal({ modelsDir: dir });
  };

  return (
    <div className="space-y-6">
      <SettingsSection title="Translation">
        <SettingsField label="Enable translation" helper="Translate transcripts automatically" horizontal>
          <Switch checked={ts.enabled} onCheckedChange={(v) => updateTranslation({ enabled: v })} />
        </SettingsField>

        <SettingsField label="Provider" helper="Translation backend to use">
          <Select
            value={ts.provider}
            onValueChange={(v) => updateTranslation({ provider: v as "google-free" | "translate-local" })}
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="google-free">Google Translate</SelectItem>
              <SelectItem value="translate-local">Local TranslateService</SelectItem>
            </SelectContent>
          </Select>
        </SettingsField>

        <SettingsField label="Target language" helper="Translate transcripts into this language">
          <Select
            value={ts.targetLanguage}
            onValueChange={(v) => updateTranslation({ targetLanguage: v })}
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {LANGUAGES.map((l) => (
                <SelectItem key={l.value} value={l.value}>{l.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </SettingsField>
      </SettingsSection>

      {ts.provider === "google-free" ? (
        <SettingsSection title="Provider: Google Translate">
          <SettingsField label="Endpoint" helper="Free Google Translate API endpoint">
            <Input
              value={ts.google.endpoint}
              onChange={(e) => updateTranslation({ google: { endpoint: e.target.value } })}
              className="font-mono text-xs"
            />
          </SettingsField>
        </SettingsSection>
      ) : (
        <>
          <SettingsSection title="Provider: Local TranslateService">
            <SettingsField label="Base URL" helper="Local translation service address">
              <Input
                value={ts.local.baseUrl}
                onChange={(e) => updateLocal({ baseUrl: e.target.value })}
                className="font-mono text-xs"
              />
            </SettingsField>

            <SettingsField label="Models directory" helper="Path to NLLB-200 CTranslate2 model files">
              <div className="flex gap-2">
                <Input
                  value={ts.local.modelsDir}
                  onChange={(e) => updateLocal({ modelsDir: e.target.value })}
                  className="font-mono text-xs flex-1"
                />
                <Button variant="outline" size="icon" onClick={browseModelsDir}>
                  <FolderSearch className="h-4 w-4" />
                </Button>
                <Button
                  variant="outline"
                  size="icon"
                  onClick={() => window.electronAPI.settings.openDirectory(ts.local.modelsDir)}
                >
                  <FolderOpen className="h-4 w-4" />
                </Button>
              </div>
            </SettingsField>
          </SettingsSection>

          <SettingsSection title="Service Status">
            <div className="rounded-md border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Badge variant={statusBadge[status.status]?.variant ?? "secondary"}>
                    {statusBadge[status.status]?.label ?? status.status}
                  </Badge>
                  {status.pid && (
                    <span className="text-xs text-muted-foreground">PID: {status.pid}</span>
                  )}
                  {status.uptime != null && (
                    <span className="text-xs text-muted-foreground">
                      Uptime: {Math.floor(status.uptime / 60)}m {status.uptime % 60}s
                    </span>
                  )}
                </div>
                <Button variant="outline" size="sm" onClick={restart}>
                  <RotateCw className="h-3.5 w-3.5 mr-1" />
                  Restart Service
                </Button>
              </div>

              {status.loadedModels.length > 0 && (
                <p className="text-xs text-muted-foreground">
                  Models: {status.loadedModels.join(", ")}
                </p>
              )}

              {status.error && (
                <p className="text-xs text-destructive">{status.error}</p>
              )}
            </div>
          </SettingsSection>

          <SettingsSection title="Service Logs">
            <div className="rounded-md border bg-muted/30 p-3">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs text-muted-foreground">Recent logs (max 50 lines)</span>
                <Button variant="ghost" size="sm" onClick={clearLogs} disabled={logs.length === 0}>
                  <Trash2 className="h-3 w-3 mr-1" />
                  Clear
                </Button>
              </div>
              <ScrollArea className="h-40 rounded bg-black/50 p-2">
                {logs.length === 0 ? (
                  <p className="text-xs text-muted-foreground italic p-2">No logs yet...</p>
                ) : (
                  logs.map((line, i) => (
                    <p key={i} className="text-xs font-mono text-muted-foreground whitespace-nowrap">
                      {line}
                    </p>
                  ))
                )}
              </ScrollArea>
            </div>
          </SettingsSection>
        </>
      )}

      <Separator />

      <SettingsSection title="Test Translation">
        <SettingsField label="Source text">
          <Textarea
            value={testText}
            onChange={(e) => setTestText(e.target.value)}
            rows={2}
            className="resize-none"
          />
        </SettingsField>

        <Button onClick={runTest} disabled={testing || !testText.trim()}>
          <RefreshCw className={`h-4 w-4 mr-2 ${testing ? "animate-spin" : ""}`} />
          Translate
        </Button>

        {testResult && (
          <div className="space-y-2">
            <div className="rounded-md border bg-muted/50 p-3">
              <p className="text-sm">{testResult.translatedText}</p>
            </div>
            <p className="text-xs text-muted-foreground">
              Provider: {testResult.providerName} · {testResult.durationMs}ms
            </p>
          </div>
        )}

        {testError && (
          <p className="text-sm text-destructive">{testError}</p>
        )}
      </SettingsSection>
    </div>
  );
}
