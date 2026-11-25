import { api } from './core/api.js';
import { h, icon } from './core/dom.js';
import { addRoute, findRoute, navigate, startRouter } from './core/router.js';
import { clearSession, getUser } from './core/session.js';
import { renderLayout } from './components/layout.js';
import { toast } from './components/toast.js';
import { renderLogin } from './pages/login.js';
import { renderDashboard } from './pages/dashboard.js';
import { renderMyRequests, renderApprovals, renderAllRequests, renderHistory } from './pages/requests.js';
import { renderBalances } from './pages/balances.js';
import { renderEmployees } from './pages/employees.js';
import { renderCatalog } from './pages/catalog.js';
import { renderUsers } from './pages/users.js';

const app = document.getElementById('app');

// Bảng định tuyến: route → hàm vẽ trang + role được xem (kiểm tra ở giao diện, API vẫn kiểm tra lại).
addRoute('/dashboard', renderDashboard, { title: 'Tổng quan' });
addRoute('/my-requests', renderMyRequests, { title: 'Đơn nghỉ của tôi', needEmployee: true });
addRoute('/approvals', renderApprovals, { roles: ['Admin', 'Manager'], title: 'Duyệt đơn nghỉ' });
addRoute('/requests', renderAllRequests, { roles: ['Admin', 'Manager'], title: 'Danh sách đơn nghỉ' });
addRoute('/history', renderHistory, { title: 'Lịch sử duyệt' });
addRoute('/balances', renderBalances, { title: 'Số dư phép' });
addRoute('/employees', renderEmployees, { title: 'Nhân viên' });
addRoute('/catalog', renderCatalog, { roles: ['Admin'], title: 'Danh mục' });
addRoute('/users', renderUsers, { roles: ['Admin'], title: 'Tài khoản' });

let activeController = null;

async function handleRoute({ path, query }) {
  activeController?.abort();
  activeController = new AbortController();
  const user = getUser();

  if (path === '/login') {
    if (user) return navigate('/dashboard');
    renderLogin(app, { onSuccess: () => navigate('/dashboard') });
    return;
  }
  if (!user) return navigate('/login');
  if (path === '/' || path === '') return navigate('/dashboard');

  const route = findRoute(path);
  const content = renderLayout(app, { user, activePath: path, title: route ? route.title : 'Không tìm thấy trang', onLogout: logout });

  if (!route) {
    content.append(messagePage('signpost-split', 'Không tìm thấy trang', 'Đường dẫn bạn truy cập không tồn tại.'));
    return;
  }
  if ((route.roles && !route.roles.includes(user.role)) || (route.needEmployee && !user.employeeId)) {
    content.append(messagePage('shield-lock', 'Bạn không có quyền truy cập', 'Trang này không dành cho tài khoản của bạn.'));
    return;
  }

  try {
    await route.handler(content, { user, query, signal: activeController.signal });
  } catch (error) {
    // 401 (phiên bị thu hồi/hết hạn) đã được xử lý qua sự kiện auth:expired → về trang đăng nhập
    if (error.name !== 'AbortError' && error.status !== 401) {
      console.error(error);
      toast('Không thể hiển thị trang. Vui lòng thử lại.', 'danger');
    }
  }
}

function messagePage(iconName, title, text) {
  return h('div', { class: 'card' },
    h('div', { class: 'card-body text-center py-5' },
      h('div', { class: 'display-5 text-secondary mb-3' }, icon(iconName)),
      h('h1', { class: 'h4 fw-bold' }, title),
      h('p', { class: 'text-secondary' }, text),
      h('a', { class: 'btn btn-primary', href: '#/dashboard' }, icon('house', 'me-1'), 'Về trang tổng quan')));
}

async function logout() {
  // Thu hồi refresh token trên server rồi mới xóa phiên ở trình duyệt
  try { await api('/api/users/logout', { method: 'POST' }); } catch { /* vẫn đăng xuất phía client */ }
  clearSession();
  toast('Bạn đã đăng xuất.', 'info');
  navigate('/login');
}

window.addEventListener('auth:expired', () => {
  toast('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', 'warning');
  navigate('/login');
});

startRouter(handleRoute);
