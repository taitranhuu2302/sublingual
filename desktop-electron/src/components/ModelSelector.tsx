import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./ui/select";
import type { WhisperModel } from "../types/electron-api";

interface Props {
  models: WhisperModel[];
  value: string;
  onChange: (modelId: string) => void;
  disabled?: boolean;
}

export function ModelSelector({ models, value, onChange, disabled }: Props) {
  return (
    <Select value={value} onValueChange={onChange} disabled={disabled}>
      <SelectTrigger className="w-[250px]">
        <SelectValue placeholder="Select model" />
      </SelectTrigger>
      <SelectContent>
        {models.map((m) => (
          <SelectItem key={m.id} value={m.id} disabled={!m.downloaded}>
            {m.name} {!m.downloaded && "(not downloaded)"}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
