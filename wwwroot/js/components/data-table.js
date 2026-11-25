import { debounce, h, icon } from '../core/dom.js';
import { formatNumber } from '../core/format.js';
import { emptyRow, errorRow, loadingRows } from './states.js';

/**
 * Bảng dữ liệu dùng chung: tìm kiếm (debounce 300ms), bộ lọc, sắp xếp theo cột,
 * phân trang phía server. Mọi số liệu (tổng bản ghi, số trang) lấy từ API.
 *
 * columns: [{ key, label, sortKey?, className?, render?(row) }]
 * fetchPage(params, signal) → Promise<PagedResult>
 * filters: [{ name, label, options?, loadOptions?() }]
 */
export function createDataTable(container, options) {
  const {
    columns,
    fetchPage,
    searchPlaceholder = 'Tìm kiếm...',
    filters = [],
    initialSort = {},
    initialFilters = {},
    toolbarActions = [],
    emptyMessage = 'Chưa có dữ liệu.',
    emptyHint = '',
    caption = '',
    signal: routeSignal
  } = options;

  const state = {
    pageNumber: 1,
    pageSize: 10,
    searchTerm: '',
    sortBy: initialSort.sortBy ?? null,
    isDescending: Boolean(initialSort.isDescending),
    filters: { ...initialFilters }
  };

  let requestController = null;
  let requestSequence = 0;
  let firstLoad = true;

  // ---------- Thanh công cụ: tìm kiếm + lọc + nút thao tác ----------
  const searchInput = h('input', {
    type: 'search',
    class: 'form-control',
    placeholder: searchPlaceholder,
    'aria-label': searchPlaceholder,
    maxlength: 100
  });
  searchInput.addEventListener('input', debounce(() => {
    state.searchTerm = searchInput.value.trim();
    state.pageNumber = 1;
    load();
  }, 300));

  const filterSelects = {};
  const filterColumns = filters.map((filter) => {
    const select = h('select', { class: 'form-select', 'aria-label': filter.label },
      h('option', { value: '' }, `${filter.label}: Tất cả`),
      (filter.options || []).map((option) => h('option', { value: String(option.value) }, option.label)));
    filterSelects[filter.name] = select;
    select.value = state.filters[filter.name] != null ? String(state.filters[filter.name]) : '';
    select.addEventListener('change', () => {
      state.filters[filter.name] = select.value || null;
      state.pageNumber = 1;
      load();
    });

    if (filter.loadOptions) {
      filter.loadOptions().then((loaded) => {
        for (const option of loaded) select.append(h('option', { value: String(option.value) }, option.label));
        select.value = state.filters[filter.name] != null ? String(state.filters[filter.name]) : '';
      }).catch(() => { /* bộ lọc không tải được thì vẫn dùng được bảng */ });
    }
    return h('div', { class: 'col-12 col-sm-6 col-lg-auto' }, select);
  });

  const toolbar = h('div', { class: 'card-body border-bottom py-3' },
    h('div', { class: 'row g-2 align-items-center' },
      // search: false → ẩn ô tìm kiếm khi API không hỗ trợ tìm kiếm (không hiển thị chức năng giả)
      h('div', { class: `col-12 col-lg${options.search === false ? ' d-none' : ''}` },
        h('div', { class: 'input-group' },
          h('span', { class: 'input-group-text bg-white' }, icon('search')),
          searchInput)),
      filterColumns,
      toolbarActions.length
        ? h('div', { class: 'col-12 col-lg-auto d-flex flex-wrap gap-2 justify-content-lg-end' }, toolbarActions)
        : null));

  // ---------- Bảng ----------
  const headRow = h('tr');
  const tbody = h('tbody');
  const table = h('table', { class: 'table table-hover align-middle mb-0' },
    caption ? h('caption', { class: 'visually-hidden' }, caption) : null,
    h('thead', {}, headRow),
    tbody);

  // ---------- Chân bảng: tổng số + số dòng/trang + phân trang ----------
  const summary = h('div', { class: 'text-secondary small', 'aria-live': 'polite' });
  const pageSizeSelect = h('select', { class: 'form-select form-select-sm w-auto', 'aria-label': 'Số dòng mỗi trang' },
    [10, 20, 50].map((size) => h('option', { value: String(size) }, `${size} dòng/trang`)));
  pageSizeSelect.value = String(state.pageSize);
  pageSizeSelect.addEventListener('change', () => {
    state.pageSize = Number(pageSizeSelect.value);
    state.pageNumber = 1;
    load();
  });
  const pagination = h('ul', { class: 'pagination pagination-sm mb-0' });

  const footer = h('div', { class: 'card-footer bg-white d-flex flex-wrap gap-3 align-items-center justify-content-between py-3' },
    summary,
    h('div', { class: 'd-flex align-items-center gap-2 flex-wrap' },
      pageSizeSelect,
      h('nav', { 'aria-label': 'Phân trang' }, pagination)));

  const card = h('div', { class: 'card table-card' }, toolbar, h('div', { class: 'table-responsive' }, table), footer);
  container.append(card);

  function renderHead() {
    headRow.replaceChildren(...columns.map((column) => {
      const th = h('th', { scope: 'col', class: column.className || '' });
      if (!column.sortKey) {
        th.append(column.label);
        return th;
      }
      const active = state.sortBy === column.sortKey;
      th.setAttribute('aria-sort', active ? (state.isDescending ? 'descending' : 'ascending') : 'none');
      const button = h('button', { type: 'button', class: `sort-button${active ? ' active' : ''}` },
        column.label,
        icon(active ? (state.isDescending ? 'sort-down' : 'sort-up') : 'arrow-down-up', 'sort-icon'));
      button.addEventListener('click', () => {
        if (state.sortBy === column.sortKey) state.isDescending = !state.isDescending;
        else {
          state.sortBy = column.sortKey;
          state.isDescending = false;
        }
        state.pageNumber = 1;
        renderHead();
        load();
      });
      th.append(button);
      return th;
    }));
  }

  function hasActiveFilter() {
    return Boolean(state.searchTerm) || Object.values(state.filters).some((value) => value !== null && value !== undefined && value !== '');
  }

  function resetFilters() {
    searchInput.value = '';
    state.searchTerm = '';
    for (const name of Object.keys(state.filters)) state.filters[name] = null;
    for (const select of Object.values(filterSelects)) select.value = '';
    state.pageNumber = 1;
    load();
  }

  function renderRows(result) {
    if (result.items.length === 0) {
      tbody.replaceChildren(emptyRow(columns.length, hasActiveFilter()
        ? { iconName: 'search', title: 'Không có kết quả phù hợp', text: 'Hãy thử từ khóa khác hoặc xóa bộ lọc.', action: { label: 'Xóa bộ lọc', onClick: resetFilters } }
        : { title: emptyMessage, text: emptyHint }));
      return;
    }
    tbody.replaceChildren(...result.items.map((row) =>
      h('tr', {}, columns.map((column) =>
        h('td', { class: column.className || '' }, column.render ? column.render(row) : (row[column.key] ?? ''))))));
  }

  function renderSummary(result) {
    if (result.totalCount === 0) {
      summary.textContent = 'Không có bản ghi';
      return;
    }
    const from = (result.pageNumber - 1) * result.pageSize + 1;
    const to = from + result.items.length - 1;
    summary.textContent = `Hiển thị ${formatNumber(from)}–${formatNumber(to)} trên ${formatNumber(result.totalCount)} bản ghi`;
  }

  function renderPagination(result) {
    const { pageNumber, totalPages } = result;
    const item = (label, page, { disabled = false, active = false, ariaLabel } = {}) => {
      const button = h('button', {
        type: 'button',
        class: 'page-link',
        'aria-label': ariaLabel || `Trang ${page}`,
        'aria-current': active ? 'page' : null,
        disabled
      }, label);
      if (!disabled && !active) {
        button.addEventListener('click', () => {
          state.pageNumber = page;
          load();
        });
      }
      return h('li', { class: `page-item${disabled ? ' disabled' : ''}${active ? ' active' : ''}` }, button);
    };

    const items = [item(icon('chevron-left'), pageNumber - 1, { disabled: !result.hasPrevious, ariaLabel: 'Trang trước' })];
    const start = Math.max(1, Math.min(pageNumber - 2, totalPages - 4));
    const end = Math.min(totalPages, start + 4);
    for (let page = start; page <= end; page++) items.push(item(String(page), page, { active: page === pageNumber }));
    items.push(item(icon('chevron-right'), pageNumber + 1, { disabled: !result.hasNext, ariaLabel: 'Trang sau' }));
    pagination.replaceChildren(...items);
  }

  async function load() {
    // Hủy request cũ đang chạy; chỉ hiển thị kết quả của request mới nhất
    requestController?.abort();
    requestController = new AbortController();
    const sequence = ++requestSequence;
    const signal = routeSignal && AbortSignal.any
      ? AbortSignal.any([routeSignal, requestController.signal])
      : requestController.signal;

    if (firstLoad) tbody.replaceChildren(...loadingRows(columns.length));
    else table.classList.add('table-loading');
    card.setAttribute('aria-busy', 'true');

    const params = {
      pageNumber: state.pageNumber,
      pageSize: state.pageSize,
      searchTerm: state.searchTerm || undefined,
      sortBy: state.sortBy || undefined,
      isDescending: state.sortBy ? state.isDescending : undefined
    };
    for (const [name, value] of Object.entries(state.filters)) {
      if (value !== null && value !== undefined && value !== '') params[name] = value;
    }

    try {
      const result = await fetchPage(params, signal);
      if (sequence !== requestSequence) return;

      // Trang hiện tại trống (ví dụ vừa xóa dòng cuối cùng) → lùi về trang cuối còn dữ liệu
      if (result.items.length === 0 && result.totalCount > 0 && state.pageNumber > 1) {
        state.pageNumber = Math.max(1, result.totalPages);
        load();
        return;
      }

      renderRows(result);
      renderSummary(result);
      renderPagination(result);
    } catch (error) {
      if (error.name === 'AbortError' || sequence !== requestSequence) return;
      tbody.replaceChildren(errorRow(columns.length, error, () => load()));
      summary.textContent = '';
      pagination.replaceChildren();
    } finally {
      if (sequence === requestSequence) {
        firstLoad = false;
        table.classList.remove('table-loading');
        card.removeAttribute('aria-busy');
      }
    }
  }

  renderHead();
  load();

  return { reload: load, element: card };
}
