import { api, toTablePage } from '../core/api.js';
import { h, icon } from '../core/dom.js';
import { formatDate, formatDateTime, todayIso } from '../core/format.js';
import { confirmDialog } from '../components/confirm.js';
import { createDataTable } from '../components/data-table.js';
import { openFormModal } from '../components/modal-form.js';
import { toast } from '../components/toast.js';
import { iconButton, pageHeader, showInfoModal, statusBadge, STATUS_OPTIONS } from '../components/ui.js';

// ---------- Dùng chung ----------

async function loadLeaveTypes() {
  const types = await api('/api/leave-types');
  return types.map((t) => ({ value: t.id, label: t.name, raw: t }));
}

function queryFrom(params, extra = {}) {
  return { page: params.pageNumber, pageSize: params.pageSize, search: params.searchTerm, status: params.status, leaveTypeId: params.leaveTypeId, ...extra };
}

function dateRange(r) {
  return h('span', { class: 'text-nowrap' }, `${formatDate(r.startDate)} → ${formatDate(r.endDate)}`);
}

function employeeCell(r) {
  return h('div', {}, h('div', { class: 'fw-semibold' }, r.employeeName ?? ''), h('div', { class: 'small text-secondary' }, r.employeeCode ?? ''));
}

export async function showRequestDetail(id) {
  try {
    const r = await api(`/api/leave-requests/${id}`);
    const row = (label, value) => [h('dt', { class: 'col-sm-4 text-secondary' }, label), h('dd', { class: 'col-sm-8' }, value ?? '—')];
    showInfoModal(`Đơn ${r.requestNumber}`, h('div', {},
      h('dl', { class: 'row mb-3' },
        row('Nhân viên', `${r.employeeName} (${r.employeeCode})`),
        row('Loại nghỉ', r.leaveTypeName),
        row('Thời gian', `${formatDate(r.startDate)} → ${formatDate(r.endDate)}`),
        row('Số ngày làm việc', `${r.totalDays} ngày (hưởng lương ${r.paidDays} ngày)`),
        row('Trạng thái', statusBadge(r.status)),
        row('Lý do', r.reason),
        row('Liên hệ', [r.contactPhone, r.contactAddress].filter(Boolean).join(' · ') || null),
        row('Tạo lúc', formatDateTime(r.createdAt)),
        row('Gửi duyệt lúc', r.submittedAt ? formatDateTime(r.submittedAt) : null),
        r.status === 'Cancelled' ? row('Lý do hủy', r.cancelReason) : null),
      h('h3', { class: 'h6 fw-semibold' }, 'Lịch sử duyệt'),
      r.approvals.length === 0
        ? h('p', { class: 'text-secondary mb-0' }, 'Chưa có lượt duyệt nào.')
        : h('ul', { class: 'list-group' }, r.approvals.map((a) => h('li', { class: 'list-group-item' },
          h('div', { class: 'd-flex justify-content-between' },
            h('span', { class: 'fw-semibold' }, a.approverName ?? `#${a.approverId}`),
            statusBadge(a.status)),
          h('div', { class: 'small text-secondary' }, formatDateTime(a.actionDate)),
          a.comment ? h('div', {}, a.comment) : null)))));
  } catch (error) {
    toast(error.message, 'danger');
  }
}

function canCancel(r) {
  if (r.status === 'Draft' || r.status === 'Pending') return true;
  return r.status === 'Approved' && String(r.startDate).slice(0, 10) > todayIso();
}

async function cancelRequest(r, reload) {
  const values = await openFormModal({
    title: `Hủy đơn ${r.requestNumber}`,
    submitText: 'Hủy đơn',
    fields: [{ name: 'reason', label: 'Lý do hủy', type: 'textarea', maxLength: 255 }],
    onSubmit: (v) => api(`/api/leave-requests/${r.id}/cancel`, { method: 'POST', body: { reason: v.reason || null } })
  });
  if (values) {
    toast('Đã hủy đơn. Số dư phép (nếu có) đã được trả lại.', 'success');
    reload();
  }
}

