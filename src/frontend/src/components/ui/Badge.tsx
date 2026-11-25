'use client';

import React from 'react';

export type BadgeVariant =
  | 'success'
  | 'warning'
  | 'error'
  | 'info'
  | 'neutral'
  | 'gold'
  | 'forest';

export interface BadgeProps {
  children: React.ReactNode;
  variant?: BadgeVariant;
  size?: 'sm' | 'md';
  icon?: React.ReactNode;
  className?: string;
}

export const Badge: React.FC<BadgeProps> = ({
  children,
  variant = 'neutral',
  size = 'md',
  icon,
  className = '',
}) => {
  const sizeStyles = {
    sm: 'px-1.5 py-0.5 text-[11px] gap-1 font-medium',
    md: 'px-2.5 py-0.5 text-xs gap-1.5 font-medium',
  };

  const variantStyles: Record<BadgeVariant, string> = {
    success: 'bg-emerald-50 text-emerald-800 border border-emerald-200/80',
    warning: 'bg-amber-50 text-amber-800 border border-amber-200/80',
    error: 'bg-rose-50 text-rose-800 border border-rose-200/80',
    info: 'bg-blue-50 text-blue-800 border border-blue-200/80',
    neutral: 'bg-gray-100 text-gray-700 border border-gray-200',
    gold: 'bg-[#FBF9F2] text-[#7D6336] border border-[#EBDDBE]',
    forest: 'bg-[#F0F9F5] text-[#123B32] border border-[#B8DFC9]',
  };

  return (
    <span
      className={`inline-flex items-center rounded-md select-none tracking-tight ${sizeStyles[size]} ${variantStyles[variant]} ${className}`}
    >
      {icon && <span className="flex-shrink-0">{icon}</span>}
      <span>{children}</span>
    </span>
  );
};
