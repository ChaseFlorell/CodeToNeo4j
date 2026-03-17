import 'dart:io';
import '../lib/src/analyzer_service.dart';
import '../lib/src/json_output.dart';

Future<void> main(List<String> args) async {
  if (args.isEmpty) {
    stderr.writeln('Usage: dart run dart_analyzer_bridge <project_root_path>');
    exit(1);
  }

  final projectRoot = args[0];
  final dir = Directory(projectRoot);
  if (!dir.existsSync()) {
    stderr.writeln('Error: Directory does not exist: $projectRoot');
    exit(1);
  }

  try {
    final service = AnalyzerService();
    final result = await service.analyze(projectRoot);
    stdout.writeln(toJsonString(result));
  } catch (e, stackTrace) {
    stderr.writeln('Error: $e');
    stderr.writeln(stackTrace);
    exit(1);
  }
}
