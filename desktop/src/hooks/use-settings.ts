import { useState, useEffect, useCallback } from "react";
import type { AppSettings } from "../types/electron-api";

const DEFAULTS: AppSettings = {
    storage: { sessionsRoot: "", speechToTextModelsRoot: "" },
    overlay: {
        fontSize: 26,
        lineHeight: 1.35,
        width: 720,
        height: 200,
        theme: "Dark",
        opacity: 0.88,
        showTranslation: true,
        positionX: null,
        positionY: null,
    },
    speechToText: {
        selectedModel: "",
        sourceLanguage: "en",
    },
    translation: {
        enabled: true,
        provider: "google-free",
        targetLanguage: "vi",
        google: { endpoint: "https://translate.googleapis.com/translate_a/single" },
        local: { baseUrl: "http://127.0.0.1:3333" },
    },
};

export function useSettings() {
    const [settings, setSettingsState] = useState<AppSettings>(DEFAULTS);
    const [loaded, setLoaded] = useState(false);

    useEffect(() => {
        if (!window.electronAPI) return;

        window.electronAPI.settings.get().then((s) => {
            setSettingsState(s);
            setLoaded(true);
        });
    }, []);

    const update = useCallback(async (partial: Partial<AppSettings>) => {
        if (!window.electronAPI) return;
        await window.electronAPI.settings.set(partial);
        const fresh = await window.electronAPI.settings.get();
        setSettingsState(fresh);
    }, []);

    return { settings, update, loaded };
}
