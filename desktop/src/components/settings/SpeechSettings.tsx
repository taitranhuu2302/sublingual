import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { SettingsSection } from "./SettingsSection";
import { SettingsField } from "./SettingsField";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Download, FolderOpen } from "lucide-react";
import { ModelDownloadDialog } from "@/components/ModelDownloadDialog";
import type { AppSettings, WhisperModel } from "@/types/electron-api";

const LANGUAGES = [
  { value: "en", label: "English" },
  { value: "vi", label: "Vietnamese" },
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

export function SpeechSettings({ settings, onUpdate }: Props) {
  const [models, setModels] = useState<WhisperModel[]>([]);
  const [showDownload, setShowDownload] = useState(false);

  useEffect(() => {
    window.electronAPI.asr.getModels().then(setModels);
  }, []);

  const refreshModels = async () => {
    const list = await window.electronAPI.asr.getModels();
    setModels(list);
  };

  return (
    <div className="space-y-6">
      <SettingsSection title="Speech-to-Text Model">
        <SettingsField label="Active model">
          <Select
            value={settings.speechToText.selectedModel}
            onValueChange={(v) =>
              onUpdate({ speechToText: { ...settings.speechToText, selectedModel: v } })
            }
          >
            <SelectTrigger className="w-full">
              <SelectValue placeholder="Select a model" />
            </SelectTrigger>
            <SelectContent>
              {models.filter((m) => m.downloaded).map((m) => (
                <SelectItem key={m.id} value={m.id}>{m.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </SettingsField>

        <SettingsField label="Chunk preset">
          <RadioGroup
            value={settings.speechToText.realtimeChunkPreset}
            onValueChange={(v) =>
              onUpdate({
                speechToText: {
                  ...settings.speechToText,
                  realtimeChunkPreset: v as "Fast" | "Balanced" | "Accurate",
                },
              })
            }
            className="space-y-2"
          >
            <div className="flex items-center space-x-2">
              <RadioGroupItem value="Fast" id="fast" />
              <Label htmlFor="fast" className="font-normal">Fast — 500ms chunks, lower accuracy</Label>
            </div>
            <div className="flex items-center space-x-2">
              <RadioGroupItem value="Balanced" id="balanced" />
              <Label htmlFor="balanced" className="font-normal">Balanced — 1000ms chunks</Label>
            </div>
            <div className="flex items-center space-x-2">
              <RadioGroupItem value="Accurate" id="accurate" />
              <Label htmlFor="accurate" className="font-normal">Accurate — 2000ms chunks, higher accuracy</Label>
            </div>
          </RadioGroup>
        </SettingsField>

        <SettingsField label="Source language" helper="Language of the audio being captured">
          <Select
            value={settings.speechToText.sourceLanguage}
            onValueChange={(v) =>
              onUpdate({ speechToText: { ...settings.speechToText, sourceLanguage: v } })
            }
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

      <SettingsSection title="Model Management">
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => setShowDownload(true)}>
            <Download className="h-4 w-4 mr-2" />
            Install Models
          </Button>
          <Button variant="outline" onClick={() => window.electronAPI.models.openFolder()}>
            <FolderOpen className="h-4 w-4 mr-2" />
            Open Folder
          </Button>
        </div>
      </SettingsSection>

      <ModelDownloadDialog
        open={showDownload}
        onOpenChange={(open) => {
          setShowDownload(open);
          if (!open) refreshModels();
        }}
      />
    </div>
  );
}
