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

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5062/api';

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
    ...(options.headers as Record<string, string>),
  };

  if (!(options.body instanceof FormData) && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }

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
  version: number;
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
    media?: { fileName: string; storagePath: string; contentType: string; fileSize: number; mediaType?: number; sortOrder?: number; isPrimary?: boolean; }[];
  }, idempotencyKey?: string) => {
    const headers: Record<string, string> = {};
    if (idempotencyKey) {
      headers['Idempotency-Key'] = idempotencyKey;
    }
    return apiRequest<InventoryItem>('/inventory', {
      method: 'POST',
      headers,
      body: JSON.stringify(data)
    });
  },
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
    media?: { fileName: string; storagePath: string; contentType: string; fileSize: number; mediaType?: number; sortOrder?: number; isPrimary?: boolean; }[];
    version?: number;
  }) => apiRequest<InventoryItem>(`/inventory/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  uploadMedia: async (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    const token = getAccessToken();
    const headers: HeadersInit = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;
    const res = await fetch(`${API_BASE_URL}/inventory/upload-file`, {
      method: 'POST',
      headers,
      body: formData,
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new ApiError(res.status, err.message || 'File upload failed');
    }
    return res.json() as Promise<{
      fileName: string;
      storagePath: string;
      contentType: string;
      fileSize: number;
      mediaType: number;
    }>;
  },
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

export interface CompositionMember {
  id: string;
  inventoryItemId: string;
  itemName: string;
  itemCode: string;
  tagNumber: string;
  barcode: string;
  categoryId: string;
  categoryName: string;
  categoryCode: string;
  materialName: string;
  purityKarat: number;
  grossWeight: number;
  stoneWeight: number;
  netWeight: number;
  pieceCount: number;
  status: number;
  statusName: string;
  sortOrder: number;
  isActive: boolean;
  addedAt: string;
  removedAt?: string | null;
}

export interface InventoryComposition {
  id: string;
  branchId: string;
  branchName: string;
  branchCode: string;
  stockLocationId: string;
  stockLocationName: string;
  stockLocationCode: string;
  name: string;
  type: number; // 1 = Assembly, 2 = Group
  typeName: string;
  tagNumber: string;
  barcode: string;
  status: number; // 1 = Active, 2 = Dissolved
  statusName: string;
  description: string;
  isActive: boolean;
  totalGrossWeight: number;
  totalStoneWeight: number;
  totalNetWeight: number;
  totalPieceCount: number;
  activeMemberCount: number;
  totalMemberCount: number;
  members: CompositionMember[];
  version: number;
  createdAt: string;
  createdBy?: string;
  updatedAt?: string;
}

export interface EligibleChildItem {
  id: string;
  tagNumber: string;
  barcode: string;
  itemCode: string;
  name: string;
  categoryId: string;
  categoryName: string;
  categoryCode: string;
  materialName: string;
  purityKarat: number;
  grossWeight: number;
  stoneWeight: number;
  netWeight: number;
  pieceCount: number;
}

export const compositionApi = {
  getCompositions: (params?: {
    branchId?: string;
    stockLocationId?: string;
    type?: number;
    status?: number;
    searchTerm?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const q = new URLSearchParams();
    if (params?.branchId) q.append('branchId', params.branchId);
    if (params?.stockLocationId) q.append('stockLocationId', params.stockLocationId);
    if (params?.type) q.append('type', String(params.type));
    if (params?.status) q.append('status', String(params.status));
    if (params?.searchTerm) q.append('searchTerm', params.searchTerm);
    if (params?.page) q.append('page', String(params.page));
    if (params?.pageSize) q.append('pageSize', String(params.pageSize));
    const qs = q.toString();
    return apiRequest<PagedResult<InventoryComposition>>(`/compositions${qs ? `?${qs}` : ''}`);
  },
  getCompositionById: (id: string) => apiRequest<InventoryComposition>(`/compositions/${id}`),
  lookupByTag: (tagNumber: string) => apiRequest<InventoryComposition>(`/compositions/lookup/tag/${encodeURIComponent(tagNumber)}`),
  lookupByBarcode: (barcode: string) => apiRequest<InventoryComposition>(`/compositions/lookup/barcode/${encodeURIComponent(barcode)}`),
  getEligibleChildren: (params: { branchId: string; stockLocationId: string; categoryId?: string; searchTerm?: string }) => {
    const q = new URLSearchParams();
    q.append('branchId', params.branchId);
    q.append('stockLocationId', params.stockLocationId);
    if (params.categoryId) q.append('categoryId', params.categoryId);
    if (params.searchTerm) q.append('searchTerm', params.searchTerm);
    return apiRequest<EligibleChildItem[]>(`/compositions/eligible-children?${q.toString()}`);
  },
  createComposition: (data: {
    branchId: string;
    stockLocationId: string;
    name: string;
    type: number;
    description?: string;
    childItemIds: string[];
  }) => apiRequest<InventoryComposition>('/compositions', { method: 'POST', body: JSON.stringify(data) }),
  updateComposition: (id: string, data: { name: string; description?: string; rowVersion?: number }) =>
    apiRequest<InventoryComposition>(`/compositions/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  addMember: (id: string, data: { inventoryItemId: string; sortOrder?: number }) =>
    apiRequest<InventoryComposition>(`/compositions/${id}/members`, { method: 'POST', body: JSON.stringify(data) }),
  removeMember: (id: string, inventoryItemId: string) =>
    apiRequest<InventoryComposition>(`/compositions/${id}/members/${inventoryItemId}`, { method: 'DELETE' }),
  dissolveComposition: (id: string, rowVersion?: number) =>
    apiRequest<InventoryComposition>(`/compositions/${id}/dissolve`, {
      method: 'POST',
      body: rowVersion ? JSON.stringify({ rowVersion }) : undefined,
    }),
};

