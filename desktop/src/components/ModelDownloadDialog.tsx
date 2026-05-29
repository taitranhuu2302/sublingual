import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { ScrollArea } from "@/components/ui/scroll-area";
import { useModelDownload } from "@/hooks/use-model-download";
import { Download, CheckCircle, XCircle, FolderOpen } from "lucide-react";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ModelDownloadDialog({ open, onOpenChange }: Props) {
  const { models, activeDownload, startDownload, cancelDownload, openFolder } = useModelDownload();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>Install Speech Models</DialogTitle>
        </DialogHeader>

        <ScrollArea className="max-h-[400px]">
          <div className="space-y-3 pr-2">
            {models.map((model) => {
              const isDownloading = activeDownload?.modelId === model.id && activeDownload.status === "downloading";
              const downloadError = activeDownload?.modelId === model.id && activeDownload.status === "error";
              const justCompleted = activeDownload?.modelId === model.id && activeDownload.status === "completed";

              return (
                <div key={model.id} className="rounded-lg border p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{model.name}</span>
                        <Badge variant="outline" className="text-xs">{model.size}</Badge>
                        <Badge variant="outline" className="text-xs">{model.language}</Badge>
                      </div>
                      <p className="text-sm text-muted-foreground mt-1">{model.description}</p>
                    </div>

                    <div className="shrink-0">
                      {model.isInstalled || justCompleted ? (
                        <Badge className="bg-green-600 text-white">
                          <CheckCircle className="h-3 w-3 mr-1" />
                          Installed
                        </Badge>
                      ) : isDownloading ? (
                        <Button variant="outline" size="sm" onClick={cancelDownload}>
                          Cancel
                        </Button>
                      ) : (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => startDownload(model.id)}
                          disabled={!!activeDownload && activeDownload.status === "downloading"}
                        >
                          <Download className="h-3 w-3 mr-1" />
                          Download
                        </Button>
                      )}
                    </div>
                  </div>

                  {isDownloading && (
                    <div className="mt-3">
                      <Progress value={activeDownload.percent} className="h-2" />
                      <p className="text-xs text-muted-foreground mt-1">{activeDownload.percent}%</p>
                    </div>
                  )}

                  {downloadError && (
                    <div className="mt-2 flex items-center gap-2 text-sm text-destructive">
                      <XCircle className="h-4 w-4" />
                      {activeDownload.error || "Download failed"}
                      <Button variant="ghost" size="sm" onClick={() => startDownload(model.id)}>
                        Retry
                      </Button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </ScrollArea>

        <DialogFooter>
          <Button variant="ghost" onClick={openFolder}>
            <FolderOpen className="h-4 w-4 mr-2" />
            Open Models Folder
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
