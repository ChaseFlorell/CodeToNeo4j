import 'dart:convert';
import 'models.dart';

/// Serializes an [AnalysisResult] to a JSON string.
String toJsonString(AnalysisResult result) {
  return const JsonEncoder.withIndent('  ').convert(result.toJson());
}