export interface BulkImportRow {
  rowNumber: number;
  itemName: string;
  itemCode?: string;
  materialCode: string;
  categoryCode: string;
  purityCode: string;
  grossWeight: number;
  stoneWeight: number;
  netWeight: number;
  pieceCount: number;
  stockLocationCode: string;
  makingChargeMethod: string;
  makingChargeValue: number;
  description?: string;
  isValid: boolean;
  errors: string[];
}

export interface BulkImportError {
  rowNumber: number;
  field: string;
  message: string;
}

export interface BulkImportValidationResult {
  batchId: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  originalFileName: string;
  fileType: string;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  isAllValid: boolean;
  rows: BulkImportRow[];
  errors: BulkImportError[];
}

export interface ImportedItemSummary {
  id: string;
  itemName: string;
  tagNumber: string;
  barcode: string;
  grossWeight: number;
  netWeight: number;
  stockLocationCode: string;
}

export interface BulkImportResult {
  batchId: string;
  branchId: string;
  status: string;
  totalImported: number;
  confirmedAt: string;
  createdItems: ImportedItemSummary[];
}

export interface InventoryImportBatch {
  id: string;
  branchId: string;
  branchName: string;
  originalFileName: string;
  fileType: string;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  status: number;
  statusName: string;
  importedBy: string;
  confirmedAt?: string;
  createdAt: string;
}

export const bulkImportApi = {
  getTemplateDownloadUrl: (format: 'xlsx' | 'csv' = 'xlsx') =>
    `${API_BASE_URL}/inventory/import/template?format=${format}`,
  validateFile: async (branchId: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return apiRequest<BulkImportValidationResult>(`/inventory/import/validate?branchId=${encodeURIComponent(branchId)}`, {
      method: 'POST',
      body: formData,
    });
  },
  getBatchValidation: (batchId: string) =>
    apiRequest<BulkImportValidationResult>(`/inventory/import/batches/${batchId}`),
  confirmImport: (batchId: string, idempotencyKey?: string) =>
    apiRequest<BulkImportResult>('/inventory/import/confirm', {
      method: 'POST',
      headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
      body: JSON.stringify({ batchId }),
    }),
  getBatches: (branchId?: string) => {
    const q = branchId ? `?branchId=${encodeURIComponent(branchId)}` : '';
    return apiRequest<InventoryImportBatch[]>(`/inventory/import/batches${q}`);
  },
};

