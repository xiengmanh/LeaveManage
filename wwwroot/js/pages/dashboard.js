import { api } from '../core/api.js';
import { h } from '../core/dom.js';
import { errorState, loadingBlock } from '../components/states.js';
import { kpiCard, pageHeader } from '../components/ui.js';

export async function renderDashboard(container, { user, signal }) {
  const body = h('div');
  container.append(pageHeader('Tổng quan', 'Số liệu tính trực tiếp từ cơ sở dữ liệu, theo phạm vi quyền của bạn.'), body);

  async function load() {
    body.replaceChildren(loadingBlock('Đang tải số liệu...'));
    try {
      const d = await api('/api/dashboard', { signal });
      const canApprove = user.role === 'Admin' || user.role === 'Manager';
      body.replaceChildren(
        h('p', { class: 'text-secondary' }, `Phạm vi dữ liệu: ${d.scope}`),
        h('div', { class: 'row g-3 mb-4' },
          kpiCard('Nhân viên', d.employeeCount, 'Trong phạm vi, đang làm việc', 'people-fill', 'primary'),
          kpiCard('Đơn chờ duyệt', d.pendingCount, canApprove ? `${d.awaitingMyApproval} đơn chờ bạn duyệt` : 'Đang chờ quản lý', 'hourglass-split', 'warning'),
          kpiCard('Đơn đã duyệt', d.approvedCount, `${d.rejectedCount} từ chối · ${d.cancelledCount} đã hủy`, 'check-circle-fill', 'success'),
          kpiCard('Đang nghỉ hôm nay', d.onLeaveToday, `${d.draftCount} đơn nháp`, 'calendar-event', 'info')),
        h('div', { class: 'card' },
          h('div', { class: 'card-header bg-white py-3 d-flex justify-content-between align-items-center' },
            h('h2', { class: 'h6 mb-0 fw-semibold' }, `Số dư phép của tôi năm ${new Date().getFullYear()}`),
            h('a', { href: '#/balances', class: 'small' }, 'Xem chi tiết')),
          h('div', { class: 'card-body' },
            d.myBalances.length === 0
              ? h('p', { class: 'text-secondary mb-0' }, user.employeeId ? 'Chưa được cấp số dư phép cho năm nay.' : 'Tài khoản chưa gắn với nhân viên.')
              : h('div', { class: 'row g-3' }, d.myBalances.map((b) => {
                const percent = b.initialDays > 0 ? Math.round(((b.usedDays + b.pendingDays) / b.initialDays) * 100) : 0;
                const bar = h('div', { class: `progress-bar ${percent >= 90 ? 'bg-danger' : 'bg-primary'}` });
                bar.style.width = `${Math.min(100, percent)}%`;
                return h('div', { class: 'col-12 col-md-6' },
                  h('div', { class: 'border rounded p-3 h-100' },
                    h('div', { class: 'd-flex justify-content-between mb-2' },
                      h('span', { class: 'fw-semibold' }, b.leaveTypeName),
                      h('span', {}, `Còn ${b.remainingDays}/${b.initialDays} ngày`)),
                    h('div', { class: 'progress mb-2', role: 'progressbar', 'aria-label': `Đã dùng và đang chờ ${percent}%`, 'aria-valuenow': percent, 'aria-valuemin': 0, 'aria-valuemax': 100 }, bar),
                    h('div', { class: 'small text-secondary' }, `Đã dùng ${b.usedDays} · Đang chờ duyệt ${b.pendingDays}`)));
              })))));
    } catch (error) {
      if (error.name === 'AbortError') return;
      body.replaceChildren(errorState(error, load));
    }
  }
  await load();
}
