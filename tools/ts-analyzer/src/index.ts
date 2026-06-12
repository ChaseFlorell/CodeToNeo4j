import fs from 'fs';
import { analyze } from './analyzer.js';

async function main(): Promise<void> {
  const projectRoot = process.argv[2];

  if (!projectRoot) {
    process.stderr.write('Usage: node dist/index.js <project_root_path>\n');
    process.exit(1);
  }

  if (!fs.existsSync(projectRoot) || !fs.statSync(projectRoot).isDirectory()) {
    process.stderr.write(`Error: Directory does not exist: ${projectRoot}\n`);
    process.exit(1);
  }

  try {
    const result = await analyze(projectRoot);
    process.stdout.write(JSON.stringify(result, null, 2) + '\n');
  } catch (e) {
    process.stderr.write(`Error: ${e}\n`);
    process.exit(1);
  }
}

void main();