// ==========================================
// Module 8: Inventory Count & Physical Stock Verification
// ==========================================

export interface InventoryCount {
  id: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  stockLocationId: string;
  stockLocationCode: string;
  stockLocationName: string;
  status: number; // 1 = InProgress, 2 = RequiresReview, 3 = Completed, 4 = Cancelled
  statusName: string;
  startedAt: string;
  startedBy: string;
  startedByName?: string;
  reconciledAt?: string;
  reconciledBy?: string;
  reconciledByName?: string;
  completedAt?: string;
  completedBy?: string;
  completedByName?: string;
  cancelledAt?: string;
  cancelledBy?: string;
  cancellationReason?: string;
  expectedItemCount: number;
  countedExpectedItemCount: number;
  matchedCount: number;
  missingCount: number;
  unexpectedCount: number;
  progressPercentage: number;
  notes?: string;
  version: number;
}

export interface InventoryCountLine {
  id: string;
  inventoryCountId: string;
  inventoryItemId: string;
  expectedBranchId: string;
  expectedStockLocationId: string;
  tagNumberSnapshot: string;
  barcodeSnapshot: string;
  itemNameSnapshot: string;
  materialNameSnapshot: string;
  categoryNameSnapshot: string;
  purityNameSnapshot: string;
  grossWeightSnapshot: number;
  stoneWeightSnapshot: number;
  netWeightSnapshot: number;
  pieceCountSnapshot: number;
  expectedStatusSnapshot: string;
  isExpected: boolean;
  isCounted: boolean;
  countedAt?: string;
  countedBy?: string;
  result: number;
  resultName: string;
  resolutionStatus: number;
  resolutionStatusName: string;
  resolutionReasonCode?: string;
  resolutionNotes?: string;
  resolvedAt?: string;
  resolvedBy?: string;
}

export interface InventoryCountUnexpectedItem {
  id: string;
  inventoryCountId: string;
  inventoryItemId: string;
  observedBranchId: string;
  observedStockLocationId: string;
  currentSystemStockLocationId: string;
  currentSystemLocationNameSnapshot: string;
  tagNumberSnapshot: string;
  barcodeSnapshot: string;
  itemNameSnapshot: string;
  materialNameSnapshot: string;
  categoryNameSnapshot: string;
  purityNameSnapshot: string;
  grossWeightSnapshot: number;
  stoneWeightSnapshot: number;
  netWeightSnapshot: number;
  pieceCountSnapshot: number;
  currentSystemStatusSnapshot: string;
  scannedAt: string;
  scannedBy: string;
  resolutionStatus: number;
  resolutionStatusName: string;
  resolutionReasonCode?: string;
  resolutionNotes?: string;
  resolvedAt?: string;
  resolvedBy?: string;
}

export interface ScanResult {
  scanOutcome: 'MATCHED' | 'UNEXPECTED' | 'ALREADY_COUNTED' | 'COMPOSITION_PARENT' | 'CROSS_BRANCH' | 'NOT_FOUND';
  message: string;
  expectedLine?: InventoryCountLine | null;
  unexpectedItem?: InventoryCountUnexpectedItem | null;
  compositionPreview?: CompositionScanPreview | null;
  countSummary: InventoryCount;
}

export interface CompositionMemberScanPreview {
  inventoryItemId: string;
  tagNumber: string;
  barcode: string;
  itemName: string;
  categoryName: string;
  purityKarat: number;
  grossWeight: number;
  isAlreadyCounted: boolean;
  isExpectedInBaseline: boolean;
}

export interface CompositionScanPreview {
  compositionId: string;
  name: string;
  tagNumber: string;
  barcode: string;
  type: string;
  members: CompositionMemberScanPreview[];
}

export interface ConfirmCompositionScanResult {
  newlyCountedCount: number;
  alreadyCountedCount: number;
  unexpectedCount: number;
  message: string;
  countSummary: InventoryCount;
}

export interface CountVariances {
  countId: string;
  missingCount: number;
  unexpectedCount: number;
  missingItems: InventoryCountLine[];
  unexpectedItems: InventoryCountUnexpectedItem[];
}

