import 'package:analyzer/dart/ast/ast.dart';
import 'package:analyzer/dart/ast/visitor.dart';
import 'package:analyzer/dart/element/element.dart';
import 'package:path/path.dart' as p;
import 'models.dart';

/// Recursive AST visitor that extracts symbols and relationships from Dart source.
class DartAstVisitor extends RecursiveAstVisitor<void> {
  final String filePath;
  final String projectRoot;
  final String packageName;
  final List<SymbolInfo> symbols = [];
  final List<RelationshipInfo> relationships = [];

  String? _currentClass;
  String? _currentClassKind;

  DartAstVisitor({
    required this.filePath,
    required this.projectRoot,
    required this.packageName,
  });

  String get _relativePath => p.relative(filePath, from: projectRoot);
  String get _namespace {
    final rel = _relativePath;
    final dir = p.dirname(rel).replaceAll('\\', '/');
    return 'package:$packageName/$dir';
  }

  String _fqn(String name) {
    if (_currentClass != null) {
      return 'package:$packageName/${_relativePath}::$_currentClass.$name';
    }
    return 'package:$packageName/${_relativePath}::$name';
  }

  String _accessibility(String name, {List<Annotation>? metadata}) {
    if (metadata != null) {
      for (final annotation in metadata) {
        final annName = annotation.name.name;
        if (annName == 'protected') return 'Protected';
        if (annName == 'visibleForTesting') return 'Internal';
      }
    }
    return name.startsWith('_') ? 'Private' : 'Public';
  }

  @override
  void visitClassDeclaration(ClassDeclaration node) {
    final name = node.namePart.typeName.lexeme;
    final isAbstract = node.abstractKeyword != null;
    symbols.add(SymbolInfo(
      name: name,
      kind: 'DartClass',
      symbolClass: 'class',
      fqn: _fqn(name),
      accessibility: _accessibility(name, metadata: node.metadata),
      startLine: _lineNumber(node.offset),
      endLine: _lineNumber(node.end),
      documentation: _extractDocComment(node),
      namespace_: _namespace,
    ));

    // extends
    if (node.extendsClause != null) {
      final superName = node.extendsClause!.superclass.name.lexeme;
      relationships.add(RelationshipInfo(
        fromSymbol: name,
        fromKind: 'class',
        fromLine: _lineNumber(node.offset),
        toSymbol: superName,
        toKind: 'class',
        relType: 'DEPENDS_ON',
      ));
    }

    // implements
    if (node.implementsClause != null) {
      for (final iface in node.implementsClause!.interfaces) {
        relationships.add(RelationshipInfo(
          fromSymbol: name,
          fromKind: 'class',
          fromLine: _lineNumber(node.offset),
          toSymbol: iface.name.lexeme,
          toKind: 'class',
          relType: 'DEPENDS_ON',
        ));
      }
    }

    // with (mixins)
    if (node.withClause != null) {
      for (final mixin in node.withClause!.mixinTypes) {
        relationships.add(RelationshipInfo(
          fromSymbol: name,
          fromKind: 'class',
          fromLine: _lineNumber(node.offset),
          toSymbol: mixin.name.lexeme,
          toKind: 'mixin',
          relType: 'DEPENDS_ON',
        ));
      }
    }

    _currentClass = name;
    _currentClassKind = 'class';
    super.visitClassDeclaration(node);
    _currentClass = null;
    _currentClassKind = null;
  }

  @override
  void visitMixinDeclaration(MixinDeclaration node) {
    final name = node.name.lexeme;
    symbols.add(SymbolInfo(
      name: name,
      kind: 'DartMixin',
      symbolClass: 'mixin',
      fqn: _fqn(name),
      accessibility: _accessibility(name, metadata: node.metadata),
      startLine: _lineNumber(node.offset),
      endLine: _lineNumber(node.end),
      documentation: _extractDocComment(node),
      namespace_: _namespace,
    ));

    _currentClass = name;
    _currentClassKind = 'mixin';
    super.visitMixinDeclaration(node);
    _currentClass = null;
    _currentClassKind = null;
  }

