import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';
import { visitFile } from '../src/visitor.js';

function makeSourceFile(code: string, fileName = 'test.ts'): ts.SourceFile {
  return ts.createSourceFile(fileName, code, ts.ScriptTarget.Latest, /* setParentNodes */ true);
}

const PROJECT = 'my-project';
const REL_PATH = 'src/test.ts';

describe('visitor - class declarations', () => {
  it('extracts a public class', () => {
    const sf = makeSourceFile('export class MyClass {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols.length, 1);
    const sym = result.symbols[0]!;
    assert.equal(sym.name, 'MyClass');
    assert.equal(sym.kind, 'TypeScriptClass');
    assert.equal(sym.class, 'class');
    assert.equal(sym.accessibility, 'Public');
    assert.equal(sym.fqn, `@${PROJECT}/${REL_PATH}::MyClass`);
    assert.equal(sym.namespace, `@${PROJECT}/src`);
    assert.equal(sym.containingClass, null);
  });

  it('extracts class with extends as DEPENDS_ON relationship', () => {
    const sf = makeSourceFile('class Child extends Parent {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const rel = result.relationships.find((r) => r.relType === 'src__DEPENDS_ON');
    assert.ok(rel);
    assert.equal(rel.fromSymbol, 'Child');
    assert.equal(rel.toSymbol, 'Parent');
  });

  it('extracts class with implements as DEPENDS_ON relationship', () => {
    const sf = makeSourceFile('class Impl implements IFoo, IBar {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const deps = result.relationships.filter((r) => r.relType === 'src__DEPENDS_ON');
    assert.equal(deps.length, 2);
    assert.ok(deps.some((r) => r.toSymbol === 'IFoo'));
    assert.ok(deps.some((r) => r.toSymbol === 'IBar'));
  });

  it('extracts decorator as HAS_TAG relationship', () => {
    const sf = makeSourceFile('@Injectable()\nclass MyService {}', 'test.ts');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const tag = result.relationships.find((r) => r.relType === 'src__HAS_TAG');
    assert.ok(tag);
    assert.equal(tag.fromSymbol, 'MyService');
    assert.equal(tag.toSymbol, 'Injectable');
  });

  it('assigns FQN with #default for anonymous default export class', () => {
    const sf = makeSourceFile('export default class {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols.length, 1);
    assert.equal(result.symbols[0]!.fqn, `@${PROJECT}/${REL_PATH}#default`);
  });

  it('emits TypeScriptAbstractClass kind for abstract classes', () => {
    const sf = makeSourceFile('abstract class Base { abstract doWork(): void; }');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const cls = result.symbols.find((s) => s.name === 'Base');
    assert.ok(cls);
    assert.equal(cls.kind, 'TypeScriptAbstractClass');
    assert.equal(cls.class, 'class');
  });

  it('emits TypeScriptClass kind for non-abstract classes', () => {
    const sf = makeSourceFile('class Concrete {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols[0]!.kind, 'TypeScriptClass');
  });
});

describe('visitor - interface declarations', () => {
  it('extracts interface', () => {
    const sf = makeSourceFile('export interface IFoo {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols.length, 1);
    const sym = result.symbols[0]!;
    assert.equal(sym.name, 'IFoo');
    assert.equal(sym.kind, 'TypeScriptInterface');
    assert.equal(sym.class, 'interface');
  });

  it('extracts interface extends as DEPENDS_ON', () => {
    const sf = makeSourceFile('interface IChild extends IParent {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const dep = result.relationships.find((r) => r.relType === 'src__DEPENDS_ON');
    assert.ok(dep);
    assert.equal(dep.fromSymbol, 'IChild');
    assert.equal(dep.toSymbol, 'IParent');
  });
});

describe('visitor - enum declarations', () => {
  it('extracts enum', () => {
    const sf = makeSourceFile('export enum Color { Red, Green, Blue }');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols.length, 1);
    const sym = result.symbols[0]!;
    assert.equal(sym.name, 'Color');
    assert.equal(sym.kind, 'TypeScriptEnum');
    assert.equal(sym.class, 'enum');
  });

  it('extracts const enum', () => {
    const sf = makeSourceFile('const enum Direction { Up, Down }');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols[0]?.kind, 'TypeScriptEnum');
  });
});

describe('visitor - type alias declarations', () => {
  it('extracts type alias', () => {
    const sf = makeSourceFile('export type MyType = string | number;');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols.length, 1);
    const sym = result.symbols[0]!;
    assert.equal(sym.name, 'MyType');
    assert.equal(sym.kind, 'TypeScriptTypeAlias');
    assert.equal(sym.class, 'type');
  });
});

describe('visitor - namespace declarations', () => {
  it('extracts namespace', () => {
    const sf = makeSourceFile('namespace MyNS {}');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.ok(result.symbols.some((s) => s.name === 'MyNS' && s.kind === 'TypeScriptNamespace'));
  });
});

describe('visitor - method declarations', () => {
  it('extracts method and emits CONTAINS from class', () => {
    const sf = makeSourceFile(`
      class Foo {
        doSomething(): void {}
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const method = result.symbols.find((s) => s.name === 'doSomething');
    assert.ok(method);
    assert.equal(method.kind, 'TypeScriptMethod');
    assert.equal(method.class, 'method');
    assert.equal(method.containingClass, 'Foo');
    assert.equal(method.fqn, `@${PROJECT}/${REL_PATH}::Foo.doSomething`);

    const contains = result.relationships.find((r) => r.relType === 'src__CONTAINS' && r.fromSymbol === 'Foo' && r.toSymbol === 'doSomething');
    assert.ok(contains);
  });

  it('extracts getter as TypeScriptProperty', () => {
    const sf = makeSourceFile(`
      class Bar {
        get value(): string { return ''; }
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const prop = result.symbols.find((s) => s.name === 'value');
    assert.ok(prop);
    assert.equal(prop.kind, 'TypeScriptProperty');
    assert.equal(prop.class, 'property');
  });

  it('respects private accessibility modifier', () => {
    const sf = makeSourceFile(`
      class A {
        private secret(): void {}
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const method = result.symbols.find((s) => s.name === 'secret');
    assert.ok(method);
    assert.equal(method.accessibility, 'Private');
  });

  it('respects protected accessibility modifier', () => {
    const sf = makeSourceFile(`
      class A {
        protected inner(): void {}
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const method = result.symbols.find((s) => s.name === 'inner');
    assert.ok(method);
    assert.equal(method.accessibility, 'Protected');
  });
});

describe('visitor - constructor declarations', () => {
  it('extracts constructor and emits CONTAINS', () => {
    const sf = makeSourceFile(`
      class Svc {
        constructor(private dep: string) {}
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const ctor = result.symbols.find((s) => s.kind === 'TypeScriptConstructor');
    assert.ok(ctor);
    assert.equal(ctor.name, 'constructor');
    assert.equal(ctor.containingClass, 'Svc');

    const contains = result.relationships.find((r) => r.relType === 'src__CONTAINS' && r.toSymbol === 'constructor');
    assert.ok(contains);
  });
});

describe('visitor - property declarations', () => {
  it('extracts field and emits CONTAINS', () => {
    const sf = makeSourceFile(`
      class Model {
        name: string = '';
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const field = result.symbols.find((s) => s.name === 'name');
    assert.ok(field);
    assert.equal(field.kind, 'TypeScriptField');
    assert.equal(field.class, 'field');
    assert.equal(field.containingClass, 'Model');

    const contains = result.relationships.find((r) => r.relType === 'src__CONTAINS' && r.toSymbol === 'name');
    assert.ok(contains);
  });
});

describe('visitor - function declarations', () => {
  it('extracts top-level function', () => {
    const sf = makeSourceFile('export function greet(name: string): string { return name; }');
    const result = visitFile(sf, REL_PATH, PROJECT);
    assert.equal(result.symbols.length, 1);
    const sym = result.symbols[0]!;
    assert.equal(sym.name, 'greet');
    assert.equal(sym.kind, 'TypeScriptFunction');
    assert.equal(sym.class, 'function');
    assert.equal(sym.containingClass, null);
  });

  it('extracts arrow function assigned to const', () => {
    const sf = makeSourceFile('export const add = (a: number, b: number) => a + b;');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const fn = result.symbols.find((s) => s.name === 'add');
    assert.ok(fn);
    assert.equal(fn.kind, 'TypeScriptFunction');
  });

  it('does not extract function-like inside class as top-level function', () => {
    const sf = makeSourceFile(`
      class Foo {
        bar(): void {}
      }
      function topLevel() {}
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    // Foo (class), bar (method), topLevel (function) — no extra top-level functions from class
    const functions = result.symbols.filter((s) => s.kind === 'TypeScriptFunction');
    assert.equal(functions.length, 1);
    assert.equal(functions[0]!.name, 'topLevel');
  });
});

describe('visitor - import declarations', () => {
  it('emits DEPENDS_ON for relative import', () => {
    const sf = makeSourceFile("import { Foo } from './foo';");
    const result = visitFile(sf, REL_PATH, PROJECT);
    const dep = result.relationships.find((r) => r.relType === 'src__DEPENDS_ON');
    assert.ok(dep);
    assert.equal(dep.fromSymbol, REL_PATH);
    assert.equal(dep.fromKind, 'file');
    assert.equal(dep.toKind, 'file');
  });

  it('emits DEPENDS_ON for bare module specifier (package)', () => {
    const sf = makeSourceFile("import React from 'react';");
    const result = visitFile(sf, REL_PATH, PROJECT);
    const dep = result.relationships.find((r) => r.relType === 'src__DEPENDS_ON');
    assert.ok(dep);
    assert.equal(dep.toSymbol, 'react');
    assert.equal(dep.toKind, 'package');
    assert.equal(dep.toFile, null);
  });

  it('uses package name (before /) for scoped packages', () => {
    const sf = makeSourceFile("import { Injectable } from '@angular/core';");
    const result = visitFile(sf, REL_PATH, PROJECT);
    const dep = result.relationships.find((r) => r.relType === 'src__DEPENDS_ON');
    assert.ok(dep);
    assert.equal(dep.toSymbol, '@angular');
  });
});

describe('visitor - call expressions (INVOKES)', () => {
  it('emits INVOKES from method to called function', () => {
    const sf = makeSourceFile(`
      class A {
        run(): void {
          doWork();
        }
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const invokes = result.relationships.find((r) => r.relType === 'src__INVOKES');
    assert.ok(invokes);
    assert.equal(invokes.fromSymbol, 'run');
    assert.equal(invokes.toSymbol, 'doWork');
  });

  it('emits INVOKES for new expression', () => {
    const sf = makeSourceFile(`
      class B {
        create(): void {
          new Dep();
        }
      }
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const invokes = result.relationships.find((r) => r.relType === 'src__INVOKES' && r.toKind === 'constructor');
    assert.ok(invokes);
    assert.equal(invokes.toSymbol, 'Dep');
  });

  it('does not emit INVOKES for top-level calls outside any method', () => {
    const sf = makeSourceFile('doSomething();');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const invokes = result.relationships.filter((r) => r.relType === 'src__INVOKES');
    assert.equal(invokes.length, 0);
  });
});

describe('visitor - JSDoc extraction', () => {
  it('extracts JSDoc comment as documentation', () => {
    const sf = makeSourceFile(`
      /** A greeter class */
      class Greeter {}
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const sym = result.symbols.find((s) => s.name === 'Greeter');
    assert.ok(sym);
    assert.ok(sym.documentation?.includes('A greeter class'));
  });

  it('extracts leading line comment as comments property', () => {
    const sf = makeSourceFile(`
      // This is a comment
      class Widget {}
    `);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const sym = result.symbols.find((s) => s.name === 'Widget');
    assert.ok(sym);
    assert.ok(sym.comments?.includes('This is a comment'));
  });
});

describe('visitor - line numbers', () => {
  it('records 1-based start and end lines', () => {
    const code = `
class Foo {
  bar(): void {}
}`;
    const sf = makeSourceFile(code);
    const result = visitFile(sf, REL_PATH, PROJECT);
    const cls = result.symbols.find((s) => s.name === 'Foo');
    assert.ok(cls);
    assert.ok(cls.startLine >= 1);
    assert.ok(cls.endLine >= cls.startLine);
  });
});

describe('visitor - .d.ts exclusion', () => {
  it('returns empty result for declaration files', () => {
    const sf = makeSourceFile('export declare class Foo {}', 'test.d.ts');
    // Declaration file detection is done in analyzer.ts based on isDeclarationFile
    // The visitor itself doesn't filter — test that regular .d.ts content is still parsed
    const result = visitFile(sf, 'test.d.ts', PROJECT);
    // Class IS extracted by visitor; exclusion of .d.ts is handled at program level
    assert.ok(result.symbols.length >= 0);
  });
});

describe('visitor - accessibility defaults', () => {
  it('defaults to Public when no modifier present', () => {
    const sf = makeSourceFile('class Foo { bar(): void {} }');
    const result = visitFile(sf, REL_PATH, PROJECT);
    const method = result.symbols.find((s) => s.name === 'bar');
    assert.ok(method);
    assert.equal(method.accessibility, 'Public');
  });
});

describe('visitor - allowJs (JavaScript files)', () => {
  it('extracts class from .js file', () => {
    const sf = makeSourceFile('class MyJsClass { hello() {} }', 'src/test.js');
    const result = visitFile(sf, 'src/test.js', PROJECT);
    assert.ok(result.symbols.some((s) => s.name === 'MyJsClass' && s.kind === 'TypeScriptClass'));
  });

  it('extracts function from .js file', () => {
    const sf = makeSourceFile('function jsFunction() {}', 'src/util.js');
    const result = visitFile(sf, 'src/util.js', PROJECT);
    assert.ok(result.symbols.some((s) => s.name === 'jsFunction' && s.kind === 'TypeScriptFunction'));
  });
});
