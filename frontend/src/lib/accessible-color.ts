const DARK_TEXT = '#0a1d3d';
const LIGHT_TEXT = '#ffffff';
const FALLBACK_BACKGROUND = '#eef1f4';
const MINIMUM_BODY_CONTRAST = 4.5;

type Rgb = readonly [number, number, number];

export type AccessibleColorPair = {
  backgroundColor: string;
  color: string;
  contrastRatio: number;
  adjusted: boolean;
};

function parseHexColor(value: string): Rgb | null {
  const normalized = value.trim().replace(/^#/, '');
  const expanded =
    normalized.length === 3
      ? normalized
          .split('')
          .map((character) => `${character}${character}`)
          .join('')
      : normalized;
  if (!/^[0-9a-f]{6}$/i.test(expanded)) return null;
  return [0, 2, 4].map((offset) =>
    Number.parseInt(expanded.slice(offset, offset + 2), 16),
  ) as unknown as Rgb;
}

function toHex(rgb: Rgb) {
  return `#${rgb
    .map((channel) => Math.round(channel).toString(16).padStart(2, '0'))
    .join('')}`;
}

function relativeLuminance(rgb: Rgb) {
  const [red, green, blue] = rgb.map((channel) => {
    const normalizedChannel = channel / 255;
    return normalizedChannel <= 0.04045
      ? normalizedChannel / 12.92
      : ((normalizedChannel + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

export function contrastRatio(foreground: string, background: string) {
  const foregroundRgb = parseHexColor(foreground);
  const backgroundRgb = parseHexColor(background);
  if (!foregroundRgb || !backgroundRgb) return 1;
  const lighter = Math.max(
    relativeLuminance(foregroundRgb),
    relativeLuminance(backgroundRgb),
  );
  const darker = Math.min(
    relativeLuminance(foregroundRgb),
    relativeLuminance(backgroundRgb),
  );
  return (lighter + 0.05) / (darker + 0.05);
}

function mix(start: Rgb, end: Rgb, amount: number): Rgb {
  return start.map(
    (channel, index) => channel + (end[index] - channel) * amount,
  ) as unknown as Rgb;
}

export function accessibleColorPair(
  requestedBackground: string,
): AccessibleColorPair {
  const parsedBackground =
    parseHexColor(requestedBackground) ?? parseHexColor(FALLBACK_BACKGROUND)!;
  const normalizedBackground = toHex(parsedBackground);
  const candidates = [DARK_TEXT, LIGHT_TEXT] as const;
  const color = candidates.reduce((best, candidate) =>
    contrastRatio(candidate, normalizedBackground) >
    contrastRatio(best, normalizedBackground)
      ? candidate
      : best,
  );
  const initialContrast = contrastRatio(color, normalizedBackground);
  if (initialContrast >= MINIMUM_BODY_CONTRAST) {
    return {
      backgroundColor: normalizedBackground,
      color,
      contrastRatio: initialContrast,
      adjusted: normalizedBackground !== requestedBackground.toLowerCase(),
    };
  }

  const adjustmentTarget = parseHexColor(
    color === DARK_TEXT ? LIGHT_TEXT : DARK_TEXT,
  )!;
  for (let step = 1; step <= 100; step += 1) {
    const adjustedBackground = toHex(
      mix(parsedBackground, adjustmentTarget, step / 100),
    );
    const adjustedContrast = contrastRatio(color, adjustedBackground);
    if (adjustedContrast >= MINIMUM_BODY_CONTRAST) {
      return {
        backgroundColor: adjustedBackground,
        color,
        contrastRatio: adjustedContrast,
        adjusted: true,
      };
    }
  }

  return {
    backgroundColor: FALLBACK_BACKGROUND,
    color: DARK_TEXT,
    contrastRatio: contrastRatio(DARK_TEXT, FALLBACK_BACKGROUND),
    adjusted: true,
  };
}
