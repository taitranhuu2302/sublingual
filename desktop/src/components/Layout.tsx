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
  SidebarTrigger,
} from "@/components/ui/sidebar";
import { Home, Archive, Settings, Waves, PanelLeft } from "lucide-react";

const navItems = [
  { to: "/", label: "Home", icon: Home },
  { to: "/sessions", label: "Sessions", icon: Archive },
  { to: "/settings", label: "Settings", icon: Settings },
];

function AppSidebar() {
  const location = useLocation();

  return (
    <Sidebar collapsible="icon" className="border-r border-sidebar-border/40">
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <div className="flex items-center gap-2 px-2 py-3 mb-2 group-data-[collapsible=icon]:justify-center group-data-[collapsible=icon]:px-0">
              <Waves className="h-6 w-6 text-sidebar-primary shrink-0" />
              <span className="text-base font-semibold text-sidebar-foreground group-data-[collapsible=icon]:hidden">
                Sublingual
              </span>
            </div>
            <SidebarMenu>
              {navItems.map(({ to, label, icon: Icon }) => {
                const isActive = location.pathname === to || (to !== "/" && location.pathname.startsWith(to));
                return (
                  <SidebarMenuItem key={to}>
                    <SidebarMenuButton asChild isActive={isActive} tooltip={label}>
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
      <SidebarFooter className="group-data-[collapsible=icon]:hidden">
        <p className="text-[11px] text-sidebar-foreground/40 px-2 py-1">
          NERIS &middot; Sublingual
        </p>
      </SidebarFooter>
    </Sidebar>
  );
}

export function Layout() {
  return (
    <SidebarProvider defaultOpen={true}>
      <div className="flex h-screen w-screen overflow-hidden">
        <AppSidebar />
        <SidebarInset className="flex flex-col min-h-0">
          <header className="flex items-center h-10 px-3 border-b border-border/20 shrink-0 bg-card/20">
            <SidebarTrigger className="h-7 w-7 text-muted-foreground hover:text-foreground" />
          </header>
          <main className="flex-1 overflow-y-auto flex flex-col min-h-0">
            <Outlet />
          </main>
        </SidebarInset>
      </div>
    </SidebarProvider>
  );
}
