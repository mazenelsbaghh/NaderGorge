import Image from 'next/image';

import { PLATFORM_IDENTITY } from '@/packages/brand';

type PlatformLogoVariant = 'mark' | 'full';
type PlatformLogoSize = 'sm' | 'md' | 'lg';

interface PlatformLogoProps {
  variant?: PlatformLogoVariant;
  size?: PlatformLogoSize;
  tone?: 'dark' | 'light';
  className?: string;
  priority?: boolean;
  alt?: string;
}

const sizeClasses: Record<PlatformLogoVariant, Record<PlatformLogoSize, string>> = {
  mark: {
    sm: 'h-4 w-4',
    md: 'h-8 w-8',
    lg: 'h-14 w-14',
  },
  full: {
    sm: 'h-8 w-auto',
    md: 'h-12 w-auto',
    lg: 'h-16 w-auto',
  },
};

const intrinsicSizes: Record<PlatformLogoVariant, Record<PlatformLogoSize, { width: number; height: number }>> = {
  mark: {
    sm: { width: 24, height: 24 },
    md: { width: 48, height: 48 },
    lg: { width: 80, height: 80 },
  },
  full: {
    sm: { width: 96, height: 48 },
    md: { width: 128, height: 64 },
    lg: { width: 160, height: 80 },
  },
};

export function PlatformLogo({
  variant = 'full',
  size = 'md',
  tone = 'dark',
  className = '',
  priority = false,
  alt = PLATFORM_IDENTITY.logo.alt,
}: PlatformLogoProps) {
  const imageSize = intrinsicSizes[variant][size];
  const markSize = intrinsicSizes['mark'][size];
  
  const lightSrc = variant === 'mark' ? PLATFORM_IDENTITY.logo.mark : PLATFORM_IDENTITY.logo.full;
  const darkSrc = PLATFORM_IDENTITY.logo.markLight;

  if (tone === 'light') {
    return (
      <Image
        src={darkSrc}
        width={markSize.width}
        height={markSize.height}
        alt={alt}
        priority={priority}
        className={`${sizeClasses['mark'][size]} object-contain ${className}`.trim()}
      />
    );
  }

  return (
    <>
      <Image
        src={lightSrc}
        width={imageSize.width}
        height={imageSize.height}
        alt={alt}
        priority={priority}
        className={`${sizeClasses[variant][size]} object-contain dark:hidden ${className}`.trim()}
      />
      <Image
        src={darkSrc}
        width={markSize.width}
        height={markSize.height}
        alt={alt}
        priority={priority}
        className={`${sizeClasses['mark'][size]} object-contain hidden dark:block ${className}`.trim()}
      />
    </>
  );
}
