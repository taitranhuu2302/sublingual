import { Clock3, Filter, Languages, PlayCircle } from "lucide-react";

import { AppShell } from "@/components/layout/app-shell";
import { SectionCard } from "@/components/layout/section-card";
import { mockSessions } from "@/models/history";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export function HistoryPage() {
  return (
    <AppShell
      activePage="history"
      title="Session History"
      description="Review and replay previous translated sessions."
    >
      <div className="space-y-6">
        <SectionCard
          title="Session Library"
          description="Search and filter previous sessions."
          icon={<Clock3 className="size-4 text-primary" />}
          className="border-border/80 bg-card/80 backdrop-blur-sm"
          contentClassName="space-y-4"
        >
          <div className="flex flex-col gap-3 md:flex-row">
            <Input placeholder="Search by title or ID..." className="md:max-w-sm" />
            <Button variant="outline" className="md:w-auto">
              <Filter className="mr-2 size-4" />
              Filters
            </Button>
          </div>

          <div className="space-y-3">
            {mockSessions.map((session) => (
              <div
                key={session.id}
                className="flex flex-col gap-3 rounded-lg border border-border/70 bg-input p-4 md:flex-row md:items-center md:justify-between"
              >
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <p className="text-sm font-medium">{session.title}</p>
                    <Badge variant="outline">{session.id}</Badge>
                  </div>
                  <div className="flex items-center gap-3 text-xs text-muted-foreground">
                    <span className="inline-flex items-center gap-1">
                      <Languages className="size-3.5" />
                      {session.language}
                    </span>
                    <span>{session.duration}</span>
                    <span>{session.timestamp}</span>
                  </div>
                </div>
                <Button size="sm">
                  <PlayCircle className="mr-2 size-4" />
                  Replay
                </Button>
              </div>
            ))}
          </div>
        </SectionCard>
      </div>
    </AppShell>
  );
}
