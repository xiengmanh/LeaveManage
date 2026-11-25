import { api } from '../core/api.js';
import { h, icon } from '../core/dom.js';
import { saveSession } from '../core/session.js';
import { toast } from '../components/toast.js';

export function renderLogin(root, { onSuccess }) {
  const username = h('input', {
    class: 'form-control form-control-lg', id: 'login-username', name: 'username',
    autocomplete: 'username', required: true, maxlength: 50, placeholder: 'vd: admin, manager, nv003'
  });
  const password = h('input', {
    class: 'form-control form-control-lg', id: 'login-password', name: 'password', type: 'password',
    autocomplete: 'current-password', required: true, maxlength: 100, placeholder: 'Mật khẩu'
  });
  const togglePassword = h('button', { type: 'button', class: 'btn btn-outline-secondary', 'aria-label': 'Hiện mật khẩu' }, icon('eye'));
  togglePassword.addEventListener('click', () => {
    const show = password.type === 'password';
    password.type = show ? 'text' : 'password';
    togglePassword.setAttribute('aria-label', show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
    togglePassword.replaceChildren(icon(show ? 'eye-slash' : 'eye'));
  });

  const errorBox = h('div', { class: 'alert alert-danger d-none mb-0', role: 'alert' });
  const submit = h('button', { class: 'btn btn-primary btn-lg w-100', type: 'submit' }, 'Đăng nhập');

  function showError(message) {
    errorBox.replaceChildren(icon('exclamation-octagon', 'me-2'), message);
    errorBox.classList.remove('d-none');
  }

  const form = h('form', { class: 'vstack gap-3', novalidate: true },
    h('div', {}, h('label', { class: 'form-label fw-semibold', for: 'login-username' }, 'Tên đăng nhập'), username),
    h('div', {}, h('label', { class: 'form-label fw-semibold', for: 'login-password' }, 'Mật khẩu'),
      h('div', { class: 'input-group' }, password, togglePassword)),
    errorBox,
    submit);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    errorBox.classList.add('d-none');
    if (!username.value.trim() || !password.value) {
      showError('Vui lòng nhập tên đăng nhập và mật khẩu.');
      (username.value.trim() ? password : username).focus();
      return;
    }
    submit.disabled = true;
    submit.replaceChildren(h('span', { class: 'spinner-border spinner-border-sm me-2', 'aria-hidden': 'true' }), 'Đang đăng nhập...');
    try {
      const response = await api('/api/users/login', { method: 'POST', body: { username: username.value.trim(), password: password.value } });
      saveSession(response);
      toast(`Xin chào, ${response.username}!`, 'success');
      onSuccess();
    } catch (error) {
      showError(error.message);
      password.value = '';
      password.focus();
    } finally {
      submit.disabled = false;
      submit.replaceChildren('Đăng nhập');
    }
  });

  const hero = h('section', { class: 'login-hero' },
    h('div', { class: 'd-flex align-items-center gap-2 fs-4 fw-bold' }, h('span', { class: 'brand-mark' }, icon('calendar2-check-fill')), 'LeaveManage'),
    h('div', {},
      h('h1', { class: 'display-6 fw-bold mb-3' }, 'Quản lý ngày nghỉ của nhân sự'),
      h('p', { class: 'lead opacity-75 mb-4' }, 'Tạo đơn, gửi duyệt và theo dõi số dư phép trên một hệ thống.'),
      h('ul', { class: 'login-features' },
        h('li', {}, icon('calendar-plus'), 'Nhân viên tạo đơn nghỉ, theo dõi trạng thái và số dư phép'),
        h('li', {}, icon('check2-square'), 'Quản lý trực tiếp duyệt hoặc từ chối, lưu lịch sử duyệt'),
        h('li', {}, icon('shield-lock'), 'JWT + Refresh Token, phân quyền Admin / Manager / User'))),
    h('p', { class: 'small opacity-50 mb-0' }, 'Đồ án môn Lập trình .NET Core · Nhóm 12'));

  const panel = h('section', { class: 'login-panel' },
    h('div', { class: 'login-card card' },
      h('div', { class: 'card-body p-4 p-sm-5' },
        h('div', { class: 'd-lg-none d-flex align-items-center gap-2 fs-5 fw-bold mb-4' }, h('span', { class: 'brand-mark' }, icon('calendar2-check-fill')), 'LeaveManage'),
        h('h2', { class: 'h3 fw-bold mb-1' }, 'Đăng nhập'),
        h('p', { class: 'text-secondary mb-4' }, 'Sử dụng tài khoản do quản trị viên cấp.'),
        form)));

  root.replaceChildren(h('div', { class: 'login-page' }, hero, panel));
  document.title = 'Đăng nhập | LeaveManage';
  username.focus();
}
