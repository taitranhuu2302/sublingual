import { Navigate, createBrowserRouter } from "react-router-dom";

import { CaptionsPage } from "@/routes/captions-page";
import { DashboardPage } from "@/routes/dashboard-page";
import { HistoryPage } from "@/routes/history-page";
import { SettingsPage } from "@/routes/settings-page";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <Navigate to="/dashboard" replace />,
  },
  {
    path: "/dashboard",
    element: <DashboardPage />,
  },
  {
    path: "/history",
    element: <HistoryPage />,
  },
  {
    path: "/captions",
    element: <CaptionsPage />,
  },
  {
    path: "/settings",
    element: <SettingsPage />,
  },
  {
    path: "*",
    element: <Navigate to="/dashboard" replace />,
  },
]);
