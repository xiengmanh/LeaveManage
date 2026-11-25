import { api } from '../core/api.js';
import { h, icon } from '../core/dom.js';
import { formatDateTime } from '../core/format.js';
import { confirmDialog } from '../components/confirm.js';
import { createDataTable } from '../components/data-table.js';
import { openFormModal } from '../components/modal-form.js';
import { ROLE_LABELS } from '../components/layout.js';
import { toast } from '../components/toast.js';
import { activeBadge, badge, iconButton, pageHeader } from '../components/ui.js';

const ROLE_OPTIONS = Object.entries(ROLE_LABELS).map(([value, label]) => ({ value, label }));

export async function renderUsers(container, { user, signal }) {
  const employees = (await api('/api/employees', { query: { page: 1, pageSize: 100 } })).items;
  const employeeName = new Map(employees.map((e) => [e.id, `${e.employeeCode} - ${e.fullName}`]));
  const employeeOptions = employees.map((e) => ({ value: e.id, label: employeeName.get(e.id) }));

  async function openCreate() {
    const created = await openFormModal({
      title: 'Cấp tài khoản',
      fields: [
        { name: 'username', label: 'Tên đăng nhập', required: true, maxLength: 50, colClass: 'col-sm-6' },
        { name: 'email', label: 'Email', type: 'email', required: true, maxLength: 100, colClass: 'col-sm-6' },
        { name: 'password', label: 'Mật khẩu ban đầu', type: 'password', required: true, minLength: 6, maxLength: 100, colClass: 'col-sm-6' },
        { name: 'role', label: 'Vai trò', type: 'select', required: true, options: ROLE_OPTIONS, colClass: 'col-sm-6' },
        { name: 'employeeId', label: 'Gắn với nhân viên', type: 'select', options: employeeOptions, valueType: 'number', placeholder: '-- Không gắn --', help: 'Mỗi nhân viên chỉ có tối đa 1 tài khoản.' },
        { name: 'age', label: 'Tuổi', type: 'number', required: true, min: 18, max: 60 }
      ],
      initial: { role: 'User', age: 25 },
      onSubmit: (v) => api('/api/users/register', { method: 'POST', body: v })
    });
    if (created) { toast(`Đã tạo tài khoản ${created.username}.`, 'success'); table.reload(); }
  }

  async function openEdit(account) {
    const updated = await openFormModal({
      title: `Sửa tài khoản ${account.username}`,
      fields: [
        { name: 'role', label: 'Vai trò', type: 'select', required: true, options: ROLE_OPTIONS, colClass: 'col-sm-6' },
        { name: 'employeeId', label: 'Gắn với nhân viên', type: 'select', options: employeeOptions, valueType: 'number', placeholder: '-- Giữ nguyên --', colClass: 'col-sm-6' },
        { name: 'password', label: 'Đặt lại mật khẩu (để trống = giữ nguyên)', type: 'password', minLength: 6, maxLength: 100 }
      ],
      initial: account,
      onSubmit: (v) => api(`/api/users/${account.id}`, { method: 'PUT', body: { role: v.role, employeeId: v.employeeId, password: v.password || null } })
    });
    if (updated) { toast('Đã cập nhật tài khoản.', 'success'); table.reload(); }
  }

  async function toggleLock(account) {
    const lock = account.isActive;
    const ok = await confirmDialog({
      title: lock ? 'Khóa tài khoản' : 'Mở khóa tài khoản',
      message: `${lock ? 'Khóa' : 'Mở khóa'} tài khoản ${account.username}?`,
      detail: lock ? 'Tài khoản bị khóa không đăng nhập hoặc làm mới token được nữa.' : null,
      confirmText: lock ? 'Khóa' : 'Mở khóa',
      danger: lock
    });
    if (!ok) return;
    try {
      await api(`/api/users/${account.id}`, { method: 'PUT', body: { isActive: !lock } });
      toast(lock ? 'Đã khóa tài khoản.' : 'Đã mở khóa tài khoản.', 'success');
      table.reload();
    } catch (error) {
      toast(error.message, 'danger');
    }
  }

  container.append(pageHeader('Tài khoản', 'Chỉ Admin cấp tài khoản; không có đăng ký công khai.'));
  const host = h('div');
  container.append(host);
  const table = createDataTable(host, {
    signal,
    caption: 'Danh sách tài khoản',
    searchPlaceholder: 'Tìm theo tên đăng nhập hoặc email...',
    filters: [{ name: 'role', label: 'Vai trò', options: ROLE_OPTIONS }],
    toolbarActions: [h('button', { type: 'button', class: 'btn btn-primary', onclick: openCreate }, icon('person-plus', 'me-1'), 'Cấp tài khoản')],
    emptyMessage: 'Chưa có tài khoản.',
    columns: [
      { key: 'username', label: 'Tên đăng nhập', className: 'fw-semibold' },
      { key: 'email', label: 'Email' },
      { key: 'role', label: 'Vai trò', render: (u) => badge(ROLE_LABELS[u.role] ?? u.role, u.role === 'Admin' ? 'primary' : u.role === 'Manager' ? 'info' : 'secondary') },
      { key: 'employeeId', label: 'Nhân viên', render: (u) => (u.employeeId ? employeeName.get(u.employeeId) ?? `#${u.employeeId}` : '—') },
      { key: 'isActive', label: 'Trạng thái', render: (u) => activeBadge(u.isActive, 'Hoạt động', 'Đã khóa') },
      { key: 'createdAt', label: 'Ngày tạo', render: (u) => formatDateTime(u.createdAt) },
      {
        key: 'actions', label: 'Thao tác', className: 'text-end text-nowrap',
        render: (u) => h('div', { class: 'd-inline-flex gap-1' },
          iconButton('pencil', 'Sửa', () => openEdit(u)),
          // Không cho tự khóa chính mình để tránh mất quyền quản trị
          u.username === user.username ? null
            : iconButton(u.isActive ? 'lock' : 'unlock', u.isActive ? 'Khóa' : 'Mở khóa', () => toggleLock(u), u.isActive ? 'outline-danger' : 'outline-success'))
      }
    ],
    // API /api/users trả PagedResult (pageNumber, totalPages...) đúng dạng data-table dùng
    fetchPage: (params, s) => api('/api/users', { query: { pageNumber: params.pageNumber, pageSize: params.pageSize, searchTerm: params.searchTerm, role: params.role }, signal: s })
  });
}
