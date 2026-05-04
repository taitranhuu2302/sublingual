import { Activity, Captions, History, LayoutDashboard, Settings } from "lucide-react";
import { NavLink } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const navItems = [
  { to: "/dashboard", key: "dashboard", label: "Dashboard", icon: LayoutDashboard },
  { to: "/history", key: "history", label: "History", icon: History },
  { to: "/captions", key: "captions", label: "Captions", icon: Captions },
  { to: "/settings", key: "settings", label: "Settings", icon: Settings },
] as const;

type SideNavProps = {
  activePage: "dashboard" | "history" | "captions" | "settings";
};

export function SideNav({ activePage }: SideNavProps) {
  return (
    <nav className="fixed inset-y-0 left-0 z-30 flex w-64 flex-col border-r border-border bg-card px-4 py-6">
      <div className="mb-10 flex items-center gap-3 px-2">
        <div className="flex size-8 items-center justify-center rounded-full bg-primary text-primary-foreground">
          <Activity className="size-4" />
        </div>
        <span className="text-lg font-bold tracking-tight">LingoStream</span>
      </div>

      <div className="space-y-1">
        {navItems.map((item) => (
          <NavLink
            key={item.key}
            to={item.to}
            className={cn(
              "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-accent/20 hover:text-foreground",
              activePage === item.key && "bg-primary/10 text-primary"
            )}
          >
            <item.icon className="size-4" />
            {item.label}
          </NavLink>
        ))}
      </div>

      <div className="mt-auto">
        <Button className="w-full">Start Session</Button>
      </div>
    </nav>
  );
}
