export interface SymbolInfo {
  name: string;
  kind: string;
  class: string;
  fqn: string;
  accessibility: string;
  startLine: number;
  endLine: number;
  documentation: string | null;
  comments: string | null;
  namespace: string | null;
  containingClass: string | null;
}

export interface RelationshipInfo {
  fromSymbol: string;
  fromKind: string;
  fromLine: number;
  toSymbol: string;
  toKind: string;
  toLine: number | null;
  toFile: string | null;
  relType: string;
}

export interface FileResult {
  symbols: SymbolInfo[];
  relationships: RelationshipInfo[];
}

export interface AnalysisResult {
  projectName: string;
  projectRoot: string;
  files: Record<string, FileResult>;
}
