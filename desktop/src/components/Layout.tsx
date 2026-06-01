import { NavLink, Outlet } from "react-router-dom";
import { Home, Settings, Archive, Mic } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const navItems = [
  { to: "/", label: "Home", icon: Home },
  { to: "/sessions", label: "Sessions", icon: Archive },
  { to: "/settings", label: "Settings", icon: Settings },
];

export function Layout() {
  return (
    <div className="flex h-screen flex-col overflow-hidden">
      <nav className="flex items-center gap-1 border-b bg-background px-4 py-2 shrink-0">
        <div className="flex items-center gap-2 mr-4">
          <Mic className="h-5 w-5 text-primary" />
          <span className="text-base font-semibold">Sublingual</span>
        </div>
        {navItems.map(({ to, label, icon: Icon }) => (
          <NavLink key={to} to={to} end={to === "/"}>
            {({ isActive }) => (
              <Button
                variant="ghost"
                className={cn(isActive && "bg-muted text-foreground")}
              >
                <Icon data-icon="inline-start" />
                {label}
              </Button>
            )}
          </NavLink>
        ))}
      </nav>
      <main className="flex-1 overflow-hidden flex flex-col">
        <Outlet />
      </main>
    </div>
  );
}