  @override
  void visitEnumDeclaration(EnumDeclaration node) {
    final name = node.namePart.typeName.lexeme;
    symbols.add(SymbolInfo(
      name: name,
      kind: 'DartEnum',
      symbolClass: 'enum',
      fqn: _fqn(name),
      accessibility: _accessibility(name, metadata: node.metadata),
      startLine: _lineNumber(node.offset),
      endLine: _lineNumber(node.end),
      documentation: _extractDocComment(node),
      namespace_: _namespace,
    ));

    _currentClass = name;
    _currentClassKind = 'enum';
    super.visitEnumDeclaration(node);
    _currentClass = null;
    _currentClassKind = null;
  }

  @override
  void visitExtensionDeclaration(ExtensionDeclaration node) {
    final name = node.name?.lexeme ?? '<unnamed>';
    symbols.add(SymbolInfo(
      name: name,
      kind: 'DartExtension',
      symbolClass: 'extension',
      fqn: _fqn(name),
      accessibility: _accessibility(name, metadata: node.metadata),
      startLine: _lineNumber(node.offset),
      endLine: _lineNumber(node.end),
      documentation: _extractDocComment(node),
      namespace_: _namespace,
    ));

    _currentClass = name;
    _currentClassKind = 'extension';
    super.visitExtensionDeclaration(node);
    _currentClass = null;
    _currentClassKind = null;
  }

  @override
  void visitExtensionTypeDeclaration(ExtensionTypeDeclaration node) {
    final name = node.primaryConstructor.typeName.lexeme;
    symbols.add(SymbolInfo(
      name: name,
      kind: 'DartExtensionType',
      symbolClass: 'extensiontype',
      fqn: _fqn(name),
      accessibility: _accessibility(name, metadata: node.metadata),
      startLine: _lineNumber(node.offset),
      endLine: _lineNumber(node.end),
      documentation: _extractDocComment(node),
      namespace_: _namespace,
    ));

    _currentClass = name;
    _currentClassKind = 'extensiontype';
    super.visitExtensionTypeDeclaration(node);
    _currentClass = null;
    _currentClassKind = null;
  }

  @override
  void visitGenericTypeAlias(GenericTypeAlias node) {
    final name = node.name.lexeme;
    symbols.add(SymbolInfo(
      name: name,
      kind: 'DartTypeAlias',
      symbolClass: 'type',
      fqn: _fqn(name),
      accessibility: _accessibility(name, metadata: node.metadata),
      startLine: _lineNumber(node.offset),
      endLine: _lineNumber(node.end),
      documentation: _extractDocComment(node),
      namespace_: _namespace,
    ));
    super.visitGenericTypeAlias(node);
  }

  @override
  void visitFunctionDeclaration(FunctionDeclaration node) {
    final name = node.name.lexeme;

    if (_currentClass != null) {
      // This shouldn't happen for class members — they go through visitMethodDeclaration.
      super.visitFunctionDeclaration(node);
      return;
    }

    // Top-level function
    if (node.isGetter || node.isSetter) {
      symbols.add(SymbolInfo(
        name: name,
        kind: 'DartProperty',
        symbolClass: 'property',
        fqn: _fqn(name),
        accessibility: _accessibility(name, metadata: node.metadata),
        startLine: _lineNumber(node.offset),
        endLine: _lineNumber(node.end),
        documentation: _extractDocComment(node),
        namespace_: _namespace,
      ));
    } else {
      symbols.add(SymbolInfo(
        name: name,
        kind: 'DartFunction',
        symbolClass: 'function',
        fqn: _fqn(name),
        accessibility: _accessibility(name, metadata: node.metadata),
        startLine: _lineNumber(node.offset),
        endLine: _lineNumber(node.end),
        documentation: _extractDocComment(node),
        namespace_: _namespace,
      ));
    }

    super.visitFunctionDeclaration(node);
  }

