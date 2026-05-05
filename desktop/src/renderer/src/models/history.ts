export type SessionSummary = {
  id: string;
  title: string;
  language: string;
  duration: string;
  timestamp: string;
};

export const mockSessions: SessionSummary[] = [
  {
    id: "SES-1042",
    title: "Weekly Product Standup",
    language: "EN -> ES",
    duration: "00:24:11",
    timestamp: "Today, 09:20",
  },
  {
    id: "SES-1039",
    title: "Client Demo - EU Team",
    language: "EN -> FR",
    duration: "00:42:53",
    timestamp: "Yesterday, 16:40",
  },
  {
    id: "SES-1031",
    title: "Support Retrospective",
    language: "EN -> DE",
    duration: "00:18:02",
    timestamp: "May 02, 11:02",
  },
];
