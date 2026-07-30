import { readdir } from "node:fs/promises";
import { spawn } from "node:child_process";
import path from "node:path";
import process from "node:process";

async function collectTests(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const tests = [];

  for (const entry of entries) {
    const entryPath = path.join(directory, entry.name);

    if (entry.isDirectory()) {
      tests.push(...(await collectTests(entryPath)));
    } else if (entry.isFile() && entry.name.endsWith(".test.js")) {
      tests.push(entryPath);
    }
  }

  return tests;
}

const testFiles = (await collectTests(path.resolve("dist"))).sort();

if (testFiles.length === 0) {
  throw new Error("No compiled worker test files were found under dist.");
}

const testProcess = spawn(process.execPath, ["--test", ...testFiles], {
  stdio: "inherit",
});

testProcess.on("error", (error) => {
  throw error;
});

testProcess.on("exit", (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }

  process.exitCode = code ?? 1;
});
