'use client';

import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/auth-context';
import { ShieldCheck, Gem, Lock, User as UserIcon, AlertCircle, Loader2 } from 'lucide-react';

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
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-saranga-green-950 via-saranga-green-900 to-saranga-green-800 p-4">
      <div className="w-full max-w-md">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-saranga-gold-500/20 border border-saranga-gold-500/40 shadow-xl mb-4 text-saranga-gold-400">
            <Gem className="w-9 h-9" />
          </div>
          <h1 className="text-3xl font-extrabold tracking-wider text-saranga-gold-400 uppercase">
            SARANGA
          </h1>
          <p className="text-saranga-gold-200/80 text-sm mt-1 font-medium tracking-wide">
            Jewellery Management System
          </p>
          <div className="inline-block mt-2 px-3 py-0.5 rounded-full text-xs font-semibold bg-saranga-gold-500/10 border border-saranga-gold-500/30 text-saranga-gold-300">
            Currency: Sri Lankan Rupees (LKR / Rs.)
          </div>
        </div>

        {/* Login Card */}
        <div className="bg-white rounded-2xl shadow-2xl p-8 border border-saranga-gold-500/30">
          <div className="flex items-center justify-between pb-4 mb-6 border-b border-gray-100">
            <div>
              <h2 className="text-xl font-bold text-saranga-green-950">System Login</h2>
              <p className="text-xs text-gray-500 mt-0.5">Enter authorized staff credentials</p>
            </div>
            <div className="p-2 rounded-lg bg-saranga-green-50 text-saranga-green-700">
              <ShieldCheck className="w-5 h-5" />
            </div>
          </div>

          {errorMessage && (
            <div className="mb-5 p-3 rounded-xl bg-red-50 border border-red-200 flex items-start gap-2.5 text-red-700 text-sm">
              <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5 text-red-600" />
              <span>{errorMessage}</span>
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1.5">
                Username
              </label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-gray-400">
                  <UserIcon className="w-4 h-4" />
                </div>
                <input
                  type="text"
                  {...register('username')}
                  placeholder="e.g. cashier1"
                  className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-gray-300 focus:outline-none focus:ring-2 focus:ring-saranga-green-600 focus:border-saranga-green-600 text-sm transition-all"
                  autoComplete="username"
                />
              </div>
              {errors.username && (
                <p className="text-xs text-red-600 mt-1 font-medium">{errors.username.message}</p>
              )}
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1.5">
                Password
              </label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-gray-400">
                  <Lock className="w-4 h-4" />
                </div>
                <input
                  type="password"
                  {...register('password')}
                  placeholder="••••••••"
                  className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-gray-300 focus:outline-none focus:ring-2 focus:ring-saranga-green-600 focus:border-saranga-green-600 text-sm transition-all"
                  autoComplete="current-password"
                />
              </div>
              {errors.password && (
                <p className="text-xs text-red-600 mt-1 font-medium">{errors.password.message}</p>
              )}
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full mt-2 py-3 px-4 rounded-xl font-semibold text-white bg-saranga-green-700 hover:bg-saranga-green-800 active:bg-saranga-green-900 border border-saranga-gold-500/30 shadow-lg hover:shadow-xl transition-all duration-200 flex items-center justify-center gap-2 disabled:opacity-50"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin text-saranga-gold-400" />
                  <span>Signing In...</span>
                </>
              ) : (
                <span>Sign In to POS & Inventory</span>
              )}
            </button>
          </form>

          <div className="mt-6 pt-4 text-center border-t border-gray-100">
            <span className="text-xs text-gray-400">
              Saranga Jewellery MVP • Royal Green & Gold Architecture
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
