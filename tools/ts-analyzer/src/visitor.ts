import ts from 'typescript';
import path from 'path';
import type { FileResult, RelationshipInfo, SymbolInfo } from './models.js';
import { GraphSchema } from './schema.js';

interface VisitorContext {
  readonly sourceFile: ts.SourceFile;
  readonly relativePath: string;
  readonly projectName: string;
  readonly currentClass: string | null;
  readonly currentClassKind: string | null;
  readonly currentMethod: string | null;
  readonly symbols: SymbolInfo[];
  readonly relationships: RelationshipInfo[];
}

export function visitFile(
  sourceFile: ts.SourceFile,
  relativePath: string,
  projectName: string,
): FileResult {
  const ctx: VisitorContext = {
    sourceFile,
    relativePath,
    projectName,
    currentClass: null,
    currentClassKind: null,
    currentMethod: null,
    symbols: [],
    relationships: [],
  };
  ts.forEachChild(sourceFile, (node) => visitNode(node, ctx));
  return { symbols: ctx.symbols, relationships: ctx.relationships };
}

function visitNode(node: ts.Node, ctx: VisitorContext): void {
  if (ts.isClassDeclaration(node)) {
    handleClass(node, ctx);
    return;
  }
  if (ts.isInterfaceDeclaration(node)) {
    handleInterface(node, ctx);
    return;
  }
  if (ts.isEnumDeclaration(node)) {
    handleEnum(node, ctx);
    return;
  }
  if (ts.isTypeAliasDeclaration(node)) {
    handleTypeAlias(node, ctx);
    return;
  }
  if (ts.isModuleDeclaration(node)) {
    handleNamespace(node, ctx);
    return;
  }
  if (ts.isMethodDeclaration(node) || ts.isGetAccessorDeclaration(node) || ts.isSetAccessorDeclaration(node)) {
    handleMethod(node, ctx);
    return;
  }
  if (ts.isConstructorDeclaration(node)) {
    handleConstructor(node, ctx);
    return;
  }
  if (ts.isPropertyDeclaration(node)) {
    handleProperty(node, ctx);
    return;
  }
  if (ts.isFunctionDeclaration(node)) {
    if (!ctx.currentClass) {
      handleFunction(node, ctx);
    }
    return;
  }
  if (ts.isVariableStatement(node) && !ctx.currentClass) {
    handleVariableStatement(node, ctx);
    return;
  }
  if (ts.isImportDeclaration(node)) {
    handleImport(node, ctx);
    return;
  }
  if (ts.isCallExpression(node) && ctx.currentMethod != null) {
    handleCall(node, ctx);
    ts.forEachChild(node, (child) => visitNode(child, ctx));
    return;
  }
  if (ts.isNewExpression(node) && ctx.currentMethod != null) {
    handleNew(node, ctx);
    ts.forEachChild(node, (child) => visitNode(child, ctx));
    return;
  }

  ts.forEachChild(node, (child) => visitNode(child, ctx));
}

// ---- Symbol handlers ----

function handleClass(node: ts.ClassDeclaration, ctx: VisitorContext): void {
  const name = node.name?.text ?? 'default';
  const fqn = node.name ? buildFqn(ctx, name) : `@${ctx.projectName}/${ctx.relativePath}#default`;
  const isAbstract = node.modifiers?.some((m) => m.kind === ts.SyntaxKind.AbstractKeyword) ?? false;

  ctx.symbols.push({
    name,
    kind: isAbstract ? 'TypeScriptAbstractClass' : 'TypeScriptClass',
    class: 'class',
    fqn,
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: null,
  });

  for (const clause of node.heritageClauses ?? []) {
    const relKind = clause.token === ts.SyntaxKind.ExtendsKeyword ? 'class' : 'interface';
    for (const type of clause.types) {
      const typeName = type.expression.getText(ctx.sourceFile);
      ctx.relationships.push(makeRelationship(name, 'class', getLine(node, ctx.sourceFile), typeName, relKind, GraphSchema.dependsOn, ctx));
    }
  }

  for (const decorator of ts.getDecorators(node) ?? []) {
    const decoratorName = getDecoratorName(decorator, ctx.sourceFile);
    if (decoratorName) {
      ctx.relationships.push(makeRelationship(name, 'class', getLine(node, ctx.sourceFile), decoratorName, 'decorator', GraphSchema.hasTag, ctx));
    }
  }

  const classCtx: VisitorContext = { ...ctx, currentClass: name, currentClassKind: 'class', currentMethod: null };
  ts.forEachChild(node, (child) => visitNode(child, classCtx));
}

