import { api, toTablePage } from '../core/api.js';
import { h } from '../core/dom.js';
import { formatDateTime } from '../core/format.js';
import { createDataTable } from '../components/data-table.js';
import { badge, pageHeader } from '../components/ui.js';

// Nhật ký hoạt động: Admin xem của tất cả tài khoản, tài khoản khác chỉ xem của chính mình.
export async function renderActivity(container, { user, signal }) {
  const isAdmin = user.role === 'Admin';
  container.append(pageHeader('Nhật ký hoạt động',
    isAdmin ? 'Hoạt động của tất cả tài khoản trong hệ thống.' : 'Các hoạt động của riêng tài khoản bạn.'));
  const host = h('div');
  container.append(host);
  createDataTable(host, {
    signal,
    caption: 'Nhật ký hoạt động',
    searchPlaceholder: isAdmin ? 'Tìm theo tài khoản hoặc hành động...' : 'Tìm theo hành động...',
    filters: [{ name: 'success', label: 'Kết quả', options: [{ value: 'true', label: 'Thành công' }, { value: 'false', label: 'Thất bại' }] }],
    emptyMessage: 'Chưa có hoạt động nào.',
    columns: [
      { key: 'createdAt', label: 'Thời điểm', render: (l) => formatDateTime(l.createdAt), className: 'text-nowrap' },
      ...(isAdmin ? [{ key: 'username', label: 'Tài khoản', render: (l) => h('span', { class: 'fw-semibold' }, l.username) }] : []),
      { key: 'action', label: 'Hành động' },
      { key: 'isSuccess', label: 'Kết quả', render: (l) => l.isSuccess ? badge('Thành công', 'success', 'check-circle') : badge(`Thất bại (${l.statusCode})`, 'danger', 'x-circle') },
      { key: 'ipAddress', label: 'Địa chỉ IP', render: (l) => l.ipAddress ?? '', className: 'text-nowrap text-secondary' }
    ],
    fetchPage: async (params, s) => toTablePage(await api('/api/activity-logs', {
      query: { page: params.pageNumber, pageSize: params.pageSize, search: params.searchTerm, success: params.success },
      signal: s
    }))
  });
}
