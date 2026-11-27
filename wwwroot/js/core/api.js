import { clearSession, getToken, getSession, saveSession } from './session.js';

// Lỗi từ API. Backend trả { statusCode, message, errorCode, traceId } (middleware)
// hoặc ProblemDetails { title, errors } khi dữ liệu không hợp lệ (400).
export class ApiError extends Error {
  constructor(status, body) {
    super(ApiError.pickMessage(status, body));
    this.name = 'ApiError';
    this.status = status;
    this.code = body?.errorCode ?? body?.code ?? null;
    this.errors = body?.errors ?? null;
    this.traceId = body?.traceId ?? null;
  }

  static pickMessage(status, body) {
    if (body?.message) return body.message;
    if (body?.errors) return 'Dữ liệu không hợp lệ, vui lòng kiểm tra các ô được đánh dấu.';
    if (status === 0) return 'Không kết nối được tới máy chủ. Vui lòng kiểm tra server đang chạy.';
    if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
    if (status === 403) return 'Bạn không có quyền thực hiện chức năng này.';
    if (status === 404) return 'Không tìm thấy dữ liệu.';
    if (status === 409) return 'Thao tác bị xung đột với dữ liệu hiện tại.';
    if (status >= 500) return 'Máy chủ gặp lỗi. Vui lòng thử lại sau.';
    return 'Yêu cầu không hợp lệ.';
  }
}

let refreshing = null;

// Access token hết hạn → gọi refresh; trình duyệt tự gửi cookie HttpOnly lm_refresh (chỉ 1 request tại một thời điểm)
async function tryRefresh() {
  if (!getSession()) return false;
  refreshing ??= fetch('/api/users/refresh', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { Accept: 'application/json' }
  }).then(async (response) => {
    if (!response.ok) return false;
    saveSession(await response.json());
    return true;
  }).catch(() => false).finally(() => { refreshing = null; });
  return refreshing;
}

/**
 * Gọi API cùng origin, tự gắn "Authorization: Bearer <token>".
 * Gặp 401 thì thử refresh một lần rồi gọi lại; vẫn 401 thì báo hết phiên.
 */
export async function api(path, { method = 'GET', body, query, signal } = {}, retried = false) {
  const url = new URL(path, window.location.origin);
  for (const [key, value] of Object.entries(query || {})) {
    if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, value);
  }

  const headers = { Accept: 'application/json' };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let response;
  try {
    response = await fetch(url, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined, signal });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError(0, null);
  }

  if (response.status === 401 && token && !retried) {
    if (await tryRefresh()) return api(path, { method, body, query, signal }, true);
    clearSession();
    window.dispatchEvent(new CustomEvent('auth:expired'));
  }

  if (response.status === 204) return null;
  const data = await response.json().catch(() => null);
  if (!response.ok) throw new ApiError(response.status, data);
  return data;
}

/** Đổi PaginatedResponse của API ({page, hasNextPage...}) sang dạng data-table dùng. */
export function toTablePage(result) {
  return {
    items: result.items,
    totalCount: result.totalCount,
    pageNumber: result.page,
    pageSize: result.pageSize,
    totalPages: result.totalPages,
    hasPrevious: result.hasPreviousPage,
    hasNext: result.hasNextPage
  };
}

/** Bảng không phân trang phía server (danh sách ngắn) → giả lập 1 trang. */
export function singlePage(items) {
  return { items, totalCount: items.length, pageNumber: 1, pageSize: Math.max(items.length, 1), totalPages: 1, hasPrevious: false, hasNext: false };
}
