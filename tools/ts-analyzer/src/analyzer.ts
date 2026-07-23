import ts from 'typescript';
import fs from 'fs';
import path from 'path';
import { visitFile } from './visitor.js';
import type { AnalysisResult, FileResult } from './models.js';

export async function analyze(projectRoot: string): Promise<AnalysisResult> {
  const normalizedRoot = path.resolve(projectRoot);
  const projectName = readProjectName(normalizedRoot);

  // ts.findConfigFile walks UP the directory tree with no ceiling. If normalizedRoot has no
  // tsconfig.json of its own, it will keep ascending past the project root -- potentially
  // reaching a home-directory or system-level tsconfig -- and that ancestor's `include`/`files`
  // patterns would then be parsed and enumerated (via ts.createProgram below) across a directory
  // tree far broader than this project, before the startsWith(normalizedRoot) filter at output
  // time discards the irrelevant results. Reject any config found outside normalizedRoot so we
  // fall back to the bounded glob instead.
  const foundConfigPath = ts.findConfigFile(normalizedRoot, ts.sys.fileExists, 'tsconfig.json');
  const configPath = foundConfigPath && path.resolve(foundConfigPath).startsWith(normalizedRoot) ? foundConfigPath : undefined;

  let compilerOptions: ts.CompilerOptions;
  let rootFileNames: string[];

  if (configPath) {
    const { config, error } = ts.readConfigFile(configPath, ts.sys.readFile);
    if (error) {
      process.stderr.write(`Warning: Could not read tsconfig.json: ${ts.flattenDiagnosticMessageText(error.messageText, '\n')}\n`);
    }
    const parsed = ts.parseJsonConfigFileContent(config ?? {}, ts.sys, path.dirname(configPath));
    compilerOptions = { ...parsed.options, noEmit: true, skipLibCheck: true, allowJs: true };
    rootFileNames = parsed.fileNames.filter((f) => !f.endsWith('.d.ts'));
  } else {
    rootFileNames = globSourceFiles(normalizedRoot);
    compilerOptions = {
      target: ts.ScriptTarget.Latest,
      allowJs: true,
      checkJs: false,
      noEmit: true,
      skipLibCheck: true,
    };
  }

  const program = ts.createProgram(rootFileNames, compilerOptions);
  const files: Record<string, FileResult> = {};

  for (const sourceFile of program.getSourceFiles()) {
    if (sourceFile.isDeclarationFile) continue;
    if (!sourceFile.fileName.startsWith(normalizedRoot)) continue;
    if (sourceFile.fileName.includes('node_modules')) continue;
    if (isGeneratedFile(sourceFile.fileName)) continue;

    const relativePath = path.relative(normalizedRoot, sourceFile.fileName).replace(/\\/g, '/');
    try {
      files[relativePath] = visitFile(sourceFile, relativePath, projectName);
    } catch (e) {
      process.stderr.write(`Warning: Error analyzing ${relativePath}: ${e}\n`);
    }
  }

  return { projectName, projectRoot: normalizedRoot, files };
}

function readProjectName(projectRoot: string): string {
  const pkgPath = path.join(projectRoot, 'package.json');
  if (fs.existsSync(pkgPath)) {
    try {
      const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf-8')) as { name?: string };
      if (pkg.name) return pkg.name;
    } catch {
      // fall through
    }
  }
  return path.basename(projectRoot);
}

function globSourceFiles(dir: string): string[] {
  const results: string[] = [];
  const skipDirs = new Set(['node_modules', 'dist', 'build', '.next', '.nuxt', 'coverage', '.git']);
  const visited = new Set<string>();

  function walk(current: string): void {
    let real: string;
    try {
      real = fs.realpathSync(current);
    } catch {
      return;
    }
    if (visited.has(real)) return;
    visited.add(real);

    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(current, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      if (entry.isDirectory()) {
        if (!skipDirs.has(entry.name) && !entry.name.startsWith('.')) {
          walk(path.join(current, entry.name));
        }
      } else if (entry.isFile()) {
        const ext = path.extname(entry.name);
        if (['.ts', '.tsx', '.cts', '.mts', '.js', '.jsx'].includes(ext) && !entry.name.endsWith('.d.ts')) {
          results.push(path.join(current, entry.name));
        }
      }
    }
  }

  walk(dir);
  return results;
}

function isGeneratedFile(filePath: string): boolean {
  const name = path.basename(filePath);
  return (
    name.endsWith('.generated.ts') ||
    name.endsWith('.gen.ts') ||
    name.endsWith('.generated.js') ||
    name.endsWith('.gen.js') ||
    filePath.includes('/dist/') ||
    filePath.includes('/build/') ||
    filePath.includes('/.next/') ||
    filePath.includes('/coverage/')
  );
}
