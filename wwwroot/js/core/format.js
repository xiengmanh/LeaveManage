const dateTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'Asia/Ho_Chi_Minh'
});

const numberFormatter = new Intl.NumberFormat('vi-VN');

// Ngày từ API có dạng "2026-12-07" hoặc "2026-12-07T00:00:00": tách chuỗi trực tiếp để không bị lệch ngày do múi giờ.
export function formatDate(value) {
  if (!value) return '';
  const [year, month, day] = String(value).slice(0, 10).split('-');
  return `${day}/${month}/${year}`;
}

// Thời điểm (UTC, có hậu tố Z) → hiển thị theo giờ Việt Nam (UTC+7).
export function formatDateTime(value) {
  return value ? dateTimeFormatter.format(new Date(value)) : '';
}

export function formatNumber(value) {
  return numberFormatter.format(value ?? 0);
}

export function todayIso() {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}
