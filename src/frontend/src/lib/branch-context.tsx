'use client';

import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { Branch, branchApi, getActiveBranchId, setActiveBranchId as setApiActiveBranchId } from './api';
import { useAuth } from './auth-context';

interface BranchContextType {
  branches: Branch[];
  activeBranch: Branch | null;
  activeBranchId: string | null;
  setActiveBranch: (branchId: string | null) => void;
  isLoading: boolean;
  refreshBranches: () => Promise<void>;
}

const BranchContext = createContext<BranchContextType | undefined>(undefined);

export function BranchProvider({ children }: { children: React.ReactNode }) {
  const { user, isAuthenticated } = useAuth();
  const [branches, setBranches] = useState<Branch[]>([]);
  const [activeBranchId, setActiveBranchIdState] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const refreshBranches = useCallback(async () => {
    if (!isAuthenticated) return;
    try {
      setIsLoading(true);
      const data = await branchApi.getBranches();
      setBranches(data);

      // Determine active branch
      const savedBranchId = getActiveBranchId();
      if (savedBranchId && data.some(b => b.id === savedBranchId && b.isActive)) {
        setActiveBranchIdState(savedBranchId);
        setApiActiveBranchId(savedBranchId);
      } else if (user?.defaultBranchId && data.some(b => b.id === user.defaultBranchId && b.isActive)) {
        setActiveBranchIdState(user.defaultBranchId);
        setApiActiveBranchId(user.defaultBranchId);
      } else if (data.length > 0) {
        const firstActive = data.find(b => b.isActive) || data[0];
        setActiveBranchIdState(firstActive.id);
        setApiActiveBranchId(firstActive.id);
      }
    } catch {
      // Ignore initial branch load errors if unauthorized
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated, user?.defaultBranchId]);

  useEffect(() => {
    if (isAuthenticated) {
      refreshBranches();
    } else {
      setBranches([]);
      setActiveBranchIdState(null);
      setApiActiveBranchId(null);
    }
  }, [isAuthenticated, refreshBranches]);

  const setActiveBranch = (branchId: string | null) => {
    setActiveBranchIdState(branchId);
    setApiActiveBranchId(branchId);
  };

  const activeBranch = branches.find(b => b.id === activeBranchId) || null;

  return (
    <BranchContext.Provider
      value={{
        branches,
        activeBranch,
        activeBranchId,
        setActiveBranch,
        isLoading,
        refreshBranches,
      }}
    >
      {children}
    </BranchContext.Provider>
  );
}

export function useBranch() {
  const context = useContext(BranchContext);
  if (!context) {
    throw new Error('useBranch must be used within a BranchProvider');
  }
  return context;
}