function handleInterface(node: ts.InterfaceDeclaration, ctx: VisitorContext): void {
  const name = node.name.text;
  ctx.symbols.push({
    name,
    kind: 'TypeScriptInterface',
    class: 'interface',
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: null,
  });

  for (const clause of node.heritageClauses ?? []) {
    for (const type of clause.types) {
      const typeName = type.expression.getText(ctx.sourceFile);
      ctx.relationships.push(makeRelationship(name, 'interface', getLine(node, ctx.sourceFile), typeName, 'interface', GraphSchema.dependsOn, ctx));
    }
  }
}

function handleEnum(node: ts.EnumDeclaration, ctx: VisitorContext): void {
  const name = node.name.text;
  ctx.symbols.push({
    name,
    kind: 'TypeScriptEnum',
    class: 'enum',
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: null,
  });
}

function handleTypeAlias(node: ts.TypeAliasDeclaration, ctx: VisitorContext): void {
  const name = node.name.text;
  ctx.symbols.push({
    name,
    kind: 'TypeScriptTypeAlias',
    class: 'type',
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: null,
  });
}

function handleNamespace(node: ts.ModuleDeclaration, ctx: VisitorContext): void {
  if (typeof node.name.text !== 'string') return;
  const name = node.name.text;
  ctx.symbols.push({
    name,
    kind: 'TypeScriptNamespace',
    class: 'namespace',
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: null,
  });

  const nsCtx: VisitorContext = { ...ctx, currentClass: name, currentClassKind: 'namespace', currentMethod: null };
  if (node.body) {
    ts.forEachChild(node.body, (child) => visitNode(child, nsCtx));
  }
}

function handleMethod(
  node: ts.MethodDeclaration | ts.GetAccessorDeclaration | ts.SetAccessorDeclaration,
  ctx: VisitorContext,
): void {
  const name = getNodeName(node.name, ctx.sourceFile);
  if (!name) return;

  const isAccessor = ts.isGetAccessorDeclaration(node) || ts.isSetAccessorDeclaration(node);
  const kind = isAccessor ? 'TypeScriptProperty' : 'TypeScriptMethod';
  const classStr = isAccessor ? 'property' : 'method';

  ctx.symbols.push({
    name,
    kind,
    class: classStr,
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: ctx.currentClass,
  });

  if (ctx.currentClass) {
    ctx.relationships.push(makeRelationship(ctx.currentClass, ctx.currentClassKind!, getLine(node, ctx.sourceFile), name, classStr, GraphSchema.contains, ctx));
  }

  if (node.body) {
    const methodCtx: VisitorContext = { ...ctx, currentMethod: name };
    ts.forEachChild(node.body, (child) => visitNode(child, methodCtx));
  }
}

function handleConstructor(node: ts.ConstructorDeclaration, ctx: VisitorContext): void {
  const name = 'constructor';
  ctx.symbols.push({
    name,
    kind: 'TypeScriptConstructor',
    class: 'constructor',
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: ctx.currentClass,
  });

  if (ctx.currentClass) {
    ctx.relationships.push(makeRelationship(ctx.currentClass, ctx.currentClassKind!, getLine(node, ctx.sourceFile), name, 'constructor', GraphSchema.contains, ctx));
  }

  if (node.body) {
    const methodCtx: VisitorContext = { ...ctx, currentMethod: name };
    ts.forEachChild(node.body, (child) => visitNode(child, methodCtx));
  }
}

