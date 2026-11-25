import { api, toTablePage } from '../core/api.js';
import { h, icon } from '../core/dom.js';
import { formatDate, todayIso } from '../core/format.js';
import { confirmDialog } from '../components/confirm.js';
import { createDataTable } from '../components/data-table.js';
import { openFormModal } from '../components/modal-form.js';
import { toast } from '../components/toast.js';
import { activeBadge, iconButton, pageHeader } from '../components/ui.js';

export async function renderEmployees(container, { user, signal }) {
  const isAdmin = user.role === 'Admin';
  const [departments, positions] = await Promise.all([api('/api/departments'), api('/api/positions')]);
  const departmentOptions = departments.map((d) => ({ value: d.id, label: d.name }));
  const positionOptions = positions.map((p) => ({ value: p.id, label: p.name }));

  async function managerOptions(exceptId) {
    const page = await api('/api/employees', { query: { page: 1, pageSize: 100, isActive: true } });
    return page.items.filter((e) => e.id !== exceptId).map((e) => ({ value: e.id, label: `${e.employeeCode} - ${e.fullName}` }));
  }

  async function openForm(employee) {
    const managers = await managerOptions(employee?.id);
    const isEdit = Boolean(employee);
    const fields = [
      { name: 'employeeCode', label: 'Mã nhân viên', required: !isEdit, maxLength: 50, colClass: 'col-sm-6', placeholder: 'vd: NV007' },
      { name: 'fullName', label: 'Họ và tên', required: true, maxLength: 100, colClass: 'col-sm-6' },
      { name: 'email', label: 'Email', type: 'email', required: true, maxLength: 100, colClass: 'col-sm-6' },
      { name: 'phoneNumber', label: 'Số điện thoại', maxLength: 20, colClass: 'col-sm-6' },
      { name: 'dateOfBirth', label: 'Ngày sinh', type: 'date', required: true, max: todayIso(), colClass: 'col-sm-6' },
      { name: 'hireDate', label: 'Ngày vào làm', type: 'date', required: !isEdit, colClass: 'col-sm-6' },
      { name: 'departmentId', label: 'Phòng ban', type: 'select', required: true, options: departmentOptions, valueType: 'number', colClass: 'col-sm-6' },
      { name: 'positionId', label: 'Chức vụ', type: 'select', required: true, options: positionOptions, valueType: 'number', colClass: 'col-sm-6' },
      { name: 'managerId', label: 'Quản lý trực tiếp (người duyệt đơn)', type: 'select', options: managers, valueType: 'number', placeholder: '-- Không có --' }
    ];
    if (isEdit) {
      // Mã nhân viên và ngày vào làm không sửa qua API cập nhật
      fields.splice(0, 1);
      fields.splice(fields.findIndex((f) => f.name === 'hireDate'), 1);
      fields.push({ name: 'isActive', label: 'Đang làm việc', type: 'switch' });
    }

    const initial = isEdit ? {
      ...employee,
      dateOfBirth: String(employee.dateOfBirth).slice(0, 10),
      departmentId: employee.department?.id,
      positionId: employee.position?.id,
      managerId: employee.manager?.id
    } : { hireDate: todayIso() };

    const saved = await openFormModal({
      title: isEdit ? `Sửa nhân viên ${employee.employeeCode}` : 'Thêm nhân viên',
      fields,
      initial,
      size: 'modal-lg',
      onSubmit: (v) => {
        const body = { ...v, phoneNumber: v.phoneNumber || null };
        if (isEdit) {
          body.managerId = v.managerId ?? 0; // 0 = bỏ quản lý trực tiếp
          return api(`/api/employees/${employee.id}`, { method: 'PUT', body });
        }
        body.managerId = v.managerId ?? null;
        return api('/api/employees', { method: 'POST', body });
      }
    });
    if (saved) {
      toast(isEdit ? 'Đã cập nhật nhân viên.' : 'Đã thêm nhân viên. Hãy cấp tài khoản và số dư phép cho nhân viên này.', 'success');
      table.reload();
    }
  }

  async function deactivate(employee) {
    const ok = await confirmDialog({
      title: 'Cho nhân viên nghỉ việc',
      message: `Đánh dấu ${employee.fullName} đã nghỉ việc?`,
      detail: 'Dữ liệu và lịch sử đơn nghỉ được giữ lại; nhân viên không tạo đơn mới được nữa.',
      confirmText: 'Xác nhận'
    });
    if (!ok) return;
    try {
      await api(`/api/employees/${employee.id}`, { method: 'DELETE' });
      toast('Đã cập nhật trạng thái nhân viên.', 'success');
      table.reload();
    } catch (error) {
      toast(error.message, 'danger');
    }
  }

  const subtitle = isAdmin ? 'Toàn bộ nhân viên.' : user.role === 'Manager' ? 'Bạn và nhân viên cấp dưới trực tiếp.' : 'Thông tin của bạn.';
  container.append(pageHeader('Nhân viên', subtitle));
  const host = h('div');
  container.append(host);
  const table = createDataTable(host, {
    signal,
    caption: 'Danh sách nhân viên',
    searchPlaceholder: 'Tìm theo tên, mã hoặc email...',
    filters: [
      { name: 'departmentId', label: 'Phòng ban', options: departmentOptions },
      { name: 'isActive', label: 'Trạng thái', options: [{ value: 'true', label: 'Đang làm việc' }, { value: 'false', label: 'Đã nghỉ việc' }] }
    ],
    toolbarActions: isAdmin ? [h('button', { type: 'button', class: 'btn btn-primary', onclick: () => openForm(null) }, icon('person-plus', 'me-1'), 'Thêm nhân viên')] : [],
    emptyMessage: 'Không có nhân viên.',
    columns: [
      { key: 'employeeCode', label: 'Mã NV', className: 'text-nowrap' },
      { key: 'fullName', label: 'Họ tên', render: (e) => h('div', {}, h('div', { class: 'fw-semibold' }, e.fullName), h('div', { class: 'small text-secondary' }, e.email)) },
      { key: 'department', label: 'Phòng ban', render: (e) => e.department?.name ?? '' },
      { key: 'position', label: 'Chức vụ', render: (e) => e.position?.name ?? '' },
      { key: 'manager', label: 'Quản lý trực tiếp', render: (e) => e.manager?.fullName ?? '—' },
      { key: 'hireDate', label: 'Ngày vào làm', render: (e) => formatDate(e.hireDate) },
      { key: 'isActive', label: 'Trạng thái', render: (e) => activeBadge(e.isActive, 'Đang làm việc', 'Đã nghỉ việc') },
      ...(isAdmin ? [{
        key: 'actions', label: 'Thao tác', className: 'text-end text-nowrap',
        render: (e) => h('div', { class: 'd-inline-flex gap-1' },
          iconButton('pencil', 'Sửa', () => openForm(e)),
          e.isActive ? iconButton('person-x', 'Cho nghỉ việc', () => deactivate(e), 'outline-danger') : null)
      }] : [])
    ],
    fetchPage: async (params, s) => toTablePage(await api('/api/employees', {
      query: { page: params.pageNumber, pageSize: params.pageSize, search: params.searchTerm, departmentId: params.departmentId, isActive: params.isActive },
      signal: s
    }))
  });
}
