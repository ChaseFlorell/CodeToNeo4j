import { describe, it, before, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';
import os from 'os';
import { analyze } from '../src/analyzer.js';

let tmpDir: string;

before(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'ts-analyzer-test-'));
});

after(() => {
  fs.rmSync(tmpDir, { recursive: true, force: true });
});

function writeFile(relativePath: string, content: string): string {
  const full = path.join(tmpDir, relativePath);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content, 'utf-8');
  return full;
}

describe('analyzer - project name resolution', () => {
  it('uses package.json name as project name', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'pkg-name-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'my-app' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'index.ts'), 'export class Foo {}', 'utf-8');
    const result = await analyze(dir);
    assert.equal(result.projectName, 'my-app');
  });

  it('falls back to directory name when no package.json', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'no-pkg-'));
    fs.writeFileSync(path.join(dir, 'a.ts'), 'export class Bar {}', 'utf-8');
    const result = await analyze(dir);
    assert.equal(result.projectName, path.basename(dir));
  });
});

describe('analyzer - file discovery', () => {
  it('includes .ts files in the output', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'ts-files-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'main.ts'), 'export function hello() {}', 'utf-8');
    const result = await analyze(dir);
    assert.ok(Object.keys(result.files).some((f) => f.endsWith('.ts')));
  });

  it('excludes node_modules', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'nm-excl-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'src.ts'), 'export class A {}', 'utf-8');
    const nmDir = path.join(dir, 'node_modules', 'some-pkg');
    fs.mkdirSync(nmDir, { recursive: true });
    fs.writeFileSync(path.join(nmDir, 'index.ts'), 'export class Pkg {}', 'utf-8');
    const result = await analyze(dir);
    assert.ok(!Object.keys(result.files).some((f) => f.includes('node_modules')));
  });

  it('excludes .d.ts files', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'dts-excl-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'types.d.ts'), 'export declare class T {}', 'utf-8');
    fs.writeFileSync(path.join(dir, 'main.ts'), 'export class Main {}', 'utf-8');
    const result = await analyze(dir);
    assert.ok(!Object.keys(result.files).some((f) => f.endsWith('.d.ts')));
    assert.ok(Object.keys(result.files).some((f) => f === 'main.ts'));
  });

  it('handles directory with no tsconfig (glob fallback)', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'no-tsconfig-'));
    fs.writeFileSync(path.join(dir, 'a.ts'), 'export class A {}', 'utf-8');
    fs.writeFileSync(path.join(dir, 'b.js'), 'function b() {}', 'utf-8');
    const result = await analyze(dir);
    assert.ok(Object.keys(result.files).some((f) => f.endsWith('.ts')));
  });
});

describe('analyzer - symbol extraction integration', () => {
  it('extracts class symbol from a .ts file', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'class-sym-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'test-proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'model.ts'), 'export class User { name: string = ""; }', 'utf-8');
    const result = await analyze(dir);
    const fileResult = result.files['model.ts'];
    assert.ok(fileResult);
    const cls = fileResult.symbols.find((s) => s.name === 'User');
    assert.ok(cls);
    assert.equal(cls.kind, 'TypeScriptClass');
  });

  it('extracts function symbol from a .js file', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'js-fn-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'js-proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'utils.js'), 'function add(a, b) { return a + b; }', 'utf-8');
    const result = await analyze(dir);
    const fileResult = result.files['utils.js'];
    assert.ok(fileResult);
    const fn = fileResult.symbols.find((s) => s.name === 'add');
    assert.ok(fn);
    assert.equal(fn.kind, 'TypeScriptFunction');
  });

  it('sets projectRoot to the normalized absolute path', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'root-'));
    fs.writeFileSync(path.join(dir, 'a.ts'), 'export class X {}', 'utf-8');
    const result = await analyze(dir);
    assert.ok(path.isAbsolute(result.projectRoot));
  });
});
