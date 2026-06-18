export const GUEST_TOKEN = 'guest-dev-bypass-token';
export const GUEST_GROUP_ID = '00000000-0000-0000-0000-000000000001';

export function isGuestSession(): boolean {
  return (
    sessionStorage.getItem('isGuestLogin') === 'true' ||
    localStorage.getItem('DIApiToken') === GUEST_TOKEN
  );
}
