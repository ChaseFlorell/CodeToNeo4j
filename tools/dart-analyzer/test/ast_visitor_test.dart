import 'package:analyzer/dart/analysis/utilities.dart';
import 'package:test/test.dart';

import '../lib/src/ast_visitor.dart';
import '../lib/src/models.dart';

DartAstVisitor _visit(String source, {
  String filePath = '/proj/lib/foo.dart',
  String projectRoot = '/proj',
  String packageName = 'my_pkg',
}) {
  final result = parseString(content: source);
  final visitor = DartAstVisitor(
    filePath: filePath,
    projectRoot: projectRoot,
    packageName: packageName,
  );
  result.unit.accept(visitor);
  return visitor;
}

SymbolInfo _symbol(DartAstVisitor v, String name) =>
    v.symbols.firstWhere((s) => s.name == name);

Iterable<RelationshipInfo> _rels(DartAstVisitor v, {
  String? from,
  String? to,
  String? type,
}) =>
    v.relationships.where((r) =>
        (from == null || r.fromSymbol == from) &&
        (to == null || r.toSymbol == to) &&
        (type == null || r.relType == type));

void main() {
  group('class declarations', () {
    test('extracts name, kind, and fqn', () {
      final v = _visit('class Foo {}');
      final s = _symbol(v, 'Foo');
      expect(s.kind, 'DartClass');
      expect(s.symbolClass, 'class');
      expect(s.fqn, contains('Foo'));
    });

    test('public class has Public accessibility', () {
      final v = _visit('class Foo {}');
      expect(_symbol(v, 'Foo').accessibility, 'Public');
    });

    test('private class has Private accessibility', () {
      final v = _visit('class _Foo {}');
      expect(_symbol(v, '_Foo').accessibility, 'Private');
    });

    test('extends produces DEPENDS_ON relationship', () {
      final v = _visit('class A {} class B extends A {}');
      final rel = _rels(v, from: 'B', to: 'A', type: 'DEPENDS_ON');
      expect(rel, hasLength(1));
    });

    test('implements produces DEPENDS_ON relationship', () {
      final v = _visit('abstract class I {} class C implements I {}');
      final rel = _rels(v, from: 'C', to: 'I', type: 'DEPENDS_ON');
      expect(rel, hasLength(1));
    });

    test('with (mixin) produces DEPENDS_ON relationship', () {
      final v = _visit('mixin M {} class C with M {}');
      final rel = _rels(v, from: 'C', to: 'M', type: 'DEPENDS_ON');
      expect(rel, hasLength(1));
    });

    test('multiple implements produce one DEPENDS_ON each', () {
      final v = _visit(
          'abstract class I1 {} abstract class I2 {} class C implements I1, I2 {}');
      expect(_rels(v, from: 'C', type: 'DEPENDS_ON'), hasLength(2));
    });
  });

  group('mixin declarations', () {
    test('extracts mixin as DartMixin', () {
      final v = _visit('mixin M {}');
      final s = _symbol(v, 'M');
      expect(s.kind, 'DartMixin');
      expect(s.symbolClass, 'mixin');
    });
  });

  group('enum declarations', () {
    test('extracts enum as DartEnum', () {
      final v = _visit('enum Color { red, green, blue }');
      final s = _symbol(v, 'Color');
      expect(s.kind, 'DartEnum');
      expect(s.symbolClass, 'enum');
    });
  });

  group('extension declarations', () {
    test('extracts named extension as DartExtension', () {
      final v = _visit('extension StringX on String {}');
      final s = _symbol(v, 'StringX');
      expect(s.kind, 'DartExtension');
      expect(s.symbolClass, 'extension');
    });
  });

  group('method declarations', () {
    test('regular method produces DartMethod symbol', () {
      final v = _visit('class C { void doThing() {} }');
      final s = _symbol(v, 'doThing');
      expect(s.kind, 'DartMethod');
      expect(s.containingClass, 'C');
    });

    test('method produces CONTAINS relationship from class', () {
      final v = _visit('class C { void doThing() {} }');
      expect(_rels(v, from: 'C', to: 'doThing', type: 'CONTAINS'), hasLength(1));
    });

    test('getter produces DartProperty symbol', () {
      final v = _visit('class C { int get value => 0; }');
      final s = _symbol(v, 'value');
      expect(s.kind, 'DartProperty');
    });

    test('setter produces DartProperty symbol', () {
      final v = _visit('class C { set value(int v) {} }');
      final s = _symbol(v, 'value');
      expect(s.kind, 'DartProperty');
    });

    test('operator produces DartOperator symbol', () {
      final v = _visit('class C { bool operator ==(Object o) => false; }');
      final s = v.symbols.firstWhere((s) => s.kind == 'DartOperator');
      expect(s.kind, 'DartOperator');
    });

    test('private method has Private accessibility', () {
      final v = _visit('class C { void _secret() {} }');
      expect(_symbol(v, '_secret').accessibility, 'Private');
    });
  });

  group('constructor declarations', () {
    test('unnamed constructor produces DartConstructor', () {
      final v = _visit('class C { C(); }');
      final s = v.symbols.firstWhere((s) => s.kind == 'DartConstructor');
      expect(s.kind, 'DartConstructor');
      expect(s.containingClass, 'C');
    });

    test('named constructor includes class and name', () {
      final v = _visit('class C { C.named(); }');
      final s = v.symbols.firstWhere((s) => s.kind == 'DartConstructor');
      expect(s.name, 'C.named');
    });

    test('constructor produces CONTAINS relationship from class', () {
      final v = _visit('class C { C(); }');
      expect(_rels(v, from: 'C', type: 'CONTAINS'), isNotEmpty);
    });
  });

  group('field declarations', () {
    test('field produces DartField symbol', () {
      final v = _visit('class C { int count = 0; }');
      final s = _symbol(v, 'count');
      expect(s.kind, 'DartField');
      expect(s.containingClass, 'C');
    });

    test('typed field produces DEPENDS_ON relationship to its type', () {
      final v = _visit('class C { String name = ""; }');
      expect(_rels(v, from: 'name', to: 'String', type: 'DEPENDS_ON'), hasLength(1));
    });

    test('field produces CONTAINS relationship from class', () {
      final v = _visit('class C { int x = 0; }');
      expect(_rels(v, from: 'C', to: 'x', type: 'CONTAINS'), hasLength(1));
    });

    test('untyped field produces no DEPENDS_ON', () {
      final v = _visit('class C { var x = 0; }');
      expect(_rels(v, from: 'x', type: 'DEPENDS_ON'), isEmpty);
    });
  });

  group('top-level declarations', () {
    test('top-level function produces DartFunction', () {
      final v = _visit('void run() {}');
      final s = _symbol(v, 'run');
      expect(s.kind, 'DartFunction');
    });

    test('top-level getter produces DartProperty', () {
      final v = _visit('int get answer => 42;');
      final s = _symbol(v, 'answer');
      expect(s.kind, 'DartProperty');
    });

    test('top-level variable produces DartField', () {
      final v = _visit('const int kMax = 100;');
      final s = _symbol(v, 'kMax');
      expect(s.kind, 'DartField');
    });
  });

  group('import directives', () {
    test('import produces DEPENDS_ON relationship', () {
      final v = _visit("import 'dart:io';");
      expect(_rels(v, to: 'dart:io', type: 'DEPENDS_ON'), hasLength(1));
    });

    test('import fromKind is file', () {
      final v = _visit("import 'dart:io';");
      final rel = _rels(v, to: 'dart:io').first;
      expect(rel.fromKind, 'file');
      expect(rel.toKind, 'file');
    });
  });

  group('method invocations', () {
    test('method call produces INVOKES relationship', () {
      final v = _visit('void run() { print("hi"); }');
      expect(_rels(v, to: 'print', type: 'INVOKES'), hasLength(1));
    });
  });

  group('instance creation', () {
    test('new expression produces INVOKES relationship', () {
      final v = _visit('class Foo {} void run() { var f = new Foo(); }');
      expect(_rels(v, to: 'Foo', type: 'INVOKES'), hasLength(1));
    });
  });

  group('function expression invocations', () {
    test('simple identifier call produces INVOKES relationship', () {
      final v = _visit('void run() { final fn = () {}; fn(); }');
      expect(_rels(v, to: 'fn', type: 'INVOKES'), hasLength(1));
    });
  });

  group('extension type declarations', () {
    test('extracts extension type as DartExtensionType', () {
      final v = _visit('extension type Meters(int value) {}');
      final s = _symbol(v, 'Meters');
      expect(s.kind, 'DartExtensionType');
      expect(s.symbolClass, 'extensiontype');
    });

    test('private extension type has Private accessibility', () {
      final v = _visit('extension type _Internal(int value) {}');
      expect(_symbol(v, '_Internal').accessibility, 'Private');
    });
  });

  group('type alias declarations', () {
    test('typedef produces DartTypeAlias symbol', () {
      final v = _visit('typedef MyFunc = void Function(int);');
      final s = _symbol(v, 'MyFunc');
      expect(s.kind, 'DartTypeAlias');
      expect(s.symbolClass, 'type');
    });

    test('private typedef has Private accessibility', () {
      final v = _visit('typedef _Internal = void Function();');
      expect(_symbol(v, '_Internal').accessibility, 'Private');
    });
  });

  group('accessibility annotations', () {
    test('@protected annotation yields Protected accessibility', () {
      final v = _visit('''
import 'package:meta/meta.dart';
class C {
  @protected
  void doThing() {}
}
''');
      final s = _symbol(v, 'doThing');
      expect(s.accessibility, 'Protected');
    });

    test('@visibleForTesting annotation yields Internal accessibility', () {
      final v = _visit('''
import 'package:meta/meta.dart';
class C {
  @visibleForTesting
  void doThing() {}
}
''');
      final s = _symbol(v, 'doThing');
      expect(s.accessibility, 'Internal');
    });
  });

  group('getLine helper', () {
    test('returns offset when no resolver set', () {
      final visitor = DartAstVisitor(
        filePath: '/proj/lib/foo.dart',
        projectRoot: '/proj',
        packageName: 'pkg',
      );
      expect(visitor.getLine(42), 42);
    });

    test('delegates to resolver when set', () {
      final visitor = DartAstVisitor(
        filePath: '/proj/lib/foo.dart',
        projectRoot: '/proj',
        packageName: 'pkg',
      );
      visitor.lineInfoResolver = (offset) => offset + 10;
      expect(visitor.getLine(5), 15);
    });
  });

  group('FQN format', () {
    test('top-level symbol fqn contains package and file', () {
      final v = _visit('void run() {}',
          filePath: '/proj/lib/foo.dart',
          projectRoot: '/proj',
          packageName: 'my_pkg');
      expect(_symbol(v, 'run').fqn, 'package:my_pkg/lib/foo.dart::run');
    });

    test('class member fqn contains class name', () {
      final v = _visit('class C { void doThing() {} }',
          filePath: '/proj/lib/foo.dart',
          projectRoot: '/proj',
          packageName: 'my_pkg');
      expect(_symbol(v, 'doThing').fqn, contains('C.doThing'));
    });

    test('namespace reflects directory', () {
      final v = _visit('class C {}',
          filePath: '/proj/lib/services/foo.dart',
          projectRoot: '/proj',
          packageName: 'my_pkg');
      expect(_symbol(v, 'C').namespace_, 'package:my_pkg/lib/services');
    });
  });

  group('documentation comments', () {
    test('doc comment is captured', () {
      final v = _visit('/// A useful class.\nclass C {}');
      expect(_symbol(v, 'C').documentation, isNotNull);
      expect(_symbol(v, 'C').documentation, contains('A useful class'));
    });

    test('no doc comment yields null', () {
      final v = _visit('class C {}');
      expect(_symbol(v, 'C').documentation, isNull);
    });
  });
}
