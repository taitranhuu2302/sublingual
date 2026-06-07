import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const translateDir = path.resolve(__dirname, "..", "..", "translate", "scripts");
const isWindows = process.platform === "win32";

const script = isWindows ? "build_pyinstaller.ps1" : "build_pyinstaller.sh";
const cmd = isWindows ? "powershell" : "bash";
const args = isWindows ? ["-File", path.join(translateDir, script)] : [path.join(translateDir, script)];

console.log(`Building translate-service${isWindows ? ".exe" : ""}...`);

const child = spawn(cmd, args, { stdio: "inherit", cwd: translateDir });

child.on("exit", (code) => {
  if (code !== 0) {
    console.error("Build failed with code", code);
    process.exit(code ?? 1);
  }
  console.log(`Done: desktop/bin/translate/translate-service${isWindows ? ".exe" : ""}`);
});