// ---------- Đơn nghỉ của tôi ----------

export async function renderMyRequests(container, { user, signal }) {
  const leaveTypes = await loadLeaveTypes();
  const typeHelp = leaveTypes.map((t) => `${t.label}: báo trước ${t.raw.noticeDaysRequired ?? 0} ngày${t.raw.defaultDaysPerYear ? `, tối đa ${t.raw.defaultDaysPerYear} ngày/năm` : ''}${t.raw.allowsDeduction ? ', trừ số dư' : ''}`).join(' · ');

  const fields = [
    { name: 'leaveTypeId', label: 'Loại nghỉ', type: 'select', required: true, options: leaveTypes, valueType: 'number', help: typeHelp },
    { name: 'startDate', label: 'Từ ngày', type: 'date', required: true, min: todayIso(), colClass: 'col-sm-6' },
    { name: 'endDate', label: 'Đến ngày', type: 'date', required: true, min: todayIso(), colClass: 'col-sm-6', help: 'Chỉ tính ngày làm việc Thứ 2 – Thứ 6.' },
    { name: 'reason', label: 'Lý do', type: 'textarea', required: true, minLength: 3, maxLength: 500 },
    { name: 'contactPhone', label: 'Số điện thoại liên hệ', maxLength: 20, colClass: 'col-sm-6' },
    { name: 'contactAddress', label: 'Địa chỉ liên hệ', maxLength: 200, colClass: 'col-sm-6' }
  ];
  const toBody = (v) => ({ ...v, contactPhone: v.contactPhone || null, contactAddress: v.contactAddress || null });

  async function openCreate() {
    const created = await openFormModal({
      title: 'Tạo đơn nghỉ (lưu nháp)',
      submitText: 'Lưu nháp',
      fields,
      size: 'modal-lg',
      onSubmit: (v) => api('/api/leave-requests', { method: 'POST', body: toBody(v) })
    });
    if (created) {
      toast(`Đã lưu nháp đơn ${created.requestNumber} (${created.totalDays} ngày làm việc). Bấm "Gửi duyệt" để gửi quản lý.`, 'success');
      table.reload();
    }
  }

  async function openEdit(r) {
    const updated = await openFormModal({
      title: `Sửa đơn ${r.requestNumber}`,
      fields,
      size: 'modal-lg',
      initial: { ...r, startDate: String(r.startDate).slice(0, 10), endDate: String(r.endDate).slice(0, 10) },
      onSubmit: (v) => api(`/api/leave-requests/${r.id}`, { method: 'PUT', body: toBody(v) })
    });
    if (updated) { toast('Đã cập nhật đơn nháp.', 'success'); table.reload(); }
  }

  async function submit(r) {
    const ok = await confirmDialog({
      title: 'Gửi duyệt đơn nghỉ',
      message: `Gửi đơn ${r.requestNumber} (${r.totalDays} ngày) cho quản lý duyệt?`,
      detail: 'Hệ thống sẽ kiểm tra trùng ngày và số dư phép; số ngày sẽ được giữ chỗ trong lúc chờ duyệt.',
      confirmText: 'Gửi duyệt',
      danger: false
    });
    if (!ok) return;
    try {
      await api(`/api/leave-requests/${r.id}/submit`, { method: 'POST' });
      toast('Đã gửi duyệt.', 'success');
      table.reload();
    } catch (error) {
      toast(error.message, 'danger');
    }
  }

  container.append(pageHeader('Đơn nghỉ của tôi', 'Tạo đơn (nháp) → Gửi duyệt → Quản lý duyệt/từ chối. Có thể hủy trước ngày nghỉ.'));
  const host = h('div');
  container.append(host);
  const table = createDataTable(host, {
    signal,
    caption: 'Đơn nghỉ của tôi',
    searchPlaceholder: 'Tìm theo mã đơn...',
    filters: [
      { name: 'status', label: 'Trạng thái', options: STATUS_OPTIONS },
      { name: 'leaveTypeId', label: 'Loại nghỉ', options: leaveTypes }
    ],
    toolbarActions: [h('button', { type: 'button', class: 'btn btn-primary', onclick: openCreate }, icon('plus-lg', 'me-1'), 'Tạo đơn nghỉ')],
    emptyMessage: 'Bạn chưa có đơn nghỉ nào.',
    emptyHint: 'Bấm "Tạo đơn nghỉ" để bắt đầu.',
    columns: [
      { key: 'requestNumber', label: 'Mã đơn', className: 'text-nowrap' },
      { key: 'leaveTypeName', label: 'Loại nghỉ' },
      { key: 'range', label: 'Thời gian', render: dateRange },
      { key: 'totalDays', label: 'Số ngày', className: 'text-center' },
      { key: 'status', label: 'Trạng thái', render: (r) => statusBadge(r.status) },
      {
        key: 'actions', label: 'Thao tác', className: 'text-end text-nowrap',
        render: (r) => h('div', { class: 'd-inline-flex gap-1' },
          iconButton('eye', 'Xem chi tiết', () => showRequestDetail(r.id)),
          r.status === 'Draft' ? iconButton('pencil', 'Sửa đơn nháp', () => openEdit(r)) : null,
          r.status === 'Draft' ? iconButton('send', 'Gửi duyệt', () => submit(r), 'outline-primary') : null,
          canCancel(r) ? iconButton('x-circle', 'Hủy đơn', () => cancelRequest(r, table.reload), 'outline-danger') : null)
      }
    ],
    fetchPage: async (params, s) => toTablePage(await api('/api/leave-requests', { query: queryFrom(params, { employeeId: user.employeeId }), signal: s }))
  });
}