function handleProperty(node: ts.PropertyDeclaration, ctx: VisitorContext): void {
  const name = getNodeName(node.name, ctx.sourceFile);
  if (!name) return;

  ctx.symbols.push({
    name,
    kind: 'TypeScriptField',
    class: 'field',
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: ctx.currentClass,
  });

  if (ctx.currentClass) {
    ctx.relationships.push(makeRelationship(ctx.currentClass, ctx.currentClassKind!, getLine(node, ctx.sourceFile), name, 'field', GraphSchema.contains, ctx));
  }
}

function handleFunction(node: ts.FunctionDeclaration, ctx: VisitorContext): void {
  const name = node.name?.text;
  if (!name) return;

  ctx.symbols.push({
    name,
    kind: 'TypeScriptFunction',
    class: 'function',
    fqn: buildFqn(ctx, name),
    accessibility: getAccessibility(node),
    startLine: getLine(node, ctx.sourceFile),
    endLine: getEndLine(node, ctx.sourceFile),
    documentation: getJsDoc(node, ctx.sourceFile),
    comments: getLeadingComments(node, ctx.sourceFile),
    namespace: buildNamespace(ctx),
    containingClass: null,
  });

  if (node.body) {
    const fnCtx: VisitorContext = { ...ctx, currentMethod: name };
    ts.forEachChild(node.body, (child) => visitNode(child, fnCtx));
  }
}

function handleVariableStatement(node: ts.VariableStatement, ctx: VisitorContext): void {
  for (const decl of node.declarationList.declarations) {
    if (!ts.isIdentifier(decl.name)) continue;
    const name = decl.name.text;
    const init = decl.initializer;
    if (!init || (!ts.isArrowFunction(init) && !ts.isFunctionExpression(init))) continue;

    ctx.symbols.push({
      name,
      kind: 'TypeScriptFunction',
      class: 'function',
      fqn: buildFqn(ctx, name),
      accessibility: getAccessibility(node),
      startLine: getLine(decl, ctx.sourceFile),
      endLine: getEndLine(decl, ctx.sourceFile),
      documentation: getJsDoc(node, ctx.sourceFile),
      comments: getLeadingComments(node, ctx.sourceFile),
      namespace: buildNamespace(ctx),
      containingClass: null,
    });

    if (init.body && !ts.isToken(init.body)) {
      const fnCtx: VisitorContext = { ...ctx, currentMethod: name };
      ts.forEachChild(init.body, (child) => visitNode(child, fnCtx));
    }
  }
}

function handleImport(node: ts.ImportDeclaration, ctx: VisitorContext): void {
  if (!ts.isStringLiteral(node.moduleSpecifier)) return;
  const specifier = node.moduleSpecifier.text;

  const isRelative = specifier.startsWith('.') || specifier.startsWith('/');
  const toFile = isRelative ? resolveRelativePath(ctx.relativePath, specifier) : null;
  const toSymbol = isRelative ? (toFile ?? specifier) : specifier.split('/')[0];

  ctx.relationships.push({
    fromSymbol: ctx.relativePath,
    fromKind: 'file',
    fromLine: getLine(node, ctx.sourceFile),
    toSymbol,
    toKind: isRelative ? 'file' : 'package',
    toLine: null,
    toFile,
    relType: GraphSchema.dependsOn,
  });
}

function handleCall(node: ts.CallExpression, ctx: VisitorContext): void {
  const toSymbol = getCallTarget(node, ctx.sourceFile);
  if (!toSymbol) return;

  ctx.relationships.push({
    fromSymbol: ctx.currentMethod!,
    fromKind: 'method',
    fromLine: getLine(node, ctx.sourceFile),
    toSymbol,
    toKind: 'method',
    toLine: null,
    toFile: null,
    relType: GraphSchema.invokes,
  });
}

function handleNew(node: ts.NewExpression, ctx: VisitorContext): void {
  const expr = node.expression;
  const toSymbol = ts.isIdentifier(expr) ? expr.text : null;
  if (!toSymbol) return;

  ctx.relationships.push({
    fromSymbol: ctx.currentMethod!,
    fromKind: 'method',
    fromLine: getLine(node, ctx.sourceFile),
    toSymbol,
    toKind: 'constructor',
    toLine: null,
    toFile: null,
    relType: GraphSchema.invokes,
  });
}

