import { h, icon } from '../core/dom.js';

// Các trạng thái hiển thị chuẩn: đang tải, rỗng, lỗi.

export function loadingRows(columnCount, rowCount = 5) {
  return Array.from({ length: rowCount }, () =>
    h('tr', { class: 'skeleton-row' },
      Array.from({ length: columnCount }, () => h('td', {}, h('span', { class: 'skeleton' })))));
}

export function loadingBlock(text = 'Đang tải dữ liệu...') {
  return h('div', { class: 'loading-block', role: 'status' },
    h('div', { class: 'spinner-border spinner-border-sm text-primary', 'aria-hidden': 'true' }),
    h('span', {}, text));
}

export function emptyState({ iconName = 'inbox', title, text, action }) {
  return h('div', { class: 'empty-state' },
    icon(iconName),
    h('p', { class: 'empty-title' }, title),
    text ? h('p', { class: 'small mb-3' }, text) : null,
    action ? h('button', { type: 'button', class: 'btn btn-outline-primary btn-sm', onclick: action.onClick }, action.label) : null);
}

export function emptyRow(columnCount, state) {
  return h('tr', {}, h('td', { colspan: columnCount }, emptyState(state)));
}

export function errorState(error, onRetry) {
  return h('div', { class: 'alert alert-danger d-flex flex-wrap align-items-center gap-2 mb-0', role: 'alert' },
    icon('exclamation-octagon'),
    h('div', { class: 'me-auto' },
      h('div', {}, error?.message || 'Không thể tải dữ liệu.'),
      error?.traceId ? h('div', { class: 'small opacity-75' }, `Mã truy vết: ${error.traceId}`) : null),
    onRetry ? h('button', { type: 'button', class: 'btn btn-sm btn-outline-danger', onclick: onRetry }, icon('arrow-clockwise'), ' Thử lại') : null);
}

export function errorRow(columnCount, error, onRetry) {
  return h('tr', {}, h('td', { colspan: columnCount, class: 'p-3' }, errorState(error, onRetry)));
}
