import 'dart:io';
import 'package:analyzer/dart/analysis/analysis_context_collection.dart';
import 'package:analyzer/dart/analysis/results.dart';
import 'package:analyzer/file_system/physical_file_system.dart';
import 'package:path/path.dart' as p;
import 'ast_visitor.dart';
import 'models.dart' as models;

/// Wraps the Dart analyzer to produce [models.AnalysisResult] from a project root.
class AnalyzerService {
  /// Analyzes all Dart files under [projectRoot] and returns the result.
  Future<models.AnalysisResult> analyze(String projectRoot) async {
    final normalizedRoot = p.normalize(p.absolute(projectRoot));
    final resourceProvider = PhysicalResourceProvider.INSTANCE;

    final collection = AnalysisContextCollection(
      includedPaths: [normalizedRoot],
      resourceProvider: resourceProvider,
    );

    final projectName = _readProjectName(normalizedRoot);
    final files = <String, models.FileResult>{};

    for (final context in collection.contexts) {
      for (final filePath in context.contextRoot.analyzedFiles()) {
        if (!filePath.endsWith('.dart')) continue;

        final relativePath = p.relative(filePath, from: normalizedRoot);

        // Skip generated / hidden files
        if (relativePath.startsWith('.dart_tool') ||
            relativePath.startsWith('build/') ||
            relativePath.contains('/.')) {
          continue;
        }

        try {
          final result = await context.currentSession.getResolvedUnit(filePath);
          if (result is! ResolvedUnitResult) {
            stderr.writeln('Warning: Could not resolve $filePath');
            continue;
          }

          final unit = result.unit;
          final lineInfo = result.lineInfo;

          final visitor = DartAstVisitor(
            filePath: filePath,
            projectRoot: normalizedRoot,
            packageName: projectName,
          );

          // Wire up line resolution so offsets become 1-based line numbers
          visitor.lineInfoResolver =
              (offset) => lineInfo.getLocation(offset).lineNumber;

          unit.accept(visitor);

          // Fix up line numbers using the resolver
          final fixedSymbols = visitor.symbols.map((s) => models.SymbolInfo(
                name: s.name,
                kind: s.kind,
                symbolClass: s.symbolClass,
                fqn: s.fqn,
                accessibility: s.accessibility,
                startLine: lineInfo.getLocation(s.startLine).lineNumber,
                endLine: lineInfo.getLocation(s.endLine).lineNumber,
                documentation: s.documentation,
                comments: s.comments,
                namespace_: s.namespace_,
                containingClass: s.containingClass,
              )).toList();

          final fixedRelationships = visitor.relationships.map((r) => models.RelationshipInfo(
                fromSymbol: r.fromSymbol,
                fromKind: r.fromKind,
                fromLine: lineInfo.getLocation(r.fromLine).lineNumber,
                toSymbol: r.toSymbol,
                toKind: r.toKind,
                toLine: r.toLine != null
                    ? lineInfo.getLocation(r.toLine!).lineNumber
                    : null,
                toFile: r.toFile,
                relType: r.relType,
              )).toList();

          files[relativePath] = models.FileResult(
            symbols: fixedSymbols,
            relationships: fixedRelationships,
          );
        } catch (e) {
          stderr.writeln('Warning: Error analyzing $filePath: $e');
        }
      }
    }

    return models.AnalysisResult(
      projectName: projectName,
      projectRoot: normalizedRoot,
      files: files,
    );
  }

  String _readProjectName(String projectRoot) {
    final pubspecFile = File(p.join(projectRoot, 'pubspec.yaml'));
    if (!pubspecFile.existsSync()) return p.basename(projectRoot);

    // Simple line-based extraction of 'name:' field
    for (final line in pubspecFile.readAsLinesSync()) {
      final trimmed = line.trim();
      if (trimmed.startsWith('name:')) {
        return trimmed.substring('name:'.length).trim();
      }
    }

    return p.basename(projectRoot);
  }
}