// ---- Utility functions ----

function buildFqn(ctx: VisitorContext, name: string): string {
  const prefix = `@${ctx.projectName}/${ctx.relativePath}`;
  if (ctx.currentClass) return `${prefix}::${ctx.currentClass}.${name}`;
  return `${prefix}::${name}`;
}

function buildNamespace(ctx: VisitorContext): string {
  const dir = path.dirname(ctx.relativePath).replace(/\\/g, '/');
  return `@${ctx.projectName}/${dir === '.' ? '' : dir}`;
}

function getLine(node: ts.Node, sf: ts.SourceFile): number {
  return sf.getLineAndCharacterOfPosition(node.getStart(sf)).line + 1;
}

function getEndLine(node: ts.Node, sf: ts.SourceFile): number {
  return sf.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
}

function getAccessibility(node: ts.Node): string {
  // ts.getModifiers only accepts HasModifiers; guard with a type narrowing cast
  if (!('modifiers' in node)) return 'Public';
  const mods = ts.getModifiers(node as ts.HasModifiers);
  if (mods?.some((m) => m.kind === ts.SyntaxKind.PrivateKeyword)) return 'Private';
  if (mods?.some((m) => m.kind === ts.SyntaxKind.ProtectedKeyword)) return 'Protected';
  return 'Public';
}

function getJsDoc(node: ts.Node, sf: ts.SourceFile): string | null {
  const jsDocNodes = (node as ts.Node & { jsDoc?: ts.JSDoc[] }).jsDoc;
  if (!jsDocNodes?.length) return null;
  return jsDocNodes.map((jd) => jd.getText(sf)).join('\n') || null;
}

function getLeadingComments(node: ts.Node, sf: ts.SourceFile): string | null {
  const fullText = sf.getFullText();
  const ranges = ts.getLeadingCommentRanges(fullText, node.getFullStart());
  if (!ranges?.length) return null;
  const comments = ranges
    .filter((r) => {
      const text = fullText.substring(r.pos, r.end);
      return !text.startsWith('/**');
    })
    .map((r) => fullText.substring(r.pos, r.end));
  return comments.length ? comments.join('\n') : null;
}

function getNodeName(name: ts.PropertyName, sf: ts.SourceFile): string | null {
  if (ts.isIdentifier(name)) return name.text;
  if (ts.isStringLiteral(name)) return name.text;
  if (ts.isNumericLiteral(name)) return name.text;
  if (ts.isComputedPropertyName(name)) return name.expression.getText(sf);
  return null;
}

function getDecoratorName(decorator: ts.Decorator, sf: ts.SourceFile): string | null {
  const expr = decorator.expression;
  if (ts.isIdentifier(expr)) return expr.text;
  if (ts.isCallExpression(expr) && ts.isIdentifier(expr.expression)) return expr.expression.text;
  return null;
}

function getCallTarget(node: ts.CallExpression, sf: ts.SourceFile): string | null {
  const expr = node.expression;
  if (ts.isIdentifier(expr)) return expr.text;
  if (ts.isPropertyAccessExpression(expr)) return expr.name.text;
  return null;
}

function resolveRelativePath(fromFile: string, specifier: string): string {
  const fromDir = path.dirname(fromFile);
  const resolved = path.join(fromDir, specifier).replace(/\\/g, '/');
  // Add .ts extension if no extension present
  if (!path.extname(resolved)) return `${resolved}.ts`;
  return resolved;
}

function makeRelationship(
  fromSymbol: string,
  fromKind: string,
  fromLine: number,
  toSymbol: string,
  toKind: string,
  relType: string,
  ctx: VisitorContext,
): RelationshipInfo {
  return { fromSymbol, fromKind, fromLine, toSymbol, toKind, toLine: null, toFile: null, relType };
}