// ---------- Duyệt đơn ----------

export async function renderApprovals(container, { signal }) {
  const leaveTypes = await loadLeaveTypes();

  async function decide(r, approve) {
    const result = await openFormModal({
      title: `${approve ? 'Duyệt' : 'Từ chối'} đơn ${r.requestNumber}`,
      submitText: approve ? 'Duyệt đơn' : 'Từ chối',
      fields: [{
        name: 'comment', label: approve ? 'Ghi chú (không bắt buộc)' : 'Lý do từ chối', type: 'textarea',
        required: !approve, minLength: approve ? undefined : 3, maxLength: 500
      }],
      onSubmit: (v) => api(`/api/leave-requests/${r.id}/${approve ? 'approve' : 'reject'}`, { method: 'POST', body: { comment: v.comment || null } })
    });
    if (result) {
      toast(approve ? 'Đã duyệt đơn.' : 'Đã từ chối đơn.', 'success');
      table.reload();
    }
  }

  container.append(pageHeader('Duyệt đơn nghỉ', 'Đơn đang chờ của nhân viên do bạn quản lý trực tiếp (Admin: mọi đơn trừ đơn của chính mình).'));
  const host = h('div');
  container.append(host);
  const table = createDataTable(host, {
    signal,
    caption: 'Đơn chờ duyệt',
    searchPlaceholder: 'Tìm theo mã đơn, mã/tên nhân viên...',
    filters: [{ name: 'leaveTypeId', label: 'Loại nghỉ', options: leaveTypes }],
    emptyMessage: 'Không có đơn nào đang chờ bạn duyệt.',
    columns: [
      { key: 'requestNumber', label: 'Mã đơn', className: 'text-nowrap' },
      { key: 'employee', label: 'Nhân viên', render: employeeCell },
      { key: 'leaveTypeName', label: 'Loại nghỉ' },
      { key: 'range', label: 'Thời gian', render: dateRange },
      { key: 'totalDays', label: 'Số ngày', className: 'text-center' },
      { key: 'reason', label: 'Lý do' },
      {
        key: 'actions', label: 'Thao tác', className: 'text-end text-nowrap',
        render: (r) => h('div', { class: 'd-inline-flex gap-1' },
          iconButton('eye', 'Xem chi tiết', () => showRequestDetail(r.id)),
          iconButton('check-lg', 'Duyệt', () => decide(r, true), 'outline-success'),
          iconButton('x-lg', 'Từ chối', () => decide(r, false), 'outline-danger'))
      }
    ],
    fetchPage: async (params, s) => toTablePage(await api('/api/leave-requests/pending-approval', { query: queryFrom(params), signal: s }))
  });
}

