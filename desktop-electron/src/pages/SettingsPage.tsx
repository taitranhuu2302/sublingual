import { useEffect, useState } from "react";
import { Button } from "../components/ui/button";
import { AudioSourceSelector } from "../components/AudioSourceSelector";
import { ModelSelector } from "../components/ModelSelector";
import { useSettings } from "../hooks/use-settings";
import type { AudioSource, WhisperModel } from "../types/electron-api";

export function SettingsPage() {
  const { settings, update } = useSettings();
  const [sources, setSources] = useState<AudioSource[]>([]);
  const [models, setModels] = useState<WhisperModel[]>([]);

  useEffect(() => {
    window.electronAPI.audio.getSources().then(setSources);
    window.electronAPI.asr.getModels().then(setModels);
  }, []);

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-2xl font-bold">Settings</h1>

      <div className="space-y-4">
        <div>
          <label className="text-sm font-medium mb-2 block">Default Audio Source</label>
          <AudioSourceSelector
            sources={sources}
            value={settings.audioSourceId}
            onChange={(id) => update({ audioSourceId: id })}
          />
        </div>

        <div>
          <label className="text-sm font-medium mb-2 block">ASR Model</label>
          <ModelSelector
            models={models}
            value={settings.modelId}
            onChange={(id) => update({ modelId: id })}
          />
        </div>

        <div>
          <label className="text-sm font-medium mb-2 block">Language</label>
          <select
            className="border rounded px-3 py-2"
            value={settings.language}
            onChange={(e) => update({ language: e.target.value })}
          >
            <option value="en">English</option>
            <option value="vi">Vietnamese</option>
            <option value="ja">Japanese</option>
            <option value="ko">Korean</option>
            <option value="zh">Chinese</option>
          </select>
        </div>
      </div>
    </div>
  );
}
