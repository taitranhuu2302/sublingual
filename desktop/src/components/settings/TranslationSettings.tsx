import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { SettingsSection } from "./SettingsSection";
import { SettingsField } from "./SettingsField";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { RefreshCw } from "lucide-react";
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

export function TranslationSettings({ settings, onUpdate }: Props) {
  const ts = settings.translation;
  const [testText, setTestText] = useState("Hello, how are you today?");
  const [testResult, setTestResult] = useState<TranslationResult | null>(null);
  const [testError, setTestError] = useState("");
  const [testing, setTesting] = useState(false);

  const updateTranslation = (partial: Partial<typeof ts>) => {
    onUpdate({ translation: { ...ts, ...partial } });
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
        <SettingsSection title="Provider: Local TranslateService">
          <SettingsField label="Base URL" helper="Local translation service address">
            <Input
              value={ts.local.baseUrl}
              onChange={(e) => updateTranslation({ local: { baseUrl: e.target.value } })}
              className="font-mono text-xs"
            />
          </SettingsField>
        </SettingsSection>
      )}

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
