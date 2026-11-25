'use client';

import React from 'react';

export interface TableProps {
  headers: string[];
  children: React.ReactNode;
  className?: string;
}

export const Table: React.FC<TableProps> = ({ headers, children, className = '' }) => {
  return (
    <div className={`w-full overflow-x-auto rounded-xl border border-[#E2E6E3] bg-white shadow-sm ${className}`}>
      <table className="w-full text-left text-sm border-collapse">
        <thead>
          <tr className="border-b border-[#E2E6E3] bg-[#F8F7F4] text-[11px] font-semibold text-[#69736E] uppercase tracking-wider">
            {headers.map((h, i) => (
              <th key={i} className="px-4 py-3 select-none">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-[#E2E6E3]">{children}</tbody>
      </table>
    </div>
  );
};
