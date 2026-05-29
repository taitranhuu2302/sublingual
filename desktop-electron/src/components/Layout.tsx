import { NavLink, Outlet } from "react-router-dom";
import { Home, Settings } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const navItems = [
  { to: "/", label: "Home", icon: Home },
  { to: "/settings", label: "Settings", icon: Settings },
];

function Layout() {
  return (
    <div className="flex min-h-screen flex-col">
      <nav className="flex items-center gap-1 border-b bg-background px-4 py-2">
        {navItems.map(({ to, label, icon: Icon }) => (
          <NavLink key={to} to={to} end>
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
      <main className="flex-1 p-6">
        <Outlet />
      </main>
    </div>
  );
}

export default Layout;
