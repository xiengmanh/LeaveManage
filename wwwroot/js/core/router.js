// Hash router đơn giản: URL dạng /#/students?classId=1. Không cần cấu hình server cho từng trang.
const routes = new Map();

export function addRoute(path, handler, options = {}) {
  routes.set(path, { path, handler, roles: options.roles ?? null, title: options.title ?? '' });
}

export function findRoute(path) {
  return routes.get(path) ?? null;
}

export function parseHash() {
  const raw = window.location.hash.replace(/^#/, '') || '/';
  const [path, queryString = ''] = raw.split('?');
  return { path, query: new URLSearchParams(queryString) };
}

let onChange = null;

export function navigate(path) {
  if (window.location.hash === `#${path}`) onChange?.(parseHash());
  else window.location.hash = path;
}

export function startRouter(handler) {
  onChange = handler;
  window.addEventListener('hashchange', () => handler(parseHash()));
  handler(parseHash());
}
