import { cn } from "@/lib/utils";

type StatusPillProps = {
  label: string;
  tone?: "neutral" | "success" | "warning" | "danger";
};

const toneClassMap: Record<NonNullable<StatusPillProps["tone"]>, string> = {
  neutral: "bg-muted text-muted-foreground",
  success: "bg-emerald-500/20 text-emerald-300",
  warning: "bg-amber-500/20 text-amber-300",
  danger: "bg-red-500/20 text-red-300",
};

export function StatusPill({ label, tone = "neutral" }: StatusPillProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium",
        toneClassMap[tone]
      )}
    >
      {label}
    </span>
  );
}