// ---------- Danh sách đơn (Admin/Manager) ----------

export async function renderAllRequests(container, { user, signal }) {
  const leaveTypes = await loadLeaveTypes();
  container.append(pageHeader('Danh sách đơn nghỉ', user.role === 'Admin' ? 'Toàn bộ đơn nghỉ trong công ty.' : 'Đơn của bạn và nhân viên cấp dưới trực tiếp.'));
  const host = h('div');
  container.append(host);
  const table = createDataTable(host, {
    signal,
    caption: 'Danh sách đơn nghỉ',
    searchPlaceholder: 'Tìm theo mã đơn, mã/tên nhân viên...',
    filters: [
      { name: 'status', label: 'Trạng thái', options: STATUS_OPTIONS },
      { name: 'leaveTypeId', label: 'Loại nghỉ', options: leaveTypes }
    ],
    emptyMessage: 'Chưa có đơn nghỉ nào.',
    columns: [
      { key: 'requestNumber', label: 'Mã đơn', className: 'text-nowrap' },
      { key: 'employee', label: 'Nhân viên', render: employeeCell },
      { key: 'leaveTypeName', label: 'Loại nghỉ' },
      { key: 'range', label: 'Thời gian', render: dateRange },
      { key: 'totalDays', label: 'Số ngày', className: 'text-center' },
      { key: 'status', label: 'Trạng thái', render: (r) => statusBadge(r.status) },
      {
        key: 'actions', label: 'Thao tác', className: 'text-end text-nowrap',
        render: (r) => h('div', { class: 'd-inline-flex gap-1' },
          iconButton('eye', 'Xem chi tiết', () => showRequestDetail(r.id)),
          // Chủ đơn hoặc Admin mới được hủy (API kiểm tra lại)
          canCancel(r) && (user.role === 'Admin' || r.employeeId === user.employeeId)
            ? iconButton('x-circle', 'Hủy đơn', () => cancelRequest(r, table.reload), 'outline-danger') : null)
      }
    ],
    fetchPage: async (params, s) => toTablePage(await api('/api/leave-requests', { query: queryFrom(params), signal: s }))
  });
}

// ---------- Lịch sử duyệt ----------

export async function renderHistory(container, { signal }) {
  container.append(pageHeader('Lịch sử duyệt', 'Các lượt duyệt/từ chối liên quan đến bạn.'));
  const host = h('div');
  container.append(host);
  createDataTable(host, {
    signal,
    caption: 'Lịch sử duyệt',
    search: false,
    emptyMessage: 'Chưa có lượt duyệt nào.',
    columns: [
      { key: 'actionDate', label: 'Thời điểm', render: (a) => formatDateTime(a.actionDate), className: 'text-nowrap' },
      { key: 'requestNumber', label: 'Mã đơn', render: (a) => h('button', { type: 'button', class: 'btn btn-link p-0', onclick: () => showRequestDetail(a.leaveRequestId) }, a.requestNumber) },
      { key: 'approverName', label: 'Người duyệt' },
      { key: 'status', label: 'Kết quả', render: (a) => statusBadge(a.status) },
      { key: 'comment', label: 'Ghi chú' }
    ],
    fetchPage: async (params, s) => toTablePage(await api('/api/leave-approvals', { query: { page: params.pageNumber, pageSize: params.pageSize }, signal: s }))
  });
}
