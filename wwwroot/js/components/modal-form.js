import { ApiError } from '../core/api.js';
import { h, icon } from '../core/dom.js';

let counter = 0;

/**
 * Form trong hộp thoại (Bootstrap Modal) có kiểm tra dữ liệu 2 lớp:
 *  1) Phía trình duyệt (required, min, max, pattern...) để báo lỗi sớm.
 *  2) Phía server: lỗi 400/409 có "errors" được gắn ngay dưới từng ô nhập.
 *
 * fields: [{ name, label, type, required, options, min, max, minLength, maxLength, pattern,
 *            patternMessage, placeholder, help, colClass, valueType }]
 * onSubmit(values) trả Promise; nếu ném ApiError thì form giữ nguyên dữ liệu và hiển thị lỗi.
 */
export function openFormModal({ title, fields, initial = {}, submitText = 'Lưu', onSubmit, size = '' }) {
  return new Promise((resolve) => {
    const id = ++counter;
    const formId = `modal-form-${id}`;
    const titleId = `modal-title-${id}`;
    const inputs = {};
    const feedbacks = {};
    let finished = false;
    let result = null;

    const alertBox = h('div', { class: 'alert alert-danger d-none', role: 'alert' });
    const form = h('form', { class: 'row g-3', id: formId, novalidate: true });

    for (const field of fields) {
      const inputId = `${formId}-${field.name}`;
      const errorId = `${inputId}-error`;
      const input = createInput(field, inputId, errorId, initial);
      const feedback = h('div', { class: 'invalid-feedback', id: errorId });
      inputs[field.name] = input;
      feedbacks[field.name] = feedback;

      if (field.type === 'switch') {
        form.append(h('div', { class: field.colClass || 'col-12' },
          h('div', { class: 'form-check form-switch' },
            input,
            h('label', { class: 'form-check-label', for: inputId }, field.label),
            feedback)));
      } else {
        form.append(h('div', { class: field.colClass || 'col-12' },
          h('label', { class: 'form-label', for: inputId },
            field.label,
            field.required ? h('span', { class: 'text-danger ms-1', title: 'Bắt buộc' }, '*') : null),
          input,
          field.help ? h('div', { class: 'form-text' }, field.help) : null,
          feedback));
      }
    }

    const submitButton = h('button', { type: 'submit', class: 'btn btn-primary', form: formId }, submitText);

    const modalElement = h('div', { class: 'modal fade', tabindex: '-1', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': titleId },
      h('div', { class: `modal-dialog modal-dialog-centered modal-dialog-scrollable ${size}` },
        h('div', { class: 'modal-content' },
          h('div', { class: 'modal-header' },
            h('h2', { class: 'modal-title fs-5', id: titleId }, title),
            h('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Đóng' })),
          h('div', { class: 'modal-body' },
            h('p', { class: 'small text-secondary mb-3' }, 'Các trường có dấu ', h('span', { class: 'text-danger' }, '*'), ' là bắt buộc.'),
            alertBox,
            form),
          h('div', { class: 'modal-footer' },
            h('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal' }, 'Hủy'),
            submitButton))));

    // Nhớ phần tử đang có focus (nút đã mở form) để trả focus về đó khi đóng form (hỗ trợ bàn phím)
    const opener = document.activeElement;
    document.body.append(modalElement);
    // backdrop 'static': bấm ra ngoài không đóng form để tránh mất dữ liệu đang nhập
    const modal = new window.bootstrap.Modal(modalElement, { backdrop: 'static' });

    function clearErrors() {
      alertBox.classList.add('d-none');
      alertBox.replaceChildren();
      for (const name of Object.keys(inputs)) {
        inputs[name].classList.remove('is-invalid');
        inputs[name].removeAttribute('aria-invalid');
        feedbacks[name].textContent = '';
      }
    }

    function showFieldError(name, message) {
      if (!inputs[name]) return false;
      inputs[name].classList.add('is-invalid');
      inputs[name].setAttribute('aria-invalid', 'true');
      feedbacks[name].textContent = message;
      return true;
    }

    function showAlert(message, traceId) {
      // Không truyền null vào replaceChildren (DOM sẽ in ra chữ "null")
      alertBox.replaceChildren(h('div', { class: 'd-flex gap-2' }, icon('exclamation-octagon'), h('span', {}, message)));
      if (traceId) alertBox.append(h('div', { class: 'small opacity-75 mt-1' }, `Mã truy vết: ${traceId}`));
      alertBox.classList.remove('d-none');
    }

    function setBusy(busy) {
      submitButton.disabled = busy;
      if (busy) {
        submitButton.replaceChildren(h('span', { class: 'spinner-border spinner-border-sm me-2', 'aria-hidden': 'true' }), 'Đang lưu...');
      } else {
        submitButton.replaceChildren(submitText);
      }
    }

    function readValues() {
      const values = {};
      for (const field of fields) {
        const input = inputs[field.name];
        if (field.type === 'switch') values[field.name] = input.checked;
        else if (field.type === 'number' || field.valueType === 'number') values[field.name] = input.value === '' ? null : Number(input.value);
        else if (field.type === 'date') values[field.name] = input.value || null;
        else values[field.name] = input.value;
      }
      return values;
    }

    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      clearErrors();

      // 1) Kiểm tra phía trình duyệt
      if (!form.checkValidity()) {
        for (const field of fields) {
          const input = inputs[field.name];
          if (!input.checkValidity()) showFieldError(field.name, clientMessage(input, field));
        }
        form.querySelector(':invalid')?.focus();
        return;
      }

      // 2) Gửi lên server
      setBusy(true);
      try {
        result = await onSubmit(readValues());
        finished = true;
        modal.hide();
      } catch (error) {
        if (error instanceof ApiError && error.errors) {
          const unmatched = [];
          for (const [rawName, messages] of Object.entries(error.errors)) {
            // ASP.NET trả key dạng "Reason" hoặc "$.reason" → đổi về tên field camelCase
            const cleaned = rawName.replace(/^\$\./, '');
            const name = cleaned.charAt(0).toLowerCase() + cleaned.slice(1);
            if (!showFieldError(name, messages.join(' '))) unmatched.push(...messages);
          }
          showAlert([error.message, ...unmatched].join(' '), error.traceId);
          form.querySelector('.is-invalid')?.focus();
        } else {
          showAlert(error?.message || 'Không thể lưu dữ liệu.', error?.traceId);
        }
      } finally {
        setBusy(false);
      }
    });

    modalElement.addEventListener('shown.bs.modal', () => {
      form.querySelector('input:not([type=checkbox]):not([disabled]), select, textarea')?.focus();
    });
    modalElement.addEventListener('hidden.bs.modal', () => {
      modal.dispose();
      modalElement.remove();
      restoreFocus(opener);
      resolve(finished ? result ?? true : null);
    });
    modal.show();
  });
}

