import { h, icon } from '../core/dom.js';
import { restoreFocus } from './modal-form.js';

let counter = 0;

// Hộp thoại xác nhận trước thao tác quan trọng (xóa, hủy). Mặc định focus vào nút "Hủy bỏ".
export function confirmDialog({ title, message, detail, confirmText = 'Xác nhận', danger = true }) {
  return new Promise((resolve) => {
    const titleId = `confirm-title-${++counter}`;
    let confirmed = false;

    const cancelButton = h('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal' }, 'Hủy bỏ');
    const confirmButton = h('button', { type: 'button', class: `btn ${danger ? 'btn-danger' : 'btn-primary'}` }, confirmText);

    const modalElement = h('div', { class: 'modal fade', tabindex: '-1', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': titleId },
      h('div', { class: 'modal-dialog modal-dialog-centered' },
        h('div', { class: 'modal-content' },
          h('div', { class: 'modal-header border-0 pb-0' },
            h('h2', { class: 'modal-title fs-5 d-flex align-items-center gap-2', id: titleId },
              icon(danger ? 'exclamation-triangle text-danger' : 'question-circle text-primary'), title),
            h('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Đóng' })),
          h('div', { class: 'modal-body' },
            h('p', { class: 'mb-1 fw-semibold' }, message),
            detail ? h('p', { class: 'mb-0 text-secondary small' }, detail) : null),
          h('div', { class: 'modal-footer border-0 pt-0' }, cancelButton, confirmButton))));

    const opener = document.activeElement;
    document.body.append(modalElement);
    const modal = new window.bootstrap.Modal(modalElement);

    confirmButton.addEventListener('click', () => {
      confirmed = true;
      modal.hide();
    });
    modalElement.addEventListener('shown.bs.modal', () => cancelButton.focus());
    modalElement.addEventListener('hidden.bs.modal', () => {
      modal.dispose();
      modalElement.remove();
      restoreFocus(opener);
      resolve(confirmed);
    });
    modal.show();
  });
}
