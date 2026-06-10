import {config} from '@client/config'
import type {ApiResponse, MeResponse, RefreshTokenResponse} from '@client/types'

const API_BASE_URL = config.VITE_API_URL

const ACCESS_KEY = 'sso_access_token'
const REFRESH_KEY = 'sso_refresh_token'

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_KEY)
}

function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_KEY)
}

function saveTokens(access: string, refresh: string): void {
  localStorage.setItem(ACCESS_KEY, access)
  localStorage.setItem(REFRESH_KEY, refresh)
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS_KEY)
  localStorage.removeItem(REFRESH_KEY)
}

/** Begin the Microsoft Entra ID login: a full-page redirect to the backend, which
 *  bounces through Entra and returns to {FRONTEND_URL}/auth/callback with the tokens.
 *  User-initiated (button) only — we intentionally don't auto-redirect on page load.
 *
 *  `loginHint` (an email/UPN, not a secret) is forwarded to Entra to pre-select the account, so a
 *  user arriving from another app on the same tenant signs in without the account picker. */
export function loginWithMicrosoft(loginHint?: string): void {
  const url = new URL(`${API_BASE_URL}/api/auth/oauth/microsoft`)
  if (loginHint) url.searchParams.set('login_hint', loginHint)
  window.location.href = url.toString()
}

/** After the OAuth callback the tokens (or an error) arrive in the URL fragment. Persist
 *  them and scrub the URL so they never linger in history. Safe to call on every load. */
export function captureTokensFromUrl(): {captured: boolean; error?: string} {
  const hash = window.location.hash.replace(/^#/, '')
  if (!hash) return {captured: false}

  const params = new URLSearchParams(hash)
  const access = params.get('accessToken')
  const refresh = params.get('refreshToken')
  const error = params.get('error')

  if (access && refresh) {
    saveTokens(access, refresh)
    history.replaceState(null, '', '/')
    return {captured: true}
  }
  if (error) {
    history.replaceState(null, '', '/')
    return {captured: false, error}
  }
  return {captured: false}
}

/** Load the current user + groups, transparently refreshing the access token once on 401. */
export async function getMe(): Promise<MeResponse> {
  const token = getAccessToken()
  if (!token) throw new Error('Not authenticated')

  let res = await fetch(`${API_BASE_URL}/api/auth/me`, {
    headers: {Authorization: `Bearer ${token}`},
  })

  if (res.status === 401) {
    const refreshed = await tryRefresh()
    if (!refreshed) {
      clearTokens()
      throw new Error('Session expired')
    }
    res = await fetch(`${API_BASE_URL}/api/auth/me`, {
      headers: {Authorization: `Bearer ${refreshed}`},
    })
  }

  const data: ApiResponse<MeResponse> = await res.json()
  if (!data.success || !data.data) throw new Error(data.error || 'Failed to load profile')
  return data.data
}

async function tryRefresh(): Promise<string | null> {
  const refresh = getRefreshToken()
  if (!refresh) return null

  const res = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({refreshToken: refresh}),
  })

  const data: ApiResponse<RefreshTokenResponse> = await res
    .json()
    .catch(() => ({success: false}) as ApiResponse<RefreshTokenResponse>)
  if (!res.ok || !data.success || !data.data) return null

  localStorage.setItem(ACCESS_KEY, data.data.accessToken)
  return data.data.accessToken
}

/** Best-effort server logout, then drop local tokens regardless of the result. */
export async function logout(): Promise<void> {
  const token = getAccessToken()
  if (token) {
    await fetch(`${API_BASE_URL}/api/auth/logout`, {
      method: 'POST',
      headers: {Authorization: `Bearer ${token}`},
    }).catch(() => {})
  }
  clearTokens()
}

/** Federated sign-out: end the Entra session too (so silent SSO won't immediately sign the
 *  user back in), then Entra returns to the SPA. Call after clearing local tokens. */
export function signOutOfMicrosoft(): void {
  window.location.href = `${API_BASE_URL}/api/auth/oauth/microsoft/logout`
}
