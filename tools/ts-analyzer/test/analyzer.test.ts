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

describe('analyzer - tsconfig handling', () => {
  it('uses tsconfig.json root files when present', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'tsconfig-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'tsconfig-proj' }), 'utf-8');
    fs.writeFileSync(
      path.join(dir, 'tsconfig.json'),
      JSON.stringify({ compilerOptions: { target: 'es2020' }, include: ['*.ts'] }),
      'utf-8',
    );
    fs.writeFileSync(path.join(dir, 'main.ts'), 'export class FromTsconfig {}', 'utf-8');
    const result = await analyze(dir);
    const fileResult = result.files['main.ts'];
    assert.ok(fileResult);
    assert.ok(fileResult.symbols.some((s) => s.name === 'FromTsconfig'));
  });

  it('warns on stderr but still analyzes when tsconfig.json is malformed', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'bad-tsconfig-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'bad-tsconfig-proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'tsconfig.json'), '{ this is not valid json', 'utf-8');
    fs.writeFileSync(path.join(dir, 'main.ts'), 'export class Fallback {}', 'utf-8');

    const originalWrite = process.stderr.write.bind(process.stderr);
    let stderrOutput = '';
    process.stderr.write = ((chunk: string) => {
      stderrOutput += chunk;
      return true;
    }) as typeof process.stderr.write;

    try {
      const result = await analyze(dir);
      assert.ok(result.projectName === 'bad-tsconfig-proj');
    } finally {
      process.stderr.write = originalWrite;
    }
    assert.ok(stderrOutput.includes('Could not read tsconfig.json'));
  });
});

describe('analyzer - project name resolution edge cases', () => {
  it('falls back to directory name when package.json is malformed', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'bad-pkg-'));
    fs.writeFileSync(path.join(dir, 'package.json'), '{ not valid json', 'utf-8');
    fs.writeFileSync(path.join(dir, 'a.ts'), 'export class A {}', 'utf-8');
    const result = await analyze(dir);
    assert.equal(result.projectName, path.basename(dir));
  });
});

describe('analyzer - generated file exclusion', () => {
  it('excludes files matching generated-file name suffixes', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'generated-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'gen-proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'schema.generated.ts'), 'export class Generated {}', 'utf-8');
    fs.writeFileSync(path.join(dir, 'api.gen.ts'), 'export class Gen {}', 'utf-8');
    fs.writeFileSync(path.join(dir, 'real.ts'), 'export class Real {}', 'utf-8');
    const result = await analyze(dir);
    assert.ok(!('schema.generated.ts' in result.files));
    assert.ok(!('api.gen.ts' in result.files));
    assert.ok('real.ts' in result.files);
  });

  it('excludes files under dist/build/.next/coverage directories', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'gen-dirs-'));
    fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify({ name: 'gen-dirs-proj' }), 'utf-8');
    fs.writeFileSync(path.join(dir, 'real.ts'), 'export class Real {}', 'utf-8');
    for (const skipDir of ['dist', 'build', '.next', 'coverage']) {
      fs.mkdirSync(path.join(dir, skipDir), { recursive: true });
      fs.writeFileSync(path.join(dir, skipDir, 'output.ts'), 'export class Output {}', 'utf-8');
    }
    const result = await analyze(dir);
    assert.ok('real.ts' in result.files);
    assert.ok(!Object.keys(result.files).some((f) => f.includes('output.ts')));
  });
});

describe('analyzer - glob fallback extension handling', () => {
  it('includes .tsx, .jsx, .cts, and .mts files in glob fallback', async () => {
    const dir = fs.mkdtempSync(path.join(tmpDir, 'ext-'));
    fs.writeFileSync(path.join(dir, 'a.tsx'), 'export class Tsx {}', 'utf-8');
    fs.writeFileSync(path.join(dir, 'b.jsx'), 'function Jsx() {}', 'utf-8');
    fs.writeFileSync(path.join(dir, 'c.cts'), 'export class Cts {}', 'utf-8');
    fs.writeFileSync(path.join(dir, 'd.mts'), 'export class Mts {}', 'utf-8');
    const result = await analyze(dir);
    const names = Object.keys(result.files);
    assert.ok(names.some((f) => f.endsWith('.tsx')));
    assert.ok(names.some((f) => f.endsWith('.jsx')));
    assert.ok(names.some((f) => f.endsWith('.cts')));
    assert.ok(names.some((f) => f.endsWith('.mts')));
  });
});
