'use client';

import React from 'react';
import { Loader2 } from 'lucide-react';

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'gold' | 'danger' | 'danger-outline' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  isLoading?: boolean;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      children,
      variant = 'primary',
      size = 'md',
      isLoading = false,
      leftIcon,
      rightIcon,
      className = '',
      disabled,
      ...props
    },
    ref
  ) => {
    const baseStyles =
      'inline-flex items-center justify-center font-medium rounded-lg transition-colors focus:outline-none focus:ring-2 focus:ring-offset-1 disabled:opacity-50 disabled:cursor-not-allowed select-none';

    const sizeStyles = {
      sm: 'px-2.5 py-1.5 text-xs gap-1.5',
      md: 'px-3.5 py-2 text-sm gap-2',
      lg: 'px-4.5 py-2.5 text-base gap-2.5',
    };

    const variantStyles = {
      primary:
        'bg-[#123B32] hover:bg-[#0B2822] text-white shadow-sm border border-[#123B32] focus:ring-[#123B32]/40',
      secondary:
        'bg-white hover:bg-[#F8F7F4] text-[#202824] border border-[#E2E6E3] hover:border-[#CCD2CE] shadow-sm focus:ring-[#123B32]/20',
      gold:
        'bg-[#B89A60] hover:bg-[#9C7E47] text-white shadow-sm border border-[#B89A60] focus:ring-[#B89A60]/40',
      danger:
        'bg-rose-600 hover:bg-rose-700 text-white shadow-sm border border-rose-600 focus:ring-rose-500/30',
      'danger-outline':
        'bg-white hover:bg-rose-50 text-rose-600 border border-rose-200 hover:border-rose-300 focus:ring-rose-500/20',
      ghost:
        'bg-transparent hover:bg-black/5 text-[#69736E] hover:text-[#202824] border border-transparent focus:ring-black/10',
    };

    return (
      <button
        ref={ref}
        disabled={disabled || isLoading}
        className={`${baseStyles} ${sizeStyles[size]} ${variantStyles[variant]} ${className}`}
        {...props}
      >
        {isLoading ? (
          <Loader2 className="w-4 h-4 animate-spin flex-shrink-0" />
        ) : (
          leftIcon && <span className="flex-shrink-0">{leftIcon}</span>
        )}
        <span>{children}</span>
        {!isLoading && rightIcon && <span className="flex-shrink-0">{rightIcon}</span>}
      </button>
    );
  }
);

Button.displayName = 'Button';
