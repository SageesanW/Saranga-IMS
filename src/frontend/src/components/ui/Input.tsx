'use client';

import React from 'react';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  helperText?: string;
  error?: string;
  unit?: string;
  requiredIndicator?: boolean;
  leftIcon?: React.ReactNode;
}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  (
    {
      label,
      helperText,
      error,
      unit,
      requiredIndicator,
      leftIcon,
      className = '',
      id,
      disabled,
      readOnly,
      ...props
    },
    ref
  ) => {
    const inputId = id || (label ? label.toLowerCase().replace(/[^a-z0-9]/g, '-') : undefined);

    return (
      <div className="w-full">
        {label && (
          <div className="flex items-center justify-between mb-1.5">
            <label
              htmlFor={inputId}
              className="block text-xs font-semibold text-[#202824] uppercase tracking-wider"
            >
              {label}
              {requiredIndicator && <span className="text-rose-600 ml-0.5">*</span>}
              {!requiredIndicator && props.required === false && (
                <span className="text-gray-400 font-normal normal-case ml-1 text-[11px]">(Optional)</span>
              )}
            </label>
          </div>
        )}

        <div className="relative rounded-lg shadow-sm">
          {leftIcon && (
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-[#69736E]">
              {leftIcon}
            </div>
          )}

          <input
            ref={ref}
            id={inputId}
            disabled={disabled}
            readOnly={readOnly}
            className={`
              block w-full rounded-lg border text-sm transition-colors
              ${leftIcon ? 'pl-9' : 'pl-3.5'}
              ${unit ? 'pr-12' : 'pr-3.5'}
              py-2
              ${
                error
                  ? 'border-rose-400 text-rose-900 placeholder-rose-300 focus:border-rose-500 focus:ring-1 focus:ring-rose-500'
                  : 'border-[#E2E6E3] text-[#202824] placeholder-gray-400 focus:border-[#123B32] focus:ring-1 focus:ring-[#123B32]'
              }
              ${disabled ? 'bg-gray-100 text-gray-500 cursor-not-allowed border-gray-200' : 'bg-white'}
              ${readOnly ? 'bg-[#F8F7F4] text-[#202824] font-medium border-gray-200 cursor-default' : ''}
              focus:outline-none
              ${className}
            `}
            {...props}
          />

          {unit && (
            <div className="absolute inset-y-0 right-0 pr-3 flex items-center pointer-events-none">
              <span className="text-xs font-semibold text-[#69736E] bg-[#F8F7F4] px-1.5 py-0.5 rounded border border-[#E2E6E3]">
                {unit}
              </span>
            </div>
          )}
        </div>

        {error && <p className="mt-1 text-xs text-rose-600">{error}</p>}
        {!error && helperText && <p className="mt-1 text-xs text-[#69736E]">{helperText}</p>}
      </div>
    );
  }
);

Input.displayName = 'Input';