export interface ActiveCountCheck {
  hasActiveCount: boolean;
  activeCountId?: string | null;
  status?: number | null;
  statusName?: string | null;
  startedAt?: string | null;
}

export const inventoryCountApi = {
  getCounts: (params?: { branchId?: string; stockLocationId?: string; status?: number; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (params?.branchId) q.append('branchId', params.branchId);
    if (params?.stockLocationId) q.append('stockLocationId', params.stockLocationId);
    if (params?.status) q.append('status', params.status.toString());
    if (params?.page) q.append('page', params.page.toString());
    if (params?.pageSize) q.append('pageSize', params.pageSize.toString());
    const qs = q.toString() ? `?${q.toString()}` : '';
    return apiRequest<PagedResult<InventoryCount>>(`/inventory-counts${qs}`);
  },
  getCountById: (id: string) =>
    apiRequest<InventoryCount>(`/inventory-counts/${id}`),
  getCountLines: (id: string, params?: { isCounted?: boolean; result?: number; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (params?.isCounted !== undefined) q.append('isCounted', params.isCounted.toString());
    if (params?.result !== undefined) q.append('result', params.result.toString());
    if (params?.page) q.append('page', params.page.toString());
    if (params?.pageSize) q.append('pageSize', params.pageSize.toString());
    const qs = q.toString() ? `?${q.toString()}` : '';
    return apiRequest<PagedResult<InventoryCountLine>>(`/inventory-counts/${id}/lines${qs}`);
  },
  getUnexpectedItems: (id: string) =>
    apiRequest<InventoryCountUnexpectedItem[]>(`/inventory-counts/${id}/unexpected`),
  getVariances: (id: string) =>
    apiRequest<CountVariances>(`/inventory-counts/${id}/variances`),
  checkActive: (branchId: string, stockLocationId: string) =>
    apiRequest<ActiveCountCheck>(`/inventory-counts/check-active?branchId=${encodeURIComponent(branchId)}&stockLocationId=${encodeURIComponent(stockLocationId)}`),
  startCount: (data: { branchId: string; stockLocationId: string; notes?: string }) =>
    apiRequest<InventoryCount>('/inventory-counts', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  scanItem: (id: string, identifier: string) =>
    apiRequest<ScanResult>(`/inventory-counts/${id}/scan`, {
      method: 'POST',
      body: JSON.stringify({ identifier }),
    }),
  confirmCompositionScan: (id: string, compositionId: string, selectedInventoryItemIds: string[]) =>
    apiRequest<ConfirmCompositionScanResult>(`/inventory-counts/${id}/scan-composition`, {
      method: 'POST',
      body: JSON.stringify({ compositionId, selectedInventoryItemIds }),
    }),
  finishCount: (id: string) =>
    apiRequest<InventoryCount>(`/inventory-counts/${id}/finish`, {
      method: 'POST',
    }),
  resolveVariance: (id: string, data: { varianceType: string; varianceId: string; reasonCode: string; notes?: string }) =>
    apiRequest<InventoryCount>(`/inventory-counts/${id}/variances/resolve`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  completeCount: (id: string, notes?: string) =>
    apiRequest<InventoryCount>(`/inventory-counts/${id}/complete`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    }),
  cancelCount: (id: string, reason: string) =>
    apiRequest<InventoryCount>(`/inventory-counts/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
};

// ==========================================
// Module 9: Rates, VAT & Reference Pricing
// ==========================================

export interface MetalRate {
  id: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  purityId: string;
  purityCode: string;
  purityName: string;
  purityKarat: number;
  purityPercentage: number;
  currencyCode: string;
  ratePerGram: number;
  source: string; // MANUAL, EXTERNAL_API, ADMIN_OVERRIDE
  externalReferenceRate?: number;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
  isOverride: boolean;
  overrideReason?: string;
  createdAt: string;
  createdBy?: string;
  createdByName?: string;
}

export interface ExternalRateProviderStatus {
  providerName: string;
  isAvailable: boolean;
  lastSuccessfulRefreshAt?: string;
  lastAttemptAt?: string;
  lastErrorMessage?: string;
  totalActiveRatesManaged: number;
}

export interface VatConfiguration {
  id: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  name: string;
  isEnabled: boolean;
  ratePercentage: number;
  effectiveFrom: string;
  effectiveTo?: string;
  calculationMode: string;
  isActive: boolean;
  createdAt: string;
  createdBy?: string;
  createdByName?: string;
  updatedAt?: string;
  updatedBy?: string;
  updatedByName?: string;
}

export interface ReferencePriceBreakdown {
  branchId: string;
  branchCode: string;
  currencyCode: string;
  materialId: string;
  materialName: string;
  purityId: string;
  purityName: string;
  purityKarat: number;
  ratePerGramUsed: number;
  rateSource: string;
  isRateOverridden: boolean;
  rateOverrideReason?: string;
  netWeight: number;
  referenceMaterialValue: number;
  makingChargeMethod: number;
  makingChargeMethodName: string;
  makingChargeValue: number;
  pieceCount: number;
  makingChargeAmount: number;
  subtotalBeforeTax: number;
  vatEnabled: boolean;
  vatRatePercentage: number;
  vatAmount: number;
  referenceTotal: number;
  calculatedAt: string;
}

export const metalRateApi = {
  getActiveRates: (branchId?: string, currencyCode?: string) => {
    const params = new URLSearchParams();
    if (branchId) params.append('branchId', branchId);
    if (currencyCode) params.append('currencyCode', currencyCode);
    const qs = params.toString() ? `?${params.toString()}` : '';
    return apiRequest<MetalRate[]>(`/metal-rates${qs}`);
  },
  getActiveRate: (branchId: string, materialId: string, purityId: string, currencyCode?: string) => {
    const params = new URLSearchParams({ branchId, materialId, purityId });
    if (currencyCode) params.append('currencyCode', currencyCode);
    return apiRequest<MetalRate>(`/metal-rates/active?${params.toString()}`);
  },
  getRateHistory: (params: { branchId?: string; materialId?: string; purityId?: string; currencyCode?: string; page?: number; pageSize?: number }) => {
    const p = new URLSearchParams();
    if (params.branchId) p.append('branchId', params.branchId);
    if (params.materialId) p.append('materialId', params.materialId);
    if (params.purityId) p.append('purityId', params.purityId);
    if (params.currencyCode) p.append('currencyCode', params.currencyCode);
    if (params.page) p.append('page', params.page.toString());
    if (params.pageSize) p.append('pageSize', params.pageSize.toString());
    return apiRequest<PagedResult<MetalRate>>(`/metal-rates/history?${p.toString()}`);
  },
  createManualRate: (data: { branchId: string; materialId: string; purityId: string; ratePerGram: number; currencyCode?: string; effectiveFrom?: string; notes?: string }) =>
    apiRequest<MetalRate>('/metal-rates/manual', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  adminOverrideRate: (data: { branchId: string; materialId: string; purityId: string; overrideRatePerGram: number; overrideReason: string; currencyCode?: string }) =>
    apiRequest<MetalRate>('/metal-rates/override', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  refreshExternalRates: (data: { branchId?: string; currencyCode?: string }) =>
    apiRequest<MetalRate[]>('/metal-rates/refresh-external', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  getProviderStatus: () =>
    apiRequest<ExternalRateProviderStatus>('/metal-rates/provider-status'),
};

export const vatConfigurationApi = {
  getActiveConfiguration: (branchId: string) =>
    apiRequest<VatConfiguration>(`/vat-configurations?branchId=${encodeURIComponent(branchId)}`),
  getVatHistory: (params: { branchId?: string; page?: number; pageSize?: number }) => {
    const p = new URLSearchParams();
    if (params.branchId) p.append('branchId', params.branchId);
    if (params.page) p.append('page', params.page.toString());
    if (params.pageSize) p.append('pageSize', params.pageSize.toString());
    return apiRequest<PagedResult<VatConfiguration>>(`/vat-configurations/history?${p.toString()}`);
  },
  createOrUpdateConfiguration: (data: { branchId: string; name: string; isEnabled: boolean; ratePercentage: number; effectiveFrom?: string; calculationMode?: string }) =>
    apiRequest<VatConfiguration>('/vat-configurations', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  toggleVatStatus: (data: { branchId: string; isEnabled: boolean; reason?: string }) =>
    apiRequest<VatConfiguration>('/vat-configurations/toggle', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
};

export const pricingApi = {
  calculateReferencePrice: (data: { branchId: string; materialId: string; purityId: string; netWeight: number; pieceCount: number; makingChargeMethod: number; makingChargeValue: number; currencyCode?: string }) =>
    apiRequest<ReferencePriceBreakdown>('/pricing/calculate-reference', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
};

// Module 10: Customer Management Types & API
export interface Customer {
  id: string;
  customerCode: string;
  fullName: string;
  primaryPhone: string;
  normalizedPrimaryPhone: string;
  secondaryPhone?: string | null;
  normalizedSecondaryPhone?: string | null;
  email?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  districtProvince?: string | null;
  postalCode?: string | null;
  country?: string | null;
  dateOfBirth?: string | null;
  notes?: string | null;
  isActive: boolean;
  rowVersion: string;
  createdAt: string;
  updatedAt?: string | null;
  createdBy?: string | null;
  updatedBy?: string | null;
}

export interface CustomerLookup {
  id: string;
  customerCode: string;
  fullName: string;
  primaryPhone: string;
  email?: string | null;
  city?: string | null;
  isActive: boolean;
}

export interface CustomerDuplicateCheckResult {
  isDuplicate: boolean;
  duplicateReason?: string | null;
  existingCustomerId?: string | null;
}

export interface CashDrawer {
  id: string;
  branchId: string;
  drawerCode: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  hasActiveShift: boolean;
  activeShiftId?: string | null;
  activeCashierName?: string | null;
}

export interface Shift {
  id: string;
  shiftNumber: string;
  branchId: string;
  branchName?: string | null;
  cashDrawerId: string;
  cashDrawerName?: string | null;
  cashDrawerCode?: string | null;
  cashierId: string;
  cashierName?: string | null;
  status: number; // 0: Open, 1: Closed
  openedAt: string;
  openedBy: string;
  openedByName?: string | null;
  openingBalance: number;
  systemExpectedOpeningBalance: number;
  openingBalanceDifference: number;
  openingDifferenceReason?: string | null;
  openingNotes?: string | null;
  closedAt?: string | null;
  closedBy?: string | null;
  closedByName?: string | null;
  actualClosingBalance?: number | null;
  expectedClosingBalance?: number | null;
  closingVariance?: number | null;
  closingVarianceReason?: string | null;
  closingNotes?: string | null;
  version: number;
}

export interface OpenShiftRequest {
  branchId: string;
  cashDrawerId: string;
  openingBalance: number;
  openingDifferenceReason?: string | null;
  openingNotes?: string | null;
}

export interface CloseShiftRequest {
  actualClosingBalance: number;
  closingVarianceReason?: string | null;
  closingNotes?: string | null;
  rowVersion?: number;
}

export interface ShiftOpeningDefaults {
  cashDrawerId: string;
  drawerName: string;
  systemExpectedOpeningBalance: number;
  previousShiftClosedAt?: string | null;
  previousShiftNumber?: string | null;
}

export interface CreateCashDrawerRequest {
  branchId: string;
  drawerCode: string;
  name: string;
  description?: string | null;
}

export interface CreateCustomerRequest {
  fullName: string;
  primaryPhone: string;
  secondaryPhone?: string | null;
  email?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  districtProvince?: string | null;
  postalCode?: string | null;
  country?: string | null;
  dateOfBirth?: string | null;
  notes?: string | null;
}

export interface UpdateCustomerRequest {
  fullName: string;
  primaryPhone: string;
  secondaryPhone?: string | null;
  email?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  districtProvince?: string | null;
  postalCode?: string | null;
  country?: string | null;
  dateOfBirth?: string | null;
  notes?: string | null;
  rowVersion: string;
}

export interface UpdateCustomerStatusRequest {
  isActive: boolean;
  reason?: string | null;
}

export const customerApi = {
  getCustomers: (params?: {
    search?: string;
    isActive?: boolean;
    page?: number;
    pageSize?: number;
    sortBy?: string;
    sortDescending?: boolean;
  }) => {
    const p = new URLSearchParams();
    if (params?.search) p.append('search', params.search);
    if (params?.isActive !== undefined) p.append('isActive', params.isActive.toString());
    if (params?.page) p.append('page', params.page.toString());
    if (params?.pageSize) p.append('pageSize', params.pageSize.toString());
    if (params?.sortBy) p.append('sortBy', params.sortBy);
    if (params?.sortDescending !== undefined) p.append('sortDescending', params.sortDescending.toString());
    return apiRequest<PagedResult<Customer>>(`/customers?${p.toString()}`);
  },
  searchCustomers: (query: string, maxResults = 20) =>
    apiRequest<CustomerLookup[]>(`/customers/search?q=${encodeURIComponent(query)}&maxResults=${maxResults}`),
  getCustomerById: (id: string) =>
    apiRequest<Customer>(`/customers/${id}`),
  getCustomerByCode: (code: string) =>
    apiRequest<Customer>(`/customers/code/${encodeURIComponent(code)}`),
  checkDuplicate: (primaryPhone: string, email?: string | null, excludeCustomerId?: string | null) => {
    const p = new URLSearchParams();
    p.append('primaryPhone', primaryPhone);
    if (email) p.append('email', email);
    if (excludeCustomerId) p.append('excludeCustomerId', excludeCustomerId);
    return apiRequest<CustomerDuplicateCheckResult>(`/customers/check-duplicate?${p.toString()}`);
  },
  createCustomer: (data: CreateCustomerRequest) =>
    apiRequest<Customer>('/customers', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  updateCustomer: (id: string, data: UpdateCustomerRequest) =>
    apiRequest<Customer>(`/customers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  updateCustomerStatus: (id: string, data: UpdateCustomerStatusRequest) =>
    apiRequest<Customer>(`/customers/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify(data),
    }),
};

export const shiftApi = {
  openShift: (data: OpenShiftRequest) =>
    apiRequest<Shift>('/shifts/open', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  closeShift: (id: string, data: CloseShiftRequest) =>
    apiRequest<Shift>(`/shifts/${id}/close`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  getShiftById: (id: string) =>
    apiRequest<Shift>(`/shifts/${id}`),
  getActiveCashierShift: (cashierId?: string) =>
    apiRequest<Shift | null>(`/shifts/active-cashier${cashierId ? `?cashierId=${cashierId}` : ''}`),
  getActiveDrawerShift: (drawerId: string) =>
    apiRequest<Shift | null>(`/shifts/active-drawer/${drawerId}`),
  getOpeningDefaults: (drawerId: string) =>
    apiRequest<ShiftOpeningDefaults>(`/shifts/opening-defaults/${drawerId}`),
  getShifts: (params?: {
    branchId?: string;
    cashDrawerId?: string;
    cashierId?: string;
    status?: number;
    startDate?: string;
    endDate?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const p = new URLSearchParams();
    if (params?.branchId) p.append('branchId', params.branchId);
    if (params?.cashDrawerId) p.append('cashDrawerId', params.cashDrawerId);
    if (params?.cashierId) p.append('cashierId', params.cashierId);
    if (params?.status !== undefined) p.append('status', params.status.toString());
    if (params?.startDate) p.append('startDate', params.startDate);
    if (params?.endDate) p.append('endDate', params.endDate);
    if (params?.page) p.append('page', params.page.toString());
    if (params?.pageSize) p.append('pageSize', params.pageSize.toString());
    return apiRequest<PagedResult<Shift>>(`/shifts?${p.toString()}`);
  },
  getCashDrawers: (branchId: string) =>
    apiRequest<CashDrawer[]>(`/shifts/drawers?branchId=${branchId}`),
  createCashDrawer: (data: CreateCashDrawerRequest) =>
    apiRequest<CashDrawer>('/shifts/drawers', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
};