  @override
  void visitMethodDeclaration(MethodDeclaration node) {
    final name = node.name.lexeme;

    if (node.isGetter || node.isSetter) {
      symbols.add(SymbolInfo(
        name: name,
        kind: 'DartProperty',
        symbolClass: 'property',
        fqn: _fqn(name),
        accessibility: _accessibility(name, metadata: node.metadata),
        startLine: _lineNumber(node.offset),
        endLine: _lineNumber(node.end),
        documentation: _extractDocComment(node),
        namespace_: _namespace,
        containingClass: _currentClass,
      ));
    } else if (node.isOperator) {
      symbols.add(SymbolInfo(
        name: name,
        kind: 'DartOperator',
        symbolClass: 'operator',
        fqn: _fqn('operator $name'),
        accessibility: _accessibility(name, metadata: node.metadata),
        startLine: _lineNumber(node.offset),
        endLine: _lineNumber(node.end),
        documentation: _extractDocComment(node),
        namespace_: _namespace,
        containingClass: _currentClass,
      ));
    } else {
      symbols.add(SymbolInfo(
        name: name,
        kind: 'DartMethod',
        symbolClass: 'method',
        fqn: _fqn(name),
        accessibility: _accessibility(name, metadata: node.metadata),
        startLine: _lineNumber(node.offset),
        endLine: _lineNumber(node.end),
        documentation: _extractDocComment(node),
        namespace_: _namespace,
        containingClass: _currentClass,
      ));
    }

    if (_currentClass != null) {
      relationships.add(RelationshipInfo(
        fromSymbol: _currentClass!,
        fromKind: _currentClassKind!,
        fromLine: _lineNumber(node.offset),
        toSymbol: name,
        toKind: node.isGetter || node.isSetter ? 'property' : 'method',
        relType: 'CONTAINS',
      ));
    }

    super.visitMethodDeclaration(node);
  }

  @override
  void visitConstructorDeclaration(ConstructorDeclaration node) {
    final name = node.name?.lexeme ?? _currentClass ?? '<unnamed>';
    final displayName = node.name != null ? '$_currentClass.$name' : _currentClass ?? name;
    symbols.add(SymbolInfo(
      name: displayName,
      kind: 'DartConstructor',
      symbolClass: 'constructor',
      fqn: _fqn(displayName),
      accessibility: _accessibility(name, metadata: node.metadata),
      startLine: _lineNumber(node.offset),
      endLine: _lineNumber(node.end),
      documentation: _extractDocComment(node),
      namespace_: _namespace,
      containingClass: _currentClass,
    ));

    if (_currentClass != null) {
      relationships.add(RelationshipInfo(
        fromSymbol: _currentClass!,
        fromKind: _currentClassKind!,
        fromLine: _lineNumber(node.offset),
        toSymbol: displayName,
        toKind: 'constructor',
        relType: 'CONTAINS',
      ));
    }

    super.visitConstructorDeclaration(node);
  }

  @override
  void visitFieldDeclaration(FieldDeclaration node) {
    for (final variable in node.fields.variables) {
      final name = variable.name.lexeme;
      symbols.add(SymbolInfo(
        name: name,
        kind: 'DartField',
        symbolClass: 'field',
        fqn: _fqn(name),
        accessibility: _accessibility(name, metadata: node.metadata),
        startLine: _lineNumber(variable.offset),
        endLine: _lineNumber(variable.end),
        documentation: _extractDocComment(node),
        namespace_: _namespace,
        containingClass: _currentClass,
      ));

      if (_currentClass != null) {
        relationships.add(RelationshipInfo(
          fromSymbol: _currentClass!,
          fromKind: _currentClassKind!,
          fromLine: _lineNumber(variable.offset),
          toSymbol: name,
          toKind: 'field',
          relType: 'CONTAINS',
        ));
      }

      // Field type dependency
      final typeAnnotation = node.fields.type;
      if (typeAnnotation != null) {
        final typeName = typeAnnotation.toSource();
        relationships.add(RelationshipInfo(
          fromSymbol: name,
          fromKind: 'field',
          fromLine: _lineNumber(variable.offset),
          toSymbol: typeName,
          toKind: 'class',
          relType: 'DEPENDS_ON',
        ));
      }
    }

    super.visitFieldDeclaration(node);
  }

