import { describe, it, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'child_process';
import fs from 'fs';
import path from 'path';
import os from 'os';

const tsxBin = path.join(__dirname, '..', 'node_modules', '.bin', 'tsx');
const indexEntry = path.join(__dirname, '..', 'src', 'index.ts');

function runCli(args: string[]): { stdout: string; stderr: string; status: number | null } {
  const result = spawnSync(tsxBin, [indexEntry, ...args], { encoding: 'utf-8' });
  return { stdout: result.stdout, stderr: result.stderr, status: result.status };
}

let tmpDir: string;

before(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'ts-analyzer-cli-test-'));
});

after(() => {
  fs.rmSync(tmpDir, { recursive: true, force: true });
});

describe('index CLI', () => {
  it('exits with an error when no project root argument is given', () => {
    const { stderr, status } = runCli([]);
    assert.equal(status, 1);
    assert.match(stderr, /Usage: node dist\/index\.js <project_root_path>/);
  });

  it('exits with an error when the project root does not exist', () => {
    const missing = path.join(tmpDir, 'does-not-exist');
    const { stderr, status } = runCli([missing]);
    assert.equal(status, 1);
    assert.match(stderr, /Directory does not exist/);
  });

  it('exits with an error when the project root is a file, not a directory', () => {
    const filePath = path.join(tmpDir, 'not-a-dir.txt');
    fs.writeFileSync(filePath, 'hello', 'utf-8');
    const { stderr, status } = runCli([filePath]);
    assert.equal(status, 1);
    assert.match(stderr, /Directory does not exist/);
  });

  it('writes analysis JSON to stdout on success', () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'success-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'cli-success' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'main.ts'), 'export class Main {}', 'utf-8');

    const { stdout, status } = runCli([dir]);
    assert.equal(status, 0);
    const parsed = JSON.parse(stdout) as { projectName: string; files: Record<string, unknown> };
    assert.equal(parsed.projectName, 'cli-success');
    assert.ok('main.ts' in parsed.files);
  });
});
