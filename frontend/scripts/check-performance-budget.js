/* eslint-disable @typescript-eslint/no-require-imports */
const fs = require('fs');
const path = require('path');

const chunksDir = path.join(__dirname, '../.next/static/chunks');
if (!fs.existsSync(chunksDir)) {
  console.error("Error: .next/static/chunks directory not found. Run 'npm run build' first.");
  process.exit(1);
}

const BUDGET_SINGLE_KB = 350; // Performance budget for any single JS chunk
const budgetSingleBytes = BUDGET_SINGLE_KB * 1024;

let failed = false;
let totalSize = 0;
let fileCount = 0;

console.log(`=== Performance Budget Check (Single Chunk Limit: ${BUDGET_SINGLE_KB} KB) ===`);

function scanDirectory(dir) {
  const files = fs.readdirSync(dir);
  for (const file of files) {
    const filePath = path.join(dir, file);
    const stat = fs.statSync(filePath);
    if (stat.isDirectory()) {
      scanDirectory(filePath);
    } else if (file.endsWith('.js')) {
      fileCount++;
      totalSize += stat.size;
      const sizeKb = (stat.size / 1024).toFixed(2);
      
      if (stat.size > budgetSingleBytes) {
        console.error(`❌ Chunk '${file}' EXCEEDED budget: ${sizeKb} KB (Limit: ${BUDGET_SINGLE_KB} KB)`);
        failed = true;
      }
    }
  }
}

scanDirectory(chunksDir);

const totalSizeMb = (totalSize / (1024 * 1024)).toFixed(2);
console.log(`\nChecked ${fileCount} chunk files.`);
console.log(`Total Static JS bundle size: ${totalSizeMb} MB`);

if (failed) {
  console.error("\nPerformance budget check FAILED. Optimize imports or lazy load heavy modules.");
  process.exit(1);
} else {
  console.log("\nPerformance budget check PASSED successfully!");
  process.exit(0);
}
