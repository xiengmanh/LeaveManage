import { api } from '../core/api.js';
import { h, icon } from '../core/dom.js';
import { navigate } from '../core/router.js';
import { clearSession } from '../core/session.js';
import { ROLE_LABELS } from '../components/layout.js';
import { toast } from '../components/toast.js';
import { pageHeader } from '../components/ui.js';

// Trang "Tài khoản của tôi": mọi vai trò đều tự đổi số điện thoại và mật khẩu của chính mình.
export async function renderProfile(container) {
  const profile = await api('/api/users/me/profile');
  container.append(pageHeader('Tài khoản của tôi', 'Xem thông tin và tự đổi số điện thoại, mật khẩu.'));

  const info = (label, value) => h('div', { class: 'col-sm-6' },
    h('div', { class: 'small text-secondary' }, label),
    h('div', { class: 'fw-semibold' }, value || '—'));

  const infoCard = h('div', { class: 'card mb-3' },
    h('div', { class: 'card-body' },
      h('h2', { class: 'h5 mb-3' }, icon('person-circle', 'me-2'), 'Thông tin tài khoản'),
      h('div', { class: 'row g-3' },
        info('Tên đăng nhập', profile.username),
        info('Vai trò', ROLE_LABELS[profile.role] ?? profile.role),
        info('Họ tên', profile.fullName),
        info('Mã nhân viên', profile.employeeCode),
        info('Email', profile.email))));

  function inputField(id, label, attrs = {}, hint) {
    const input = h('input', { class: 'form-control', id, name: id, ...attrs });
    const feedback = h('div', { class: 'invalid-feedback' });
    return {
      input, feedback,
      node: h('div', {}, h('label', { class: 'form-label fw-semibold', for: id }, label), input, hint ? h('div', { class: 'form-text' }, hint) : null, feedback)
    };
  }
  function setInvalid(field, message) { field.input.classList.add('is-invalid'); field.feedback.textContent = message; }
  function clearInvalid(fields) { fields.forEach((f) => { f.input.classList.remove('is-invalid'); f.feedback.textContent = ''; }); }
  function alertBox() { return h('div', { class: 'alert alert-danger d-none mb-0', role: 'alert' }); }
  function showError(box, message) {
    box.replaceChildren(icon('exclamation-octagon', 'me-2'), message);
    box.classList.remove('d-none');
  }
  async function submitting(button, label, work) {
    button.disabled = true;
    button.replaceChildren(h('span', { class: 'spinner-border spinner-border-sm me-2', 'aria-hidden': 'true' }), 'Đang lưu...');
    try { await work(); } finally { button.disabled = false; button.replaceChildren(label); }
  }

  // ---------- Số điện thoại ----------
  const phone = inputField('profile-phone', 'Số điện thoại', { type: 'tel', maxlength: 20, autocomplete: 'tel', placeholder: 'vd: 0901234567', value: profile.phoneNumber ?? '' },
    profile.employeeId ? 'Để trống nếu muốn xóa số điện thoại.' : null);
  const phoneError = alertBox();
  const phoneButton = h('button', { class: 'btn btn-primary', type: 'submit' }, 'Lưu số điện thoại');
  const phoneForm = h('form', { class: 'vstack gap-3', novalidate: true }, phone.node, phoneError, h('div', {}, phoneButton));
  if (!profile.employeeId) {
    phone.input.disabled = true;
    phoneButton.disabled = true;
    phoneForm.prepend(h('div', { class: 'alert alert-warning mb-0' }, 'Tài khoản chưa gắn với nhân viên nên chưa có số điện thoại để cập nhật.'));
  }
  phoneForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearInvalid([phone]);
    phoneError.classList.add('d-none');
    const value = phone.input.value.replace(/[\s.-]/g, '');
    if (value && !/^(\+84|0)[0-9]{9,10}$/.test(value)) {
      setInvalid(phone, 'Số điện thoại không hợp lệ (vd: 0901234567 hoặc +84901234567).');
      phone.input.focus();
      return;
    }
    await submitting(phoneButton, 'Lưu số điện thoại', async () => {
      try {
        const res = await api('/api/users/me/phone', { method: 'PUT', body: { phoneNumber: value } });
        toast(res?.message || 'Đã cập nhật số điện thoại.', 'success');
      } catch (error) {
        showError(phoneError, error.message);
      }
    });
  });

  // ---------- Mật khẩu ----------
  const current = inputField('profile-current', 'Mật khẩu hiện tại', { type: 'password', maxlength: 100, autocomplete: 'current-password' });
  const next = inputField('profile-new', 'Mật khẩu mới', { type: 'password', maxlength: 100, autocomplete: 'new-password' }, 'Tối thiểu 8 ký tự, khác mật khẩu hiện tại.');
  const confirm = inputField('profile-confirm', 'Xác nhận mật khẩu mới', { type: 'password', maxlength: 100, autocomplete: 'new-password' });
  const passwordError = alertBox();
  const passwordButton = h('button', { class: 'btn btn-primary', type: 'submit' }, 'Đổi mật khẩu');
  const passwordForm = h('form', { class: 'vstack gap-3', novalidate: true }, current.node, next.node, confirm.node, passwordError, h('div', {}, passwordButton));
  const passwordFields = [current, next, confirm];

  passwordForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearInvalid(passwordFields);
    passwordError.classList.add('d-none');
    if (!current.input.value) setInvalid(current, 'Vui lòng nhập mật khẩu hiện tại.');
    if (next.input.value.length < 8) setInvalid(next, 'Mật khẩu mới tối thiểu 8 ký tự.');
    else if (next.input.value === current.input.value) setInvalid(next, 'Mật khẩu mới phải khác mật khẩu hiện tại.');
    if (!confirm.input.value) setInvalid(confirm, 'Vui lòng xác nhận mật khẩu mới.');
    else if (confirm.input.value !== next.input.value) setInvalid(confirm, 'Mật khẩu xác nhận không khớp.');
    const bad = passwordFields.find((f) => f.input.classList.contains('is-invalid'));
    if (bad) { bad.input.focus(); return; }

    await submitting(passwordButton, 'Đổi mật khẩu', async () => {
      try {
        await api('/api/users/me/password', {
          method: 'PUT',
          body: { currentPassword: current.input.value, newPassword: next.input.value, confirmPassword: confirm.input.value }
        });
        // Server thu hồi mọi phiên khi đổi mật khẩu → xóa phiên ở trình duyệt và đăng nhập lại
        clearSession();
        toast('Đổi mật khẩu thành công. Vui lòng đăng nhập lại bằng mật khẩu mới.', 'success');
        navigate('/login');
      } catch (error) {
        current.input.value = '';
        const details = error.errors ? Object.values(error.errors).flat().join(' ') : '';
        showError(passwordError, details || error.message);
      }
    });
  });

  container.append(infoCard, h('div', { class: 'row g-3' },
    h('div', { class: 'col-lg-6' }, h('div', { class: 'card h-100' }, h('div', { class: 'card-body' },
      h('h2', { class: 'h5 mb-3' }, icon('telephone', 'me-2'), 'Đổi số điện thoại'), phoneForm))),
    h('div', { class: 'col-lg-6' }, h('div', { class: 'card h-100' }, h('div', { class: 'card-body' },
      h('h2', { class: 'h5 mb-3' }, icon('key', 'me-2'), 'Đổi mật khẩu'), passwordForm)))));
}
