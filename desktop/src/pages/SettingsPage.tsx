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
      {/* Sub-tab navigation */}
      <nav className="w-44 border-r border-border/50 py-4 space-y-0.5 px-2 shrink-0 bg-card/30">
        {TABS.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            onClick={() => setActiveTab(id)}
            className={cn(
              "w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition-colors",
               activeTab === id
                ? "bg-[hsl(220,60%,24%)] text-foreground font-medium"
                : "text-muted-foreground hover:text-foreground hover:bg-[hsl(234,19%,20%)]"
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </button>
        ))}
      </nav>

      {/* Content area */}
      <div className="flex-1 overflow-y-auto min-h-0">
        <div className="p-6 max-w-2xl mx-auto">
          <h1 className="text-2xl font-bold mb-8">
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