// Trả focus về nút đã mở hộp thoại; nếu nút đó đã bị vẽ lại (bảng tải lại) thì về vùng nội dung chính
export function restoreFocus(opener) {
  const target = opener instanceof HTMLElement && document.contains(opener) ? opener : document.getElementById('main-content');
  target?.focus();
}

function createInput(field, inputId, errorId, initial) {
  const common = { id: inputId, name: field.name, 'aria-describedby': errorId, required: Boolean(field.required) };
  const initialValue = initial[field.name] ?? field.defaultValue;
  let input;

  if (field.type === 'select') {
    input = h('select', { ...common, class: 'form-select' },
      h('option', { value: '' }, field.placeholder || '-- Chọn --'),
      (field.options || []).map((option) => h('option', { value: String(option.value), disabled: option.disabled }, option.label)));
    input.value = initialValue != null ? String(initialValue) : '';
  } else if (field.type === 'textarea') {
    input = h('textarea', { ...common, class: 'form-control', rows: 3, maxlength: field.maxLength, placeholder: field.placeholder });
    input.value = initialValue ?? '';
  } else if (field.type === 'switch') {
    input = h('input', { id: inputId, name: field.name, 'aria-describedby': errorId, class: 'form-check-input', type: 'checkbox', role: 'switch' });
    input.checked = Boolean(initialValue);
  } else {
    input = h('input', {
      ...common,
      class: 'form-control',
      type: field.type || 'text',
      min: field.min,
      max: field.max,
      minlength: field.minLength,
      maxlength: field.maxLength,
      pattern: field.pattern,
      placeholder: field.placeholder,
      autocomplete: field.autocomplete || 'off'
    });
    input.value = initialValue ?? '';
  }
  return input;
}

function clientMessage(input, field) {
  const label = field.label.toLowerCase();
  const validity = input.validity;
  if (validity.valueMissing) return field.type === 'select' ? `Vui lòng chọn ${label}.` : `Vui lòng nhập ${label}.`;
  if (validity.typeMismatch && field.type === 'email') return 'Email không đúng định dạng.';
  if (validity.patternMismatch) return field.patternMessage || 'Giá trị không đúng định dạng.';
  if (validity.rangeUnderflow || validity.rangeOverflow) {
    if (field.type === 'date') return field.rangeMessage || 'Ngày không hợp lệ.';
    return `Giá trị phải từ ${field.min} đến ${field.max}.`;
  }
  if (validity.tooShort) return `Tối thiểu ${field.minLength} ký tự.`;
  if (validity.tooLong) return `Tối đa ${field.maxLength} ký tự.`;
  if (validity.badInput) return 'Giá trị không hợp lệ.';
  return input.validationMessage || 'Giá trị không hợp lệ.';
}
