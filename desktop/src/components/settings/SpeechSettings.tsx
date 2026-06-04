import { useState, useEffect } from "react";
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
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { Download, FolderOpen, Trash2 } from "lucide-react";
import { ModelDownloadDialog } from "@/components/ModelDownloadDialog";
import type { AppSettings, VoskModel } from "@/types/electron-api";

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
  const [models, setModels] = useState<VoskModel[]>([]);
  const [showDownload, setShowDownload] = useState(false);
  const [modelToRemove, setModelToRemove] = useState<VoskModel | null>(null);
  const hasSpkModel = models.some((m) => m.id === "vosk-model-spk-0.4" && m.downloaded);

  useEffect(() => {
    if (!window.electronAPI) return;
    window.electronAPI.asr.getModels().then(setModels);
  }, []);

  const refreshModels = async () => {
    if (!window.electronAPI) return;
    const list = await window.electronAPI.asr.getModels();
    setModels(list);
  };

  const handleRemoveModel = async () => {
    if (!modelToRemove || !window.electronAPI) return;
    await window.electronAPI.models.remove(modelToRemove.id);
    setModelToRemove(null);
    const fresh = await window.electronAPI.settings.get();
    onUpdate(fresh);
    refreshModels();
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
              {models.filter((m) => m.downloaded).length === 0 ? (
                <SelectItem value="__placeholder__" disabled>No models installed</SelectItem>
              ) : (
                models.filter((m) => m.downloaded).map((m) => (
                  <SelectItem key={m.id} value={m.id} className="flex items-center justify-between">
                    <span className="flex-1">{m.name}</span>
                    <span
                      className="inline-flex items-center justify-center h-6 w-6 rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive ml-2"
                      onPointerDown={(e) => {
                        e.stopPropagation();
                        e.preventDefault();
                        setModelToRemove(m);
                      }}
                    >
                      <Trash2 className="h-3 w-3" />
                    </span>
                  </SelectItem>
                ))
              )}
            </SelectContent>
          </Select>
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

        <SettingsField label="Max speakers" helper={hasSpkModel ? "Maximum number of speakers to detect (2-8)" : "Install Speaker Identification model to enable"}>
          <Select
            value={String(settings.speechToText.maxSpeakers ?? 4)}
            disabled={!hasSpkModel}
            onValueChange={(v) =>
              onUpdate({
                speechToText: { ...settings.speechToText, maxSpeakers: Number(v) },
              })
            }
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[2, 3, 4, 5, 6, 7, 8].map((n) => (
                <SelectItem key={n} value={String(n)}>{n} speakers</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </SettingsField>

        <SettingsField label="Flush timeout" helper="How long to wait before auto-finalizing a sentence (in milliseconds)">
          <Select
            value={String(settings.speechToText.flushTimeoutMs ?? 3000)}
            onValueChange={(v) =>
              onUpdate({
                speechToText: { ...settings.speechToText, flushTimeoutMs: Number(v) },
              })
            }
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="500">500ms</SelectItem>
              <SelectItem value="1000">1s</SelectItem>
              <SelectItem value="2000">2s</SelectItem>
              <SelectItem value="3000">3s</SelectItem>
              <SelectItem value="5000">5s</SelectItem>
              <SelectItem value="10000">10s</SelectItem>
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

      <Dialog open={!!modelToRemove} onOpenChange={(open) => { if (!open) setModelToRemove(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove Model</DialogTitle>
            <DialogDescription>
              Are you sure you want to remove <strong>{modelToRemove?.name}</strong>? This will delete the model files from your disk.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setModelToRemove(null)}>Cancel</Button>
            <Button variant="destructive" onClick={handleRemoveModel}>Remove</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
