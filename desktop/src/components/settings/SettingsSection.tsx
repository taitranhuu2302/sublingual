import { cn } from "@/lib/utils";

interface SettingsSectionProps {
  title: string;
  description?: string;
  children: React.ReactNode;
  className?: string;
}

export function SettingsSection({ title, description, children, className }: SettingsSectionProps) {
  return (
    <div className={cn("rounded-xl border border-border/50 bg-card/60 p-6", className)}>
      <h3 className="text-base font-semibold">{title}</h3>
      {description && <p className="text-xs text-muted-foreground mt-1">{description}</p>}
      <div className="mt-4 space-y-4">{children}</div>
    </div>
  );
}
