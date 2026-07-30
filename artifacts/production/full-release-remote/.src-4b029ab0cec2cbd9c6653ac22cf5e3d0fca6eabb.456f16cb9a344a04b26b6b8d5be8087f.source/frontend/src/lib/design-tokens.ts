export type ThemeMode = 'light' | 'dark';

export type SemanticTokenName =
  | 'canvas'
  | 'surface'
  | 'surfaceRaised'
  | 'surfaceMuted'
  | 'text'
  | 'textMuted'
  | 'border'
  | 'focus'
  | 'action'
  | 'actionForeground'
  | 'info'
  | 'success'
  | 'warning'
  | 'danger';

export type ThemeTokenSet = Record<SemanticTokenName, string>;

export const semanticTokenNames: readonly SemanticTokenName[] = [
  'canvas', 'surface', 'surfaceRaised', 'surfaceMuted', 'text', 'textMuted',
  'border', 'focus', 'action', 'actionForeground', 'info', 'success',
  'warning', 'danger',
];
