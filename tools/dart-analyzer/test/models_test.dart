import 'package:test/test.dart';

import '../lib/src/models.dart';
import '../lib/src/schema.dart';

void main() {
  group('SymbolInfo.toJson', () {
    test('serializes required fields', () {
      final s = SymbolInfo(
        name: 'MyClass',
        kind: 'DartClass',
        symbolClass: 'class',
        fqn: 'package:pkg/lib/foo.dart::MyClass',
        accessibility: 'Public',
        startLine: 1,
        endLine: 10,
      );
      final json = s.toJson();
      expect(json['name'], 'MyClass');
      expect(json['kind'], 'DartClass');
      expect(json['class'], 'class');
      expect(json['fqn'], 'package:pkg/lib/foo.dart::MyClass');
      expect(json['accessibility'], 'Public');
      expect(json['startLine'], 1);
      expect(json['endLine'], 10);
    });

    test('optional fields default to null', () {
      final s = SymbolInfo(
        name: 'f',
        kind: 'DartFunction',
        symbolClass: 'function',
        fqn: 'pkg::f',
        accessibility: 'Public',
        startLine: 1,
        endLine: 2,
      );
      final json = s.toJson();
      expect(json['documentation'], isNull);
      expect(json['comments'], isNull);
      expect(json['namespace'], isNull);
      expect(json['containingClass'], isNull);
    });

    test('optional fields are serialized when provided', () {
      final s = SymbolInfo(
        name: 'doThing',
        kind: 'DartMethod',
        symbolClass: 'method',
        fqn: 'pkg::C.doThing',
        accessibility: 'Private',
        startLine: 5,
        endLine: 8,
        documentation: '/// Does a thing.',
        namespace_: 'package:pkg/lib',
        containingClass: 'C',
      );
      final json = s.toJson();
      expect(json['documentation'], '/// Does a thing.');
      expect(json['namespace'], 'package:pkg/lib');
      expect(json['containingClass'], 'C');
    });
  });

  group('RelationshipInfo.toJson', () {
    test('serializes required fields', () {
      final r = RelationshipInfo(
        fromSymbol: 'B',
        fromKind: 'class',
        fromLine: 3,
        toSymbol: 'A',
        toKind: 'class',
        relType: GraphSchema.dependsOn,
      );
      final json = r.toJson();
      expect(json['fromSymbol'], 'B');
      expect(json['fromKind'], 'class');
      expect(json['fromLine'], 3);
      expect(json['toSymbol'], 'A');
      expect(json['toKind'], 'class');
      expect(json['relType'], GraphSchema.dependsOn);
    });

    test('optional toLine and toFile default to null', () {
      final r = RelationshipInfo(
        fromSymbol: 'X',
        fromKind: 'class',
        fromLine: 1,
        toSymbol: 'Y',
        toKind: 'class',
        relType: GraphSchema.contains,
      );
      final json = r.toJson();
      expect(json['toLine'], isNull);
      expect(json['toFile'], isNull);
    });

    test('optional toLine and toFile are serialized when provided', () {
      final r = RelationshipInfo(
        fromSymbol: 'X',
        fromKind: 'file',
        fromLine: 1,
        toSymbol: 'Y',
        toKind: 'file',
        toLine: 42,
        toFile: 'other.dart',
        relType: GraphSchema.dependsOn,
      );
      final json = r.toJson();
      expect(json['toLine'], 42);
      expect(json['toFile'], 'other.dart');
    });
  });

  group('FileResult.toJson', () {
    test('serializes symbols and relationships lists', () {
      final f = FileResult(symbols: [], relationships: []);
      final json = f.toJson();
      expect(json['symbols'], isEmpty);
      expect(json['relationships'], isEmpty);
    });

    test('includes serialized children', () {
      final s = SymbolInfo(
        name: 'C',
        kind: 'DartClass',
        symbolClass: 'class',
        fqn: 'pkg::C',
        accessibility: 'Public',
        startLine: 1,
        endLine: 5,
      );
      final f = FileResult(symbols: [s], relationships: []);
      final json = f.toJson();
      expect((json['symbols'] as List).first['name'], 'C');
    });
  });

  group('AnalysisResult.toJson', () {
    test('serializes top-level fields', () {
      final r = AnalysisResult(
        projectName: 'my_pkg',
        projectRoot: '/proj',
        files: {},
      );
      final json = r.toJson();
      expect(json['projectName'], 'my_pkg');
      expect(json['projectRoot'], '/proj');
      expect(json['files'], isEmpty);
    });

    test('includes file entries', () {
      final f = FileResult(symbols: [], relationships: []);
      final r = AnalysisResult(
        projectName: 'my_pkg',
        projectRoot: '/proj',
        files: {'lib/foo.dart': f},
      );
      final json = r.toJson();
      expect((json['files'] as Map).keys, contains('lib/foo.dart'));
    });
  });
}
