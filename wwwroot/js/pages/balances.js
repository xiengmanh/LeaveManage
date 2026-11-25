import { api, singlePage } from '../core/api.js';
import { h, icon } from '../core/dom.js';
import { createDataTable } from '../components/data-table.js';
import { openFormModal } from '../components/modal-form.js';
import { toast } from '../components/toast.js';
import { iconButton, pageHeader } from '../components/ui.js';

export async function renderBalances(container, { user, signal }) {
  const isAdmin = user.role === 'Admin';
  const year = new Date().getFullYear();
  const leaveTypes = (await api('/api/leave-types')).filter((t) => t.allowsDeduction);
  const typeOptions = leaveTypes.map((t) => ({ value: t.id, label: t.name }));
  const yearOptions = [year - 1, year, year + 1].map((y) => ({ value: y, label: `Năm ${y}` }));

  let employeeOptions = [];
  if (user.role !== 'User') {
    const page = await api('/api/employees', { query: { page: 1, pageSize: 100 } });
    employeeOptions = page.items.map((e) => ({ value: e.id, label: `${e.employeeCode} - ${e.fullName}` }));
  }

  async function openCreate() {
    const created = await openFormModal({
      title: 'Cấp số dư phép',
      fields: [
        { name: 'employeeId', label: 'Nhân viên', type: 'select', required: true, options: employeeOptions, valueType: 'number' },
        { name: 'leaveTypeId', label: 'Loại nghỉ (có trừ số dư)', type: 'select', required: true, options: typeOptions, valueType: 'number', colClass: 'col-sm-6' },
        { name: 'year', label: 'Năm', type: 'number', required: true, min: 2000, max: 2100, colClass: 'col-sm-6' },
        { name: 'initialDays', label: 'Số ngày được cấp', type: 'number', required: true, min: 0, max: 366 }
      ],
      initial: { year, initialDays: 12 },
      onSubmit: (v) => api('/api/leave-balances', { method: 'POST', body: v })
    });
    if (created) { toast('Đã cấp số dư phép.', 'success'); table.reload(); }
  }

  async function openEdit(b) {
    const updated = await openFormModal({
      title: `Điều chỉnh số dư: ${b.employeeName} – ${b.leaveTypeName} ${b.year}`,
      fields: [{
        name: 'initialDays', label: 'Số ngày được cấp', type: 'number', required: true, min: 0, max: 366,
        help: `Đã dùng ${b.usedDays}, đang chờ duyệt ${b.pendingDays}. Không được nhỏ hơn ${b.usedDays + b.pendingDays}.`
      }],
      initial: { initialDays: b.initialDays },
      onSubmit: (v) => api(`/api/leave-balances/${b.id}`, { method: 'PUT', body: v })
    });
    if (updated) { toast('Đã điều chỉnh số dư.', 'success'); table.reload(); }
  }

  const filters = [
    { name: 'year', label: 'Năm', options: yearOptions },
    { name: 'leaveTypeId', label: 'Loại nghỉ', options: typeOptions }
  ];
  if (employeeOptions.length > 1) filters.push({ name: 'employeeId', label: 'Nhân viên', options: employeeOptions });

  container.append(pageHeader('Số dư phép', 'Còn lại = Được cấp − Đã dùng − Đang chờ duyệt.'));
  const host = h('div');
  container.append(host);
  const table = createDataTable(host, {
    signal,
    search: false,
    caption: 'Số dư phép',
    filters,
    initialFilters: { year },
    toolbarActions: isAdmin ? [h('button', { type: 'button', class: 'btn btn-primary', onclick: openCreate }, icon('plus-lg', 'me-1'), 'Cấp số dư')] : [],
    emptyMessage: 'Chưa có số dư phép.',
    columns: [
      { key: 'employee', label: 'Nhân viên', render: (b) => h('div', {}, h('div', { class: 'fw-semibold' }, b.employeeName), h('div', { class: 'small text-secondary' }, b.employeeCode)) },
      { key: 'leaveTypeName', label: 'Loại nghỉ' },
      { key: 'year', label: 'Năm', className: 'text-center' },
      { key: 'initialDays', label: 'Được cấp', className: 'text-center' },
      { key: 'usedDays', label: 'Đã dùng', className: 'text-center' },
      { key: 'pendingDays', label: 'Đang chờ', className: 'text-center' },
      { key: 'remainingDays', label: 'Còn lại', className: 'text-center fw-semibold' },
      ...(isAdmin ? [{ key: 'actions', label: 'Thao tác', className: 'text-end', render: (b) => iconButton('pencil', 'Điều chỉnh', () => openEdit(b)) }] : [])
    ],
    fetchPage: async (params, s) => singlePage(await api('/api/leave-balances', {
      query: { year: params.year, leaveTypeId: params.leaveTypeId, employeeId: params.employeeId }, signal: s
    }))
  });
}
