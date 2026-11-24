export interface User {
  id: string;
  username: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
  branchIds: string[];
  defaultBranchId: string | null;
  isGlobalAdmin: boolean;
  isActive: boolean;
  lastLoginAt?: string | null;
  createdAt?: string;
}

export interface Branch {
  id: string;
  name: string;
  code: string;
  country: string;
  currencyCode: string;
  timeZone: string;
  isActive: boolean;
  createdAt: string;
}

export interface Role {
  id: string;
  name: string;
  description: string;
  isSystemRole: boolean;
  permissions: string[];
  createdAt: string;
}

export interface Permission {
  id: string;
  name: string;
  description: string;
  module: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Strictly in-memory access token storage (no localStorage or sessionStorage)
let inMemoryAccessToken: string | null = null;
let inMemoryActiveBranchId: string | null = null;

export function setAccessToken(token: string | null) {
  inMemoryAccessToken = token;
}

export function getAccessToken(): string | null {
  return inMemoryAccessToken;
}

export function setActiveBranchId(branchId: string | null) {
  inMemoryActiveBranchId = branchId;
  if (typeof window !== 'undefined') {
    if (branchId) {
      localStorage.setItem('saranga_active_branch', branchId);
    } else {
      localStorage.removeItem('saranga_active_branch');
    }
  }
}

export function getActiveBranchId(): string | null {
  if (inMemoryActiveBranchId) return inMemoryActiveBranchId;
  if (typeof window !== 'undefined') {
    return localStorage.getItem('saranga_active_branch');
  }
  return null;
}

export class ApiError extends Error {
  status: number;
  data: any;

  constructor(status: number, message: string, data?: any) {
    super(message);
    this.status = status;
    this.data = data;
  }
}

export async function apiRequest<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const token = getAccessToken();
  const activeBranchId = getActiveBranchId();

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  if (activeBranchId) {
    headers['X-Branch-Id'] = activeBranchId;
  }

  // Include credentials for HttpOnly cookie transfer
  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    credentials: 'include',
    headers,
  });

  if (response.status === 401 && typeof window !== 'undefined' && !endpoint.includes('/auth/login') && !endpoint.includes('/auth/refresh')) {
    // Attempt silent refresh via HttpOnly cookie
    try {
      const refreshRes = await fetch(`${API_BASE_URL}/auth/refresh`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
      });

      if (refreshRes.ok) {
        const authData: AuthResponse = await refreshRes.json();
        setAccessToken(authData.accessToken);
        localStorage.setItem('saranga_user', JSON.stringify(authData.user));

        // Retry original request with new token
        headers['Authorization'] = `Bearer ${authData.accessToken}`;
        const retryRes = await fetch(`${API_BASE_URL}${endpoint}`, {
          ...options,
          credentials: 'include',
          headers,
        });

        if (!retryRes.ok) {
          const error = await retryRes.json().catch(() => ({}));
          throw new ApiError(retryRes.status, error.message || 'Request failed', error);
        }
        return retryRes.json();
      } else {
        setAccessToken(null);
        localStorage.removeItem('saranga_user');
      }
    } catch {
      setAccessToken(null);
      localStorage.removeItem('saranga_user');
    }
  }

  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new ApiError(response.status, error.message || 'Request failed', error);
  }

  // Handle empty responses (e.g. 204 or void actions)
  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    return response.json();
  }
  return {} as T;
}

// Module 2 API Helpers
export const branchApi = {
  getBranches: (isActive?: boolean) => {
    const query = isActive !== undefined ? `?isActive=${isActive}` : '';
    return apiRequest<Branch[]>(`/branches${query}`);
  },
  getBranchById: (id: string) => apiRequest<Branch>(`/branches/${id}`),
  createBranch: (data: { name: string; code: string; country: string; currencyCode: string; timeZone: string; isActive?: boolean }) =>
    apiRequest<Branch>('/branches', { method: 'POST', body: JSON.stringify(data) }),
  updateBranch: (id: string, data: { name: string; country: string; currencyCode: string; timeZone: string; isActive: boolean }) =>
    apiRequest<Branch>(`/branches/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleStatus: (id: string) => apiRequest<Branch>(`/branches/${id}/status`, { method: 'PATCH' }),
};

export const userApi = {
  getUsers: (params?: { search?: string; branchId?: string; roleId?: string; isActive?: boolean; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (params?.search) q.append('search', params.search);
    if (params?.branchId) q.append('branchId', params.branchId);
    if (params?.roleId) q.append('roleId', params.roleId);
    if (params?.isActive !== undefined) q.append('isActive', String(params.isActive));
    if (params?.page) q.append('page', String(params.page));
    if (params?.pageSize) q.append('pageSize', String(params.pageSize));
    const qs = q.toString();
    return apiRequest<PagedResult<User>>(`/users${qs ? `?${qs}` : ''}`);
  },
  getUserById: (id: string) => apiRequest<User>(`/users/${id}`),
  createUser: (data: { username: string; email: string; password: string; fullName: string; roleIds: string[]; branchIds: string[]; defaultBranchId?: string | null; isGlobalAdmin?: boolean }) =>
    apiRequest<User>('/users', { method: 'POST', body: JSON.stringify(data) }),
  updateUser: (id: string, data: { email: string; fullName: string; defaultBranchId?: string | null }) =>
    apiRequest<User>(`/users/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleStatus: (id: string) => apiRequest<User>(`/users/${id}/status`, { method: 'PATCH' }),
  assignRoles: (id: string, roleIds: string[]) =>
    apiRequest<User>(`/users/${id}/roles`, { method: 'POST', body: JSON.stringify({ roleIds }) }),
  assignBranches: (id: string, branchIds: string[], defaultBranchId?: string | null) =>
    apiRequest<User>(`/users/${id}/branches`, { method: 'POST', body: JSON.stringify({ branchIds, defaultBranchId }) }),
  resetPassword: (id: string, newPassword: string) =>
    apiRequest<{ message: string }>(`/users/${id}/reset-password`, { method: 'POST', body: JSON.stringify({ newPassword }) }),
};

export const roleApi = {
  getRoles: () => apiRequest<Role[]>('/roles'),
  getRoleById: (id: string) => apiRequest<Role>(`/roles/${id}`),
  createRole: (data: { name: string; description: string; permissionIds: string[] }) =>
    apiRequest<Role>('/roles', { method: 'POST', body: JSON.stringify(data) }),
  updateRole: (id: string, data: { description: string; permissionIds: string[] }) =>
    apiRequest<Role>(`/roles/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  assignPermissions: (id: string, permissionIds: string[]) =>
    apiRequest<Role>(`/roles/${id}/permissions`, { method: 'POST', body: JSON.stringify({ permissionIds }) }),
};

export const permissionApi = {
  getPermissions: () => apiRequest<Permission[]>('/permissions'),
};
