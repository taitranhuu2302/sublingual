import { Outlet, useLocation, NavLink } from "react-router-dom";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarProvider,
  SidebarInset,
} from "@/components/ui/sidebar";
import { Home, Archive, Settings, Waves } from "lucide-react";

const navItems = [
  { to: "/", label: "Home", icon: Home },
  { to: "/sessions", label: "Sessions", icon: Archive },
  { to: "/settings", label: "Settings", icon: Settings },
];

function AppSidebar() {
  const location = useLocation();

  return (
    <Sidebar collapsible="none" className="border-r border-sidebar-border">
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <div className="flex items-center gap-2 px-2 py-3 mb-2">
              <Waves className="h-6 w-6 text-sidebar-primary" />
              <span className="text-base font-semibold text-sidebar-foreground">
                Sublingual
              </span>
            </div>
            <SidebarMenu>
              {navItems.map(({ to, label, icon: Icon }) => {
                const isActive = location.pathname === to || (to !== "/" && location.pathname.startsWith(to));
                return (
                  <SidebarMenuItem key={to}>
                    <SidebarMenuButton asChild isActive={isActive}>
                      <NavLink to={to}>
                        <Icon className="h-4 w-4" />
                        <span>{label}</span>
                      </NavLink>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                );
              })}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
      <SidebarFooter>
        <p className="text-[11px] text-sidebar-foreground/40 px-2 py-1">
          NERIS &middot; Sublingual
        </p>
      </SidebarFooter>
    </Sidebar>
  );
}

export function Layout() {
  return (
    <SidebarProvider>
      <div className="flex h-screen w-screen overflow-hidden">
        <AppSidebar />
        <SidebarInset className="flex flex-col min-h-0">
          <main className="flex-1 overflow-y-auto flex flex-col min-h-0">
            <Outlet />
          </main>
        </SidebarInset>
      </div>
    </SidebarProvider>
  );
}
