import { RouterProvider } from "react-router-dom";
import { TooltipProvider } from "@/components/ui/tooltip";

import { router } from "@/router";

const App = () => {
  return (
    <TooltipProvider>
      <RouterProvider router={router} />
    </TooltipProvider>
  );
};

export default App;
