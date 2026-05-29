import { Button } from "@/components/ui/button";

function SettingsPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-3xl font-bold">Settings</h1>
      <p className="text-muted-foreground">
        Configure your preferences here.
      </p>
      <div className="flex gap-2">
        <Button variant="outline">Reset</Button>
        <Button>Save</Button>
      </div>
    </div>
  );
}

export default SettingsPage;
