import 'dart:convert';

import 'package:test/test.dart';

import '../lib/src/json_output.dart';
import '../lib/src/models.dart';

void main() {
  group('toJsonString', () {
    test('produces valid JSON', () {
      final result = AnalysisResult(
        projectName: 'my_pkg',
        projectRoot: '/proj',
        files: {},
      );
      final output = toJsonString(result);
      expect(() => jsonDecode(output), returnsNormally);
    });

    test('uses 2-space indentation', () {
      final result = AnalysisResult(
        projectName: 'my_pkg',
        projectRoot: '/proj',
        files: {},
      );
      final output = toJsonString(result);
      expect(output, contains('  "projectName"'));
    });

    test('round-trips projectName and projectRoot', () {
      final result = AnalysisResult(
        projectName: 'cool_app',
        projectRoot: '/home/user/cool_app',
        files: {},
      );
      final decoded = jsonDecode(toJsonString(result)) as Map<String, dynamic>;
      expect(decoded['projectName'], 'cool_app');
      expect(decoded['projectRoot'], '/home/user/cool_app');
    });

    test('includes file entries in output', () {
      final file = FileResult(
        symbols: [
          SymbolInfo(
            name: 'MyClass',
            kind: 'DartClass',
            symbolClass: 'class',
            fqn: 'package:cool_app/lib/foo.dart::MyClass',
            accessibility: 'Public',
            startLine: 1,
            endLine: 10,
          ),
        ],
        relationships: [],
      );
      final result = AnalysisResult(
        projectName: 'cool_app',
        projectRoot: '/proj',
        files: {'lib/foo.dart': file},
      );
      final decoded = jsonDecode(toJsonString(result)) as Map<String, dynamic>;
      final files = decoded['files'] as Map<String, dynamic>;
      expect(files.keys, contains('lib/foo.dart'));
      final symbols = (files['lib/foo.dart']['symbols'] as List);
      expect(symbols.first['name'], 'MyClass');
    });
  });
}
