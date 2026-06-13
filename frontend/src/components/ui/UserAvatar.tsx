'use client';

import React from 'react';
import Image from 'next/image';
import { AVATAR_LIST } from '@/data/avatars';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

interface UserAvatarProps {
  avatarSlug?: string | null;
  fullName?: string;
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl' | number;
  className?: string;
  role?: 'student' | 'teacher';
}

const sizeClasses = {
  xs: 'h-6 w-6 text-xs',
  sm: 'h-8 w-8 text-[12px]',
  md: 'h-10 w-10 text-[14px]',
  lg: 'h-14 w-14 text-[18px]',
  xl: 'h-20 w-20 text-[24px]',
};

// Simple hash to get consistent gradient colors for a user's initials
function getGradientStyle(name: string) {
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  const index = Math.abs(hash) % 5;
  const gradients = [
    'from-[var(--primary)] to-[var(--secondary)] text-white',
    'from-[var(--secondary)] to-[var(--primary)] text-white',
    'from-[var(--primary)] to-[var(--accent)] text-white',
    'from-[var(--secondary)] to-[#0B6F6F] text-white',
    'from-[#173760] to-[var(--primary)] text-white',
  ];
  return gradients[index];
}

export function UserAvatar({
  avatarSlug,
  fullName = 'طالب',
  size = 'md',
  className = '',
}: UserAvatarProps) {
  // Resolve size
  const sizeClass = typeof size === 'string' ? sizeClasses[size] : '';
  const customSizeStyle = typeof size === 'number' ? { width: size, height: size } : {};

  // For teacher role:
  // If they have an avatarSlug that is a full URL (representing a teacher's photo upload) or a local path:
  const isUrl = avatarSlug?.startsWith('http') || avatarSlug?.startsWith('/') || avatarSlug?.startsWith('blob:');
  const matchingAvatar = !isUrl ? AVATAR_LIST.find((a) => a.slug === avatarSlug) : null;

  // Render Image Avatar if available
  if (matchingAvatar || (avatarSlug && isUrl)) {
    const src = resolveMediaUrl(matchingAvatar ? matchingAvatar.imageUrl : avatarSlug!);
    const name = matchingAvatar ? matchingAvatar.name : fullName;

    return (
      <div
        className={`relative overflow-hidden rounded-full ring-2 ring-[var(--admin-border)] flex-shrink-0 bg-[var(--admin-card-soft)] ${sizeClass} ${className}`}
        style={customSizeStyle}
      >
        <Image
          src={src}
          alt={name}
          fill
          sizes="(max-width: 768px) 40px, 80px"
          className="object-cover"
          unoptimized // upload images might be local
        />
      </div>
    );
  }

  // Fallback to initials circle
  const initials = fullName
    .trim()
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word.charAt(0))
    .join('')
    .toUpperCase() || 'ط';

  const gradient = getGradientStyle(fullName);

  return (
    <div
      className={`flex items-center justify-center rounded-full font-black select-none ring-2 ring-[var(--admin-border)] flex-shrink-0 bg-gradient-to-br ${gradient} ${sizeClass} ${className}`}
      style={customSizeStyle}
    >
      <span>{initials}</span>
    </div>
  );
}
