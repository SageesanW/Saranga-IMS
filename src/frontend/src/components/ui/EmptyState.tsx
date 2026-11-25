'use client';

import React from 'react';
import { PackageOpen } from 'lucide-react';
import { Button } from './Button';

export interface EmptyStateProps {
  title: string;
  description: string;
  icon?: React.ReactNode;
  actionLabel?: string;
  onAction?: () => void;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  title,
  description,
  icon,
  actionLabel,
  onAction,
}) => {
  return (
    <div className="flex flex-col items-center justify-center p-8 sm:p-12 text-center bg-white border border-dashed border-[#E2E6E3] rounded-xl">
      <div className="w-12 h-12 rounded-xl bg-[#F8F7F4] border border-[#E2E6E3] flex items-center justify-center text-[#69736E] mb-3.5">
        {icon || <PackageOpen className="w-6 h-6 text-[#69736E]" />}
      </div>
      <h3 className="text-sm font-bold text-[#123B32]">{title}</h3>
      <p className="text-xs text-[#69736E] max-w-sm mt-1">{description}</p>
      {actionLabel && onAction && (
        <div className="mt-4">
          <Button variant="primary" size="sm" onClick={onAction}>
            {actionLabel}
          </Button>
        </div>
      )}
    </div>
  );
};
