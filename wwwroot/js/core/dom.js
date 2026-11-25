// Tạo phần tử DOM an toàn: mọi chuỗi dữ liệu được chèn dưới dạng TEXT (không dùng innerHTML)
// nên dữ liệu người dùng nhập như "<script>" chỉ hiển thị chữ, không chạy được (chống XSS).

export function h(tag, attrs = {}, ...children) {
  const element = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs || {})) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'class') element.className = value;
    else if (key === 'dataset') Object.assign(element.dataset, value);
    else if (key.startsWith('on') && typeof value === 'function') element.addEventListener(key.slice(2).toLowerCase(), value);
    else if (value === true) element.setAttribute(key, '');
    else element.setAttribute(key, String(value));
  }
  append(element, children);
  return element;
}

export function append(parent, children) {
  for (const child of [children].flat(Infinity)) {
    if (child === undefined || child === null || child === false) continue;
    parent.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return parent;
}

export function icon(name, extraClass = '') {
  return h('i', { class: `bi bi-${name}${extraClass ? ` ${extraClass}` : ''}`, 'aria-hidden': 'true' });
}

export function debounce(fn, delay = 300) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };
}
