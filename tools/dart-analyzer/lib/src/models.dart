/// Data models for the Dart analysis bridge output.

class AnalysisResult {
  final String projectName;
  final String projectRoot;
  final Map<String, FileResult> files;

  AnalysisResult({
    required this.projectName,
    required this.projectRoot,
    required this.files,
  });

  Map<String, dynamic> toJson() => {
        'projectName': projectName,
        'projectRoot': projectRoot,
        'files': files.map((k, v) => MapEntry(k, v.toJson())),
      };
}

class FileResult {
  final List<SymbolInfo> symbols;
  final List<RelationshipInfo> relationships;

  FileResult({required this.symbols, required this.relationships});

  Map<String, dynamic> toJson() => {
        'symbols': symbols.map((s) => s.toJson()).toList(),
        'relationships': relationships.map((r) => r.toJson()).toList(),
      };
}

class SymbolInfo {
  final String name;
  final String kind;
  final String symbolClass;
  final String fqn;
  final String accessibility;
  final int startLine;
  final int endLine;
  final String? documentation;
  final String? comments;
  final String? namespace_;
  final String? containingClass;

  SymbolInfo({
    required this.name,
    required this.kind,
    required this.symbolClass,
    required this.fqn,
    required this.accessibility,
    required this.startLine,
    required this.endLine,
    this.documentation,
    this.comments,
    this.namespace_,
    this.containingClass,
  });

  Map<String, dynamic> toJson() => {
        'name': name,
        'kind': kind,
        'class': symbolClass,
        'fqn': fqn,
        'accessibility': accessibility,
        'startLine': startLine,
        'endLine': endLine,
        'documentation': documentation,
        'comments': comments,
        'namespace': namespace_,
        'containingClass': containingClass,
      };
}

class RelationshipInfo {
  final String fromSymbol;
  final String fromKind;
  final int fromLine;
  final String toSymbol;
  final String toKind;
  final int? toLine;
  final String? toFile;
  final String relType;

  RelationshipInfo({
    required this.fromSymbol,
    required this.fromKind,
    required this.fromLine,
    required this.toSymbol,
    required this.toKind,
    this.toLine,
    this.toFile,
    required this.relType,
  });

  Map<String, dynamic> toJson() => {
        'fromSymbol': fromSymbol,
        'fromKind': fromKind,
        'fromLine': fromLine,
        'toSymbol': toSymbol,
        'toKind': toKind,
        'toLine': toLine,
        'toFile': toFile,
        'relType': relType,
      };
}
