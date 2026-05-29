import { cn } from "@/lib/utils";
import { Label } from "@/components/ui/label";

interface SettingsFieldProps {
  label: string;
  helper?: string;
  htmlFor?: string;
  horizontal?: boolean;
  children: React.ReactNode;
  className?: string;
}

export function SettingsField({ label, helper, htmlFor, horizontal, children, className }: SettingsFieldProps) {
  if (horizontal) {
    return (
      <div className={cn("flex items-center justify-between gap-4", className)}>
        <div className="flex-1">
          <Label htmlFor={htmlFor} className="text-sm font-medium">{label}</Label>
          {helper && <p className="text-xs text-muted-foreground mt-0.5">{helper}</p>}
        </div>
        <div className="shrink-0">{children}</div>
      </div>
    );
  }

  return (
    <div className={cn("space-y-1.5", className)}>
      <Label htmlFor={htmlFor} className="text-sm font-medium">{label}</Label>
      {children}
      {helper && <p className="text-xs text-muted-foreground">{helper}</p>}
    </div>
  );
}
