import { api, singlePage } from '../core/api.js';
import { h, icon } from '../core/dom.js';
import { confirmDialog } from '../components/confirm.js';
import { createDataTable } from '../components/data-table.js';
import { openFormModal } from '../components/modal-form.js';
import { toast } from '../components/toast.js';
import { activeBadge, iconButton, pageHeader } from '../components/ui.js';

// Ba danh mục dùng chung một cấu hình: đường dẫn API, các cột và các ô nhập
const CATALOGS = [
  {
    key: 'departments', label: 'Phòng ban', url: '/api/departments',
    columns: [{ key: 'description', label: 'Mô tả' }],
    fields: [{ name: 'description', label: 'Mô tả', type: 'textarea', maxLength: 255 }]
  },
  {
    key: 'positions', label: 'Chức vụ', url: '/api/positions',
    columns: [{ key: 'level', label: 'Cấp bậc', className: 'text-center' }],
    fields: [{ name: 'level', label: 'Cấp bậc (1–10)', type: 'number', required: true, min: 1, max: 10 }]
  },
  {
    key: 'leave-types', label: 'Loại nghỉ', url: '/api/leave-types',
    columns: [
      { key: 'defaultDaysPerYear', label: 'Tối đa/năm', className: 'text-center', render: (t) => t.defaultDaysPerYear ?? 'Không giới hạn' },
      { key: 'noticeDaysRequired', label: 'Báo trước (ngày)', className: 'text-center', render: (t) => t.noticeDaysRequired ?? 0 },
      { key: 'allowsDeduction', label: 'Trừ số dư', render: (t) => (t.allowsDeduction ? 'Có' : 'Không') }
    ],
    fields: [
      { name: 'description', label: 'Mô tả', type: 'textarea', maxLength: 255 },
      { name: 'defaultDaysPerYear', label: 'Số ngày tối đa/năm (để trống = không giới hạn)', type: 'number', min: 1, max: 366, colClass: 'col-sm-6' },
      { name: 'noticeDaysRequired', label: 'Số ngày phải báo trước', type: 'number', min: 0, max: 365, colClass: 'col-sm-6' },
      { name: 'allowsDeduction', label: 'Trừ vào số dư phép (không đổi được sau khi tạo)', type: 'switch' }
    ]
  }
];

export async function renderCatalog(container, { query, signal }) {
  const activeKey = CATALOGS.some((c) => c.key === query.get('tab')) ? query.get('tab') : 'departments';
  const catalog = CATALOGS.find((c) => c.key === activeKey);

  container.append(
    pageHeader('Danh mục', '"Xóa" = ngừng sử dụng, dữ liệu cũ vẫn được giữ.'),
    h('ul', { class: 'nav nav-pills mb-3' }, CATALOGS.map((c) =>
      h('li', { class: 'nav-item' }, h('a', { class: `nav-link${c.key === activeKey ? ' active' : ''}`, href: `#/catalog?tab=${c.key}`, 'aria-current': c.key === activeKey ? 'page' : null }, c.label)))));

  async function openForm(item) {
    const isEdit = Boolean(item);
    const fields = [
      { name: 'code', label: 'Mã', required: true, maxLength: 20, colClass: 'col-sm-4', help: 'Tự chuyển sang chữ in hoa.' },
      { name: 'name', label: 'Tên', required: true, maxLength: 100, colClass: 'col-sm-8' },
      ...catalog.fields.map((f) => (isEdit && f.name === 'allowsDeduction' ? { ...f, help: 'Không đổi được sau khi tạo.' } : f)),
      { name: 'isActive', label: 'Đang sử dụng', type: 'switch' }
    ];
    const saved = await openFormModal({
      title: `${isEdit ? 'Sửa' : 'Thêm'} ${catalog.label.toLowerCase()}`,
      fields,
      initial: item ?? { isActive: true, level: 1 },
      onSubmit: (v) => api(isEdit ? `${catalog.url}/${item.id}` : catalog.url, { method: isEdit ? 'PUT' : 'POST', body: v })
    });
    if (saved) { toast('Đã lưu.', 'success'); table.reload(); }
  }

  async function deactivate(item) {
    const ok = await confirmDialog({ title: 'Ngừng sử dụng', message: `Ngừng sử dụng "${item.name}"?`, confirmText: 'Ngừng sử dụng' });
    if (!ok) return;
    try {
      await api(`${catalog.url}/${item.id}`, { method: 'DELETE' });
      toast('Đã ngừng sử dụng.', 'success');
      table.reload();
    } catch (error) {
      toast(error.message, 'danger');
    }
  }

  const host = h('div');
  container.append(host);
  const table = createDataTable(host, {
    signal,
    search: false,
    caption: catalog.label,
    toolbarActions: [h('button', { type: 'button', class: 'btn btn-primary', onclick: () => openForm(null) }, icon('plus-lg', 'me-1'), `Thêm ${catalog.label.toLowerCase()}`)],
    emptyMessage: 'Chưa có dữ liệu.',
    columns: [
      { key: 'code', label: 'Mã', className: 'text-nowrap fw-semibold' },
      { key: 'name', label: 'Tên' },
      ...catalog.columns,
      { key: 'isActive', label: 'Trạng thái', render: (x) => activeBadge(x.isActive) },
      {
        key: 'actions', label: 'Thao tác', className: 'text-end text-nowrap',
        render: (x) => h('div', { class: 'd-inline-flex gap-1' },
          iconButton('pencil', 'Sửa', () => openForm(x)),
          x.isActive ? iconButton('slash-circle', 'Ngừng sử dụng', () => deactivate(x), 'outline-danger') : null)
      }
    ],
    fetchPage: async (_params, s) => singlePage(await api(catalog.url, { query: { includeInactive: true }, signal: s }))
  });
}
