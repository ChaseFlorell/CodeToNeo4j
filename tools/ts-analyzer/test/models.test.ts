import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import type { AnalysisResult, FileResult, RelationshipInfo, SymbolInfo } from '../src/models.js';

describe('models - JSON round-trip', () => {
  it('serializes SymbolInfo with all fields', () => {
    const symbol: SymbolInfo = {
      name: 'MyClass',
      kind: 'TypeScriptClass',
      class: 'class',
      fqn: '@my-pkg/src/foo.ts::MyClass',
      accessibility: 'Public',
      startLine: 1,
      endLine: 10,
      documentation: '/** A class */',
      comments: '// comment',
      namespace: '@my-pkg/src',
      containingClass: null,
    };
    const json = JSON.stringify(symbol);
    const parsed = JSON.parse(json) as SymbolInfo;
    assert.deepEqual(parsed, symbol);
  });

  it('serializes SymbolInfo with null optional fields', () => {
    const symbol: SymbolInfo = {
      name: 'fn',
      kind: 'TypeScriptFunction',
      class: 'function',
      fqn: '@pkg/src/a.ts::fn',
      accessibility: 'Public',
      startLine: 5,
      endLine: 8,
      documentation: null,
      comments: null,
      namespace: '@pkg/src',
      containingClass: null,
    };
    const json = JSON.stringify(symbol);
    const parsed = JSON.parse(json) as SymbolInfo;
    assert.equal(parsed.documentation, null);
    assert.equal(parsed.comments, null);
    assert.equal(parsed.containingClass, null);
  });

  it('serializes RelationshipInfo with null toLine and toFile', () => {
    const rel: RelationshipInfo = {
      fromSymbol: 'A',
      fromKind: 'class',
      fromLine: 1,
      toSymbol: 'B',
      toKind: 'class',
      toLine: null,
      toFile: null,
      relType: 'src__DEPENDS_ON',
    };
    const json = JSON.stringify(rel);
    const parsed = JSON.parse(json) as RelationshipInfo;
    assert.equal(parsed.toLine, null);
    assert.equal(parsed.toFile, null);
  });

  it('serializes AnalysisResult with nested files', () => {
    const result: AnalysisResult = {
      projectName: 'my-app',
      projectRoot: '/home/user/my-app',
      files: {
        'src/foo.ts': {
          symbols: [
            {
              name: 'Foo',
              kind: 'TypeScriptClass',
              class: 'class',
              fqn: '@my-app/src/foo.ts::Foo',
              accessibility: 'Public',
              startLine: 1,
              endLine: 5,
              documentation: null,
              comments: null,
              namespace: '@my-app/src',
              containingClass: null,
            },
          ],
          relationships: [],
        },
      },
    };
    const json = JSON.stringify(result);
    const parsed = JSON.parse(json) as AnalysisResult;
    assert.equal(parsed.projectName, 'my-app');
    assert.equal(Object.keys(parsed.files).length, 1);
    assert.equal(parsed.files['src/foo.ts']?.symbols.length, 1);
    assert.equal(parsed.files['src/foo.ts']?.symbols[0]?.name, 'Foo');
  });

  it('serializes FileResult with multiple symbols and relationships', () => {
    const fileResult: FileResult = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'TypeScriptClass',
          class: 'class',
          fqn: '@pkg/src/a.ts::MyClass',
          accessibility: 'Public',
          startLine: 1,
          endLine: 20,
          documentation: null,
          comments: null,
          namespace: '@pkg/src',
          containingClass: null,
        },
        {
          name: 'myMethod',
          kind: 'TypeScriptMethod',
          class: 'method',
          fqn: '@pkg/src/a.ts::MyClass.myMethod',
          accessibility: 'Public',
          startLine: 3,
          endLine: 5,
          documentation: null,
          comments: null,
          namespace: '@pkg/src',
          containingClass: 'MyClass',
        },
      ],
      relationships: [
        {
          fromSymbol: 'MyClass',
          fromKind: 'class',
          fromLine: 1,
          toSymbol: 'myMethod',
          toKind: 'method',
          toLine: null,
          toFile: null,
          relType: 'src__CONTAINS',
        },
      ],
    };
    const parsed = JSON.parse(JSON.stringify(fileResult)) as FileResult;
    assert.equal(parsed.symbols.length, 2);
    assert.equal(parsed.relationships.length, 1);
    assert.equal(parsed.relationships[0]?.relType, 'src__CONTAINS');
  });
});