  @override
  void visitTopLevelVariableDeclaration(TopLevelVariableDeclaration node) {
    for (final variable in node.variables.variables) {
      final name = variable.name.lexeme;
      symbols.add(SymbolInfo(
        name: name,
        kind: 'DartField',
        symbolClass: 'field',
        fqn: _fqn(name),
        accessibility: _accessibility(name, metadata: node.metadata),
        startLine: _lineNumber(variable.offset),
        endLine: _lineNumber(variable.end),
        documentation: _extractDocComment(node),
        namespace_: _namespace,
      ));
    }
    super.visitTopLevelVariableDeclaration(node);
  }

  @override
  void visitImportDirective(ImportDirective node) {
    final uri = node.uri.stringValue;
    if (uri != null) {
      relationships.add(RelationshipInfo(
        fromSymbol: _relativePath,
        fromKind: 'file',
        fromLine: _lineNumber(node.offset),
        toSymbol: uri,
        toKind: 'file',
        relType: 'DEPENDS_ON',
      ));
    }
    super.visitImportDirective(node);
  }

  @override
  void visitMethodInvocation(MethodInvocation node) {
    final methodName = node.methodName.name;
    final target = node.target;
    final fromSymbol = _currentClass ?? _relativePath;
    final fromKind = _currentClass != null ? _currentClassKind! : 'file';

    relationships.add(RelationshipInfo(
      fromSymbol: fromSymbol,
      fromKind: fromKind,
      fromLine: _lineNumber(node.offset),
      toSymbol: methodName,
      toKind: 'method',
      relType: 'INVOKES',
    ));

    super.visitMethodInvocation(node);
  }

  @override
  void visitInstanceCreationExpression(InstanceCreationExpression node) {
    final typeName = node.constructorName.type.name.lexeme;
    final fromSymbol = _currentClass ?? _relativePath;
    final fromKind = _currentClass != null ? _currentClassKind! : 'file';

    relationships.add(RelationshipInfo(
      fromSymbol: fromSymbol,
      fromKind: fromKind,
      fromLine: _lineNumber(node.offset),
      toSymbol: typeName,
      toKind: 'constructor',
      relType: 'INVOKES',
    ));

    super.visitInstanceCreationExpression(node);
  }

  @override
  void visitFunctionExpressionInvocation(FunctionExpressionInvocation node) {
    final fromSymbol = _currentClass ?? _relativePath;
    final fromKind = _currentClass != null ? _currentClassKind! : 'file';

    final function = node.function;
    if (function is SimpleIdentifier) {
      relationships.add(RelationshipInfo(
        fromSymbol: fromSymbol,
        fromKind: fromKind,
        fromLine: _lineNumber(node.offset),
        toSymbol: function.name,
        toKind: 'function',
        relType: 'INVOKES',
      ));
    }

    super.visitFunctionExpressionInvocation(node);
  }

  // Helper: line number from offset
  int _lineNumber(int offset) {
    // Will be computed by the analyzer service which has access to the line info
    return offset; // Placeholder, overridden by analyzer_service
  }

  // Store the line info resolver set by the analyzer service
  int Function(int)? lineInfoResolver;

  int getLine(int offset) {
    if (lineInfoResolver != null) return lineInfoResolver!(offset);
    return offset;
  }

  String? _extractDocComment(AnnotatedNode node) {
    final comment = node.documentationComment;
    if (comment == null) return null;
    return comment.tokens.map((t) => t.lexeme).join('\n');
  }
}
