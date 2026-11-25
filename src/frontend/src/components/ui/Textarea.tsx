'use client';

import React from 'react';

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  helperText?: string;
  error?: string;
  requiredIndicator?: boolean;
}

export const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  (
    {
      label,
      helperText,
      error,
      requiredIndicator,
      className = '',
      id,
      disabled,
      ...props
    },
    ref
  ) => {
    const textareaId = id || (label ? label.toLowerCase().replace(/[^a-z0-9]/g, '-') : undefined);

    return (
      <div className="w-full">
        {label && (
          <div className="flex items-center justify-between mb-1.5">
            <label
              htmlFor={textareaId}
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
          <textarea
            ref={ref}
            id={textareaId}
            disabled={disabled}
            className={`
              block w-full rounded-lg border text-sm transition-colors
              px-3.5 py-2.5
              ${
                error
                  ? 'border-rose-400 text-rose-900 placeholder-rose-300 focus:border-rose-500 focus:ring-1 focus:ring-rose-500'
                  : 'border-[#E2E6E3] text-[#202824] placeholder-gray-400 focus:border-[#123B32] focus:ring-1 focus:ring-[#123B32]'
              }
              ${disabled ? 'bg-gray-100 text-gray-500 cursor-not-allowed border-gray-200' : 'bg-white'}
              focus:outline-none
              ${className}
            `}
            {...props}
          />
        </div>

        {error && <p className="mt-1 text-xs text-rose-600">{error}</p>}
        {!error && helperText && <p className="mt-1 text-xs text-[#69736E]">{helperText}</p>}
      </div>
    );
  }
);

Textarea.displayName = 'Textarea';
