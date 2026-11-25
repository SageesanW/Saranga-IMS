'use client';

import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/auth-context';
import { Lock, User as UserIcon, AlertCircle, Loader2, Eye, EyeOff } from 'lucide-react';

const loginSchema = z.object({
  username: z.string().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required'),
});

type LoginFormData = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  });

  const onSubmit = async (data: LoginFormData) => {
    try {
      setIsSubmitting(true);
      setErrorMessage(null);
      await login(data.username, data.password);
      router.push('/dashboard');
    } catch (err: any) {
      setErrorMessage(err.message || 'Login failed. Please check your credentials.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#0B2822] p-4 sm:p-6">
      <div className="w-full max-w-md">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-xl bg-gradient-to-br from-[#B89A60] to-[#7D6336] text-white font-bold text-2xl shadow-lg mb-3.5 border border-[#B89A60]/40">
            S
          </div>
          <h1 className="text-2xl font-bold tracking-wider text-white uppercase">
            SARANGA JEWELLERY
          </h1>
          <p className="text-xs text-[#B89A60] mt-1 font-medium tracking-widest uppercase">
            Enterprise Management System
          </p>
        </div>

        {/* Login Card */}
        <div className="bg-white rounded-2xl shadow-xl p-8 border border-[#E2E6E3]">
          <div className="mb-6 pb-4 border-b border-[#E2E6E3]">
            <h2 className="text-lg font-bold text-[#123B32]">Staff Authentication</h2>
            <p className="text-xs text-[#69736E] mt-0.5">
              Enter your authorized staff credentials to access your branch
            </p>
          </div>

          {errorMessage && (
            <div className="mb-5 p-3 rounded-lg bg-rose-50 border border-rose-200 flex items-start gap-2.5 text-rose-700 text-xs">
              <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5 text-rose-600" />
              <span>{errorMessage}</span>
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label
                htmlFor="login-username"
                className="block text-xs font-semibold text-[#202824] uppercase tracking-wider mb-1.5"
              >
                Username <span className="text-rose-600">*</span>
              </label>
              <div className="relative rounded-lg shadow-xs">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-[#69736E]">
                  <UserIcon className="w-4 h-4" />
                </div>
                <input
                  id="login-username"
                  type="text"
                  {...register('username')}
                  placeholder="e.g. saranga_owner"
                  className="w-full pl-9 pr-3.5 py-2 text-sm rounded-lg border border-[#E2E6E3] focus:outline-none focus:ring-1 focus:ring-[#123B32] focus:border-[#123B32] text-[#202824] placeholder-gray-400 transition-colors"
                  autoComplete="username"
                />
              </div>
              {errors.username && (
                <p className="text-xs text-rose-600 mt-1">{errors.username.message}</p>
              )}
            </div>

            <div>
              <label
                htmlFor="login-password"
                className="block text-xs font-semibold text-[#202824] uppercase tracking-wider mb-1.5"
              >
                Password <span className="text-rose-600">*</span>
              </label>
              <div className="relative rounded-lg shadow-xs">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-[#69736E]">
                  <Lock className="w-4 h-4" />
                </div>
                <input
                  id="login-password"
                  type={showPassword ? 'text' : 'password'}
                  {...register('password')}
                  placeholder="Enter your password"
                  className="w-full pl-9 pr-10 py-2 text-sm rounded-lg border border-[#E2E6E3] focus:outline-none focus:ring-1 focus:ring-[#123B32] focus:border-[#123B32] text-[#202824] placeholder-gray-400 transition-colors"
                  autoComplete="current-password"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute inset-y-0 right-0 pr-3 flex items-center text-[#69736E] hover:text-[#202824] focus:outline-none"
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
              {errors.password && (
                <p className="text-xs text-rose-600 mt-1">{errors.password.message}</p>
              )}
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full mt-2 py-2.5 px-4 rounded-lg font-semibold text-white bg-[#123B32] hover:bg-[#0B2822] active:bg-[#061713] border border-[#123B32] shadow-sm transition-colors flex items-center justify-center gap-2 disabled:opacity-50 text-sm cursor-pointer select-none"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin text-[#B89A60]" />
                  <span>Authenticating...</span>
                </>
              ) : (
                <span>Sign In to System</span>
              )}
            </button>
          </form>

          <div className="mt-6 pt-4 text-center border-t border-[#E2E6E3]">
            <p className="text-[11px] text-[#69736E]">
              Restricted enterprise system for authorized personnel only.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
