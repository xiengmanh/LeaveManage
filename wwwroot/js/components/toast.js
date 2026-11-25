import { h, icon } from '../core/dom.js';

const VARIANTS = {
  success: { className: 'text-bg-success', iconName: 'check-circle-fill' },
  danger: { className: 'text-bg-danger', iconName: 'exclamation-octagon-fill' },
  warning: { className: 'text-bg-warning', iconName: 'exclamation-triangle-fill' },
  info: { className: 'text-bg-primary', iconName: 'info-circle-fill' }
};

// Thông báo ngắn ở góc màn hình (Bootstrap Toast)
export function toast(message, type = 'success') {
  const variant = VARIANTS[type] ?? VARIANTS.info;
  const isError = type === 'danger';
  const element = h('div', {
    class: `toast align-items-center border-0 ${variant.className}`,
    role: isError ? 'alert' : 'status',
    'aria-live': isError ? 'assertive' : 'polite',
    'aria-atomic': 'true'
  },
  h('div', { class: 'd-flex' },
    h('div', { class: 'toast-body d-flex align-items-start gap-2' }, icon(variant.iconName, 'mt-1'), h('span', {}, message)),
    h('button', {
      type: 'button',
      class: `btn-close ${type === 'warning' ? '' : 'btn-close-white'} me-2 m-auto`,
      'data-bs-dismiss': 'toast',
      'aria-label': 'Đóng'
    })));

  document.getElementById('toast-container').append(element);
  element.addEventListener('hidden.bs.toast', () => element.remove());
  new window.bootstrap.Toast(element, { delay: isError ? 6000 : 3500 }).show();
}
