import { useState } from "react";
import { cn } from "@/lib/utils";
import { useSettings } from "../hooks/use-settings";
import { GeneralSettings } from "../components/settings/GeneralSettings";
import { SpeechSettings } from "../components/settings/SpeechSettings";
import { TranslationSettings } from "../components/settings/TranslationSettings";
import { OverlaySettingsPanel } from "../components/settings/OverlaySettings";
import { Settings, Mic, Languages, Monitor } from "lucide-react";

const TABS = [
  { id: "general", label: "General", icon: Settings },
  { id: "speech", label: "Speech", icon: Mic },
  { id: "translation", label: "Translation", icon: Languages },
  { id: "overlay", label: "Overlay", icon: Monitor },
] as const;

type TabId = (typeof TABS)[number]["id"];

export function SettingsPage() {
  const { settings, update, loaded } = useSettings();
  const [activeTab, setActiveTab] = useState<TabId>("general");

  if (!loaded) return null;

  return (
    <div className="flex flex-1 min-h-0">
      <nav className="w-48 border-r py-4 space-y-1 px-2 shrink-0">
        {TABS.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            onClick={() => setActiveTab(id)}
            className={cn(
              "w-full flex items-center gap-2 px-3 py-2 rounded-md text-sm transition-colors",
              activeTab === id
                ? "bg-muted font-medium text-foreground"
                : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </button>
        ))}
      </nav>

      <div className="flex-1 overflow-y-auto min-h-0">
        <div className="p-6 max-w-2xl">
          <h1 className="text-2xl font-bold mb-6">
            {TABS.find((t) => t.id === activeTab)?.label} Settings
          </h1>

          {activeTab === "general" && <GeneralSettings settings={settings} onUpdate={update} />}
          {activeTab === "speech" && <SpeechSettings settings={settings} onUpdate={update} />}
          {activeTab === "translation" && <TranslationSettings settings={settings} onUpdate={update} />}
          {activeTab === "overlay" && <OverlaySettingsPanel settings={settings} onUpdate={update} />}
        </div>
      </div>
    </div>
  );
}
