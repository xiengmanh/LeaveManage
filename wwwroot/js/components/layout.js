import { h, icon } from '../core/dom.js';

// Menu theo role. Giao diện chỉ ẩn/hiện cho tiện; API mới là nơi kiểm tra quyền thật ([Authorize] + Service).
const MENU = [
  { path: '/dashboard', label: 'Tổng quan', icon: 'speedometer2' },
  { path: '/my-requests', label: 'Đơn nghỉ của tôi', icon: 'calendar-plus', needEmployee: true },
  { path: '/approvals', label: 'Duyệt đơn', icon: 'check2-square', roles: ['Admin', 'Manager'] },
  { path: '/requests', label: 'Danh sách đơn nghỉ', icon: 'list-check', roles: ['Admin', 'Manager'] },
  { path: '/history', label: 'Lịch sử duyệt', icon: 'clock-history' },
  { path: '/balances', label: 'Số dư phép', icon: 'piggy-bank' },
  { path: '/employees', label: 'Nhân viên', icon: 'people' },
  { path: '/catalog', label: 'Danh mục', icon: 'collection', roles: ['Admin'] },
  { path: '/users', label: 'Tài khoản', icon: 'person-gear', roles: ['Admin'] }
];

export const ROLE_LABELS = { Admin: 'Quản trị viên', Manager: 'Quản lý', User: 'Nhân viên' };

export function menuFor(user) {
  return MENU.filter((item) => (!item.roles || item.roles.includes(user.role)) && (!item.needEmployee || user.employeeId));
}

let shell = null;

function brand(id) {
  return h('div', { class: 'sidebar-brand', id },
    h('span', { class: 'brand-mark' }, icon('calendar2-check-fill')),
    h('span', {}, 'LeaveManage'));
}

function buildShell(root, user, onLogout) {
  const links = [];
  const nav = h('nav', { class: 'sidebar-nav', 'aria-label': 'Điều hướng chính' },
    h('div', { class: 'sidebar-section' }, 'Quản lý ngày nghỉ'),
    menuFor(user).map((item) => {
      const link = h('a', { href: `#${item.path}`, class: 'sidebar-link', dataset: { path: item.path } }, icon(item.icon), h('span', {}, item.label));
      links.push(link);
      return link;
    }));

  const sidebar = h('aside', { class: 'offcanvas-lg offcanvas-start sidebar', tabindex: '-1', id: 'appSidebar', 'aria-label': 'Menu chính' },
    h('div', { class: 'offcanvas-header sidebar-mobile-header' },
      brand('appSidebarLabel'),
      h('button', { type: 'button', class: 'btn-close btn-close-white', 'data-bs-dismiss': 'offcanvas', 'data-bs-target': '#appSidebar', 'aria-label': 'Đóng menu' })),
    h('div', { class: 'offcanvas-body' },
      h('div', { class: 'sidebar-panel' },
        h('div', { class: 'd-none d-lg-block' }, brand()),
        nav,
        h('div', { class: 'sidebar-footer' },
          h('div', { class: 'small text-uppercase sidebar-muted' }, 'Đăng nhập với vai trò'),
          h('div', { class: 'fw-semibold text-white' }, ROLE_LABELS[user.role] ?? user.role)))));

  nav.addEventListener('click', (event) => {
    if (event.target.closest('a')) window.bootstrap.Offcanvas.getInstance(sidebar)?.hide();
  });

  const title = h('div', { class: 'topbar-title' });
  const userMenu = h('div', { class: 'dropdown ms-auto' },
    h('button', { type: 'button', class: 'btn user-chip dropdown-toggle', 'data-bs-toggle': 'dropdown', 'aria-expanded': 'false' },
      h('span', { class: 'avatar-circle', 'aria-hidden': 'true' }, (user.username[0] ?? 'U').toUpperCase()),
      h('span', { class: 'd-none d-sm-inline text-start lh-sm' },
        h('span', { class: 'd-block fw-semibold' }, user.username),
        h('span', { class: 'd-block small text-secondary' }, ROLE_LABELS[user.role] ?? user.role))),
    h('ul', { class: 'dropdown-menu dropdown-menu-end shadow-sm' },
      h('li', {}, h('span', { class: 'dropdown-item-text small text-secondary' },
        user.employeeId ? `Mã nhân viên liên kết: #${user.employeeId}` : 'Tài khoản chưa gắn nhân viên')),
      h('li', {}, h('hr', { class: 'dropdown-divider' })),
      h('li', {}, h('button', { type: 'button', class: 'dropdown-item text-danger', onclick: onLogout }, icon('box-arrow-right', 'me-2'), 'Đăng xuất'))));

  const topbar = h('header', { class: 'topbar' },
    h('button', {
      type: 'button', class: 'btn btn-light btn-icon d-lg-none', 'data-bs-toggle': 'offcanvas',
      'data-bs-target': '#appSidebar', 'aria-controls': 'appSidebar', 'aria-label': 'Mở menu'
    }, icon('list')),
    title,
    userMenu);

  const content = h('main', { class: 'content', id: 'main-content', tabindex: '-1' });
  const element = h('div', { class: 'app-shell' }, sidebar, h('div', { class: 'app-main' }, topbar, content));
  root.replaceChildren(element);
  return { element, content, title, links, key: `${user.username}|${user.role}` };
}

/** Vẽ khung trang một lần; các lần sau chỉ đổi tiêu đề và mục đang chọn. */
export function renderLayout(root, { user, activePath, title, onLogout }) {
  const key = `${user.username}|${user.role}`;
  if (!shell || shell.key !== key || !root.contains(shell.element)) shell = buildShell(root, user, onLogout);
  shell.title.textContent = title;
  document.title = `${title} | LeaveManage`;
  for (const link of shell.links) {
    const active = link.dataset.path === activePath;
    link.classList.toggle('active', active);
    if (active) link.setAttribute('aria-current', 'page');
    else link.removeAttribute('aria-current');
  }
  shell.content.replaceChildren();
  return shell.content;
}
