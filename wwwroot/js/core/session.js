// Phiên đăng nhập lưu trong sessionStorage: tự mất khi đóng tab, không chia sẻ giữa các tab.
// Gồm access token (JWT), refresh token và thông tin hiển thị (username, role, employeeId).
const SESSION_KEY = 'leavemanage.session';

export function saveSession(authResponse) {
  sessionStorage.setItem(SESSION_KEY, JSON.stringify({
    token: authResponse.token,
    refreshToken: authResponse.refreshToken,
    expiresAt: authResponse.expiresAt,
    user: {
      username: authResponse.username,
      role: authResponse.role,
      employeeId: authResponse.employeeId ?? null
    }
  }));
}

export function getSession() {
  try {
    return JSON.parse(sessionStorage.getItem(SESSION_KEY) || 'null');
  } catch {
    clearSession();
    return null;
  }
}

export function getToken() {
  return getSession()?.token ?? null;
}

export function getRefreshToken() {
  return getSession()?.refreshToken ?? null;
}

export function getUser() {
  return getSession()?.user ?? null;
}

export function clearSession() {
  sessionStorage.removeItem(SESSION_KEY);
}
