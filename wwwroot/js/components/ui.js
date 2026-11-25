import { h, icon } from '../core/dom.js';

export function pageHeader(title, subtitle, ...actions) {
  return h('div', { class: 'page-header' },
    h('div', {},
      h('h1', {}, title),
      subtitle ? h('p', {}, subtitle) : null),
    actions.length ? h('div', { class: 'd-flex flex-wrap gap-2' }, actions) : null);
}

// Badge luôn có chữ (và icon), không chỉ dựa vào màu sắc
export function badge(text, variant = 'secondary', iconName = null) {
  return h('span', { class: `badge badge-soft-${variant}` }, iconName ? icon(iconName, 'me-1') : null, text);
}

export function activeBadge(isActive, activeText = 'Đang hoạt động', inactiveText = 'Ngừng sử dụng') {
  return isActive ? badge(activeText, 'success', 'check-circle') : badge(inactiveText, 'secondary', 'pause-circle');
}

const STATUS = {
  Draft: ['Nháp', 'secondary', 'pencil'],
  Pending: ['Chờ duyệt', 'warning', 'hourglass-split'],
  Approved: ['Đã duyệt', 'success', 'check-circle'],
  Rejected: ['Từ chối', 'danger', 'x-circle'],
  Cancelled: ['Đã hủy', 'secondary', 'slash-circle'],
  Expired: ['Hết hạn', 'secondary', 'clock-history']
};

export const STATUS_OPTIONS = Object.entries(STATUS).filter(([key]) => key !== 'Expired')
  .map(([value, [label]]) => ({ value, label }));

export function statusBadge(status) {
  const [label, variant, iconName] = STATUS[status] ?? [status, 'secondary', null];
  return badge(label, variant, iconName);
}

export function iconButton(iconName, label, onClick, variant = 'outline-secondary', disabled = false) {
  return h('button', {
    type: 'button',
    class: `btn btn-${variant} btn-icon`,
    title: label,
    'aria-label': label,
    disabled,
    onclick: onClick
  }, icon(iconName));
}

export function kpiCard(label, value, hint, iconName, tone = 'primary') {
  return h('div', { class: 'col-12 col-sm-6 col-xl-3' },
    h('div', { class: 'card kpi-card h-100' },
      h('div', { class: 'card-body d-flex align-items-center gap-3' },
        h('div', { class: `kpi-icon soft-${tone}` }, icon(iconName)),
        h('div', {},
          h('div', { class: 'kpi-label' }, label),
          h('div', { class: 'kpi-value' }, String(value ?? 0)),
          hint ? h('div', { class: 'small text-secondary' }, hint) : null))));
}

/** Hộp thoại chỉ xem (chi tiết đơn...). */
export function showInfoModal(title, body) {
  const modalElement = h('div', { class: 'modal fade', tabindex: '-1', role: 'dialog', 'aria-modal': 'true', 'aria-label': title },
    h('div', { class: 'modal-dialog modal-dialog-centered modal-dialog-scrollable modal-lg' },
      h('div', { class: 'modal-content' },
        h('div', { class: 'modal-header' },
          h('h2', { class: 'modal-title fs-5' }, title),
          h('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Đóng' })),
        h('div', { class: 'modal-body' }, body),
        h('div', { class: 'modal-footer' },
          h('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal' }, 'Đóng')))));
  document.body.append(modalElement);
  const modal = new window.bootstrap.Modal(modalElement);
  modalElement.addEventListener('hidden.bs.modal', () => { modal.dispose(); modalElement.remove(); });
  modal.show();
}
