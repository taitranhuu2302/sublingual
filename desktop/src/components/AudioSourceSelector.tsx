import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./ui/select";
import type { AudioSource } from "../types/electron-api";

interface Props {
  sources: AudioSource[];
  value: string;
  onChange: (sourceId: string) => void;
  disabled?: boolean;
}

export function AudioSourceSelector({ sources, value, onChange, disabled }: Props) {
  return (
    <Select value={value} onValueChange={onChange} disabled={disabled}>
      <SelectTrigger className="w-[250px]">
        <SelectValue placeholder="Select audio source" />
      </SelectTrigger>
      <SelectContent>
        {sources.map((s) => (
          <SelectItem key={s.id} value={s.id}>
            {s.name} ({s.type})
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
