import { ReactNode } from "react";
import { Bell, Search, UserCircle } from "lucide-react";

import { Input } from "@/components/ui/input";
import { SideNav } from "@/components/layout/side-nav";

type AppShellProps = {
  activePage: "dashboard" | "history" | "captions" | "settings";
  title: string;
  description: string;
  children: ReactNode;
};

export function AppShell({
  activePage,
  title,
  description,
  children,
}: AppShellProps) {
  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <SideNav activePage={activePage} />
      <div className="ml-64 flex min-h-screen flex-1 flex-col">
        <header className="fixed left-64 right-0 top-0 z-20 flex h-16 items-center justify-end border-b border-border/50 bg-background/80 px-8 backdrop-blur-md">
          <div className="flex items-center gap-3">
            <div className="relative w-64">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search..."
                className="h-9 rounded-lg border-border bg-input pl-9"
              />
            </div>
            <button className="rounded-full p-2 text-muted-foreground transition-colors hover:text-foreground">
              <Bell className="size-5" />
            </button>
            <button className="rounded-full p-2 text-muted-foreground transition-colors hover:text-foreground">
              <UserCircle className="size-5" />
            </button>
          </div>
        </header>

        <main className="flex-1 px-8 pb-8 pt-24">
          <div className="mb-8">
            <h1 className="text-headline-xl mb-2">{title}</h1>
            <p className="text-body-lg text-muted-foreground">{description}</p>
          </div>
          {children}
        </main>
      </div>
    </div>
  );
}
