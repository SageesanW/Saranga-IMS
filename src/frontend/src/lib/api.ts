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

// Module 3: Attributes Master Types
export interface Material {
  id: string;
  name: string;
  code: string;
  description: string;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
}

export interface Category {
  id: string;
  name: string;
  code: string;
  description: string;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
}

export interface Purity {
  id: string;
  name: string;
  karatValue: number;
  purityPercentage: number;
  code: string;
  description: string;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
}

export interface SpecificationOption {
  id: string;
  specificationId: string;
  value: string;
  code: string;
  description: string;
  isActive: boolean;
  sortOrder: number;
}

export interface Specification {
  id: string;
  name: string;
  code: string;
  description: string;
  inputType: string;
  isActive: boolean;
  sortOrder: number;
  options: SpecificationOption[];
  createdAt: string;
}

export interface StockLocation {
  id: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  name: string;
  code: string;
  locationType: string;
  description: string;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
}

export interface MakingChargeMethod {
  id: number;
  name: string;
  code: string;
  description: string;
  isDefault: boolean;
}

// Module 3 API Helpers
export const materialApi = {
  getMaterials: (isActive?: boolean) => {
    const q = isActive !== undefined ? `?isActive=${isActive}` : '';
    return apiRequest<Material[]>(`/materials${q}`);
  },
  getMaterialById: (id: string) => apiRequest<Material>(`/materials/${id}`),
  createMaterial: (data: { name: string; code: string; description?: string; isActive?: boolean; sortOrder?: number }) =>
    apiRequest<Material>('/materials', { method: 'POST', body: JSON.stringify(data) }),
  updateMaterial: (id: string, data: { name: string; description?: string; isActive: boolean; sortOrder: number }) =>
    apiRequest<Material>(`/materials/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleStatus: (id: string) => apiRequest<Material>(`/materials/${id}/status`, { method: 'PATCH' }),
};

export const categoryApi = {
  getCategories: (isActive?: boolean) => {
    const q = isActive !== undefined ? `?isActive=${isActive}` : '';
    return apiRequest<Category[]>(`/categories${q}`);
  },
  getCategoryById: (id: string) => apiRequest<Category>(`/categories/${id}`),
  createCategory: (data: { name: string; code: string; description?: string; isActive?: boolean; sortOrder?: number }) =>
    apiRequest<Category>('/categories', { method: 'POST', body: JSON.stringify(data) }),
  updateCategory: (id: string, data: { name: string; description?: string; isActive: boolean; sortOrder: number }) =>
    apiRequest<Category>(`/categories/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleStatus: (id: string) => apiRequest<Category>(`/categories/${id}/status`, { method: 'PATCH' }),
};

export const purityApi = {
  getPurities: (isActive?: boolean) => {
    const q = isActive !== undefined ? `?isActive=${isActive}` : '';
    return apiRequest<Purity[]>(`/purities${q}`);
  },
  getPurityById: (id: string) => apiRequest<Purity>(`/purities/${id}`),
  createPurity: (data: { name: string; karatValue: number; purityPercentage: number; code: string; description?: string; isActive?: boolean; sortOrder?: number }) =>
    apiRequest<Purity>('/purities', { method: 'POST', body: JSON.stringify(data) }),
  updatePurity: (id: string, data: { name: string; karatValue: number; purityPercentage: number; description?: string; isActive: boolean; sortOrder: number }) =>
    apiRequest<Purity>(`/purities/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleStatus: (id: string) => apiRequest<Purity>(`/purities/${id}/status`, { method: 'PATCH' }),
};

export const specificationApi = {
  getSpecifications: (isActive?: boolean) => {
    const q = isActive !== undefined ? `?isActive=${isActive}` : '';
    return apiRequest<Specification[]>(`/specifications${q}`);
  },
  getSpecificationById: (id: string) => apiRequest<Specification>(`/specifications/${id}`),
  createSpecification: (data: { name: string; code: string; description?: string; isActive?: boolean; sortOrder?: number }) =>
    apiRequest<Specification>('/specifications', { method: 'POST', body: JSON.stringify(data) }),
  updateSpecification: (id: string, data: { name: string; description?: string; isActive: boolean; sortOrder: number }) =>
    apiRequest<Specification>(`/specifications/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleStatus: (id: string) => apiRequest<Specification>(`/specifications/${id}/status`, { method: 'PATCH' }),
  addOption: (specificationId: string, data: { value: string; code: string; description?: string; isActive?: boolean; sortOrder?: number }) =>
    apiRequest<SpecificationOption>(`/specifications/${specificationId}/options`, { method: 'POST', body: JSON.stringify(data) }),
  toggleOptionStatus: (specificationId: string, optionId: string) =>
    apiRequest<SpecificationOption>(`/specifications/${specificationId}/options/${optionId}/status`, { method: 'PATCH' }),
};

export const stockLocationApi = {
  getStockLocations: (branchId?: string, isActive?: boolean) => {
    const q = new URLSearchParams();
    if (branchId) q.append('branchId', branchId);
    if (isActive !== undefined) q.append('isActive', String(isActive));
    const qs = q.toString();
    return apiRequest<StockLocation[]>(`/stock-locations${qs ? `?${qs}` : ''}`);
  },
  getStockLocationById: (id: string) => apiRequest<StockLocation>(`/stock-locations/${id}`),
  createStockLocation: (data: { branchId: string; name: string; code: string; locationType?: string; description?: string; isActive?: boolean; sortOrder?: number }) =>
    apiRequest<StockLocation>('/stock-locations', { method: 'POST', body: JSON.stringify(data) }),
  updateStockLocation: (id: string, data: { name: string; locationType?: string; description?: string; isActive: boolean; sortOrder: number }) =>
    apiRequest<StockLocation>(`/stock-locations/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleStatus: (id: string) => apiRequest<StockLocation>(`/stock-locations/${id}/status`, { method: 'PATCH' }),
};

export const makingChargeMethodApi = {
  getMakingChargeMethods: () => apiRequest<MakingChargeMethod[]>('/making-charge-methods'),
};

// Module 4: Inventory Core Types
export interface InventoryItemSpecification {
  id: string;
  specificationId: string;
  specificationCode: string;
  specificationName: string;
  specificationOptionId?: string | null;
  optionCode?: string | null;
  optionValue?: string | null;
  customValue?: string | null;
}

export interface InventoryItemMedia {
  id: string;
  fileName: string;
  storagePath: string;
  contentType: string;
  fileSize: number;
  mediaType: number;
  mediaTypeName: string;
  sortOrder: number;
  isPrimary: boolean;
  uploadedAt: string;
}

export interface InventoryItem {
  id: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  stockLocationId: string;
  locationCode: string;
  locationName: string;
  locationType: string;
  name: string;
  itemCode: string;
  tagNumber: string;
  barcode: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  categoryId: string;
  categoryCode: string;
  categoryName: string;
  purityId: string;
  purityCode: string;
  purityName: string;
  karatValue: number;
  purityPercentage: number;
  grossWeight: number;
  stoneWeight: number;
  netWeight: number;
  pieceCount: number;
  makingChargeMethod: number;
  makingChargeMethodName: string;
  makingChargeValue: number;
  status: number;
  statusName: string;
  description: string;
  isActive: boolean;
  specifications: InventoryItemSpecification[];
  media: InventoryItemMedia[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface InventoryLookup {
  id: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  stockLocationId: string;
  locationCode: string;
  locationName: string;
  tagNumber: string;
  barcode: string;
  name: string;
  materialName: string;
  categoryName: string;
  karatValue: number;
  grossWeight: number;
  stoneWeight: number;
  netWeight: number;
  pieceCount: number;
  status: number;
  statusName: string;
  makingChargeMethod: number;
  makingChargeValue: number;
}

export interface InventoryQueryParams {
  search?: string;
  branchId?: string;
  stockLocationId?: string;
  materialId?: string;
  categoryId?: string;
  purityId?: string;
  status?: number;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

// Module 4 API Helpers
export const inventoryApi = {
  getInventory: (params?: InventoryQueryParams) => {
    const q = new URLSearchParams();
    if (params?.search) q.append('search', params.search);
    if (params?.branchId) q.append('branchId', params.branchId);
    if (params?.stockLocationId) q.append('stockLocationId', params.stockLocationId);
    if (params?.materialId) q.append('materialId', params.materialId);
    if (params?.categoryId) q.append('categoryId', params.categoryId);
    if (params?.purityId) q.append('purityId', params.purityId);
    if (params?.status !== undefined) q.append('status', String(params.status));
    if (params?.isActive !== undefined) q.append('isActive', String(params.isActive));
    if (params?.page) q.append('page', String(params.page));
    if (params?.pageSize) q.append('pageSize', String(params.pageSize));
    const qs = q.toString();
    return apiRequest<PagedResult<InventoryItem>>(`/inventory${qs ? `?${qs}` : ''}`);
  },
  getInventoryItemById: (id: string) => apiRequest<InventoryItem>(`/inventory/${id}`),
  createInventoryItem: (data: {
    branchId: string;
    stockLocationId: string;
    name: string;
    itemCode?: string;
    tagNumber?: string;
    barcode?: string;
    materialId: string;
    categoryId: string;
    purityId: string;
    grossWeight: number;
    stoneWeight: number;
    pieceCount?: number;
    makingChargeMethod?: number;
    makingChargeValue?: number;
    description?: string;
    isActive?: boolean;
    specifications?: { specificationId: string; specificationOptionId?: string | null; customValue?: string | null }[];
  }) => apiRequest<InventoryItem>('/inventory', { method: 'POST', body: JSON.stringify(data) }),
  updateInventoryItem: (id: string, data: {
    stockLocationId: string;
    name: string;
    itemCode?: string;
    materialId: string;
    categoryId: string;
    purityId: string;
    grossWeight: number;
    stoneWeight: number;
    pieceCount: number;
    makingChargeMethod: number;
    makingChargeValue: number;
    description?: string;
    isActive: boolean;
    specifications?: { specificationId: string; specificationOptionId?: string | null; customValue?: string | null }[];
  }) => apiRequest<InventoryItem>(`/inventory/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  toggleActive: (id: string) => apiRequest<InventoryItem>(`/inventory/${id}/toggle-active`, { method: 'PATCH' }),
  toggleStatus: (id: string) => apiRequest<InventoryItem>(`/inventory/${id}/toggle-active`, { method: 'PATCH' }),
  adminStatusCorrection: (id: string, targetStatus: number, reason: string) =>
    apiRequest<InventoryItem>(`/inventory/${id}/admin-status-correction`, {
      method: 'POST',
      body: JSON.stringify({ targetStatus, reason }),
    }),
  lookupByBarcode: (barcode: string) => apiRequest<InventoryLookup>(`/inventory/lookup/barcode/${encodeURIComponent(barcode)}`),
  lookupByTag: (tagNumber: string) => apiRequest<InventoryLookup>(`/inventory/lookup/tag/${encodeURIComponent(tagNumber)}`),
};
