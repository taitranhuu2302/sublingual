import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SettingsSection } from "./SettingsSection";
import { SettingsField } from "./SettingsField";
import { FolderOpen } from "lucide-react";
import type { AppSettings } from "@/types/electron-api";

interface Props {
  settings: AppSettings;
  onUpdate: (partial: Partial<AppSettings>) => void;
}

export function GeneralSettings({ settings, onUpdate }: Props) {
  const browseFolder = async (field: "sessionsRoot" | "speechToTextModelsRoot") => {
    const dir = await window.electronAPI.settings.browseDirectory(
      field === "sessionsRoot" ? "Select Sessions Folder" : "Select Models Folder"
    );
    if (dir) {
      onUpdate({ storage: { ...settings.storage, [field]: dir } });
    }
  };

  const openFolder = async (dirPath: string) => {
    await window.electronAPI.settings.openDirectory(dirPath);
  };

  return (
    <div className="space-y-6">
      <SettingsSection title="Storage" description="Configure where app data is stored">
        <SettingsField label="Sessions folder" helper="Where captured audio sessions are saved">
          <div className="flex gap-2">
            <Input value={settings.storage.sessionsRoot} readOnly className="flex-1 font-mono text-xs" />
            <Button variant="ghost" size="icon" onClick={() => browseFolder("sessionsRoot")}>
              <FolderOpen className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="sm" onClick={() => openFolder(settings.storage.sessionsRoot)}>
              Open
            </Button>
          </div>
        </SettingsField>

        <SettingsField label="Speech models folder" helper="Local speech-to-text model files">
          <div className="flex gap-2">
            <Input value={settings.storage.speechToTextModelsRoot} readOnly className="flex-1 font-mono text-xs" />
            <Button variant="ghost" size="icon" onClick={() => browseFolder("speechToTextModelsRoot")}>
              <FolderOpen className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="sm" onClick={() => openFolder(settings.storage.speechToTextModelsRoot)}>
              Open
            </Button>
          </div>
        </SettingsField>
      </SettingsSection>
    </div>
  );
}
