import {captureTokensFromUrl, clearTokens, getAccessToken, getMe, loginWithMicrosoft, logout} from '@client/api/auth'
import type {MeResponse} from '@client/types'
import {useEffect, useState} from 'react'

type Status = 'loading' | 'anon' | 'authed' | 'error'

// Map the backend's OAuth error codes (returned in the callback fragment) to readable copy.
const ERROR_COPY: Record<string, string> = {
  state_mismatch: 'Login could not be verified (state mismatch). Please try again.',
  token_exchange_failed: 'Microsoft rejected the sign-in. Please try again.',
  invalid_oauth_response: 'The sign-in response was incomplete. Please try again.',
  account_link_failed: 'Could not link your Microsoft account.',
  server_error: 'Sign-in succeeded but the server could not complete it (is the database reachable?).',
}

// Marks (per browser tab) that a silent SSO was already attempted, to avoid a redirect loop.
const SILENT_TRIED = 'sso_silent_tried'

function MicrosoftLogo() {
  return (
    <svg width="16" height="16" viewBox="0 0 23 23" aria-hidden="true">
      <rect x="1" y="1" width="10" height="10" fill="#F25022" />
      <rect x="12" y="1" width="10" height="10" fill="#7FBA00" />
      <rect x="1" y="12" width="10" height="10" fill="#00A4EF" />
      <rect x="12" y="12" width="10" height="10" fill="#FFB900" />
    </svg>
  )
}

function RoleBadge({role}: {role: string}) {
  const tone =
    role === 'admin'
      ? 'text-rose-300 bg-rose-500/10 border-rose-500/20'
      : role === 'moderator'
        ? 'text-amber-300 bg-amber-500/10 border-amber-500/20'
        : 'text-accent-300 bg-accent-500/10 border-accent-500/20'
  return (
    <span className={`rounded-full border px-2 py-0.5 text-[10px] font-mono uppercase tracking-wider ${tone}`}>
      {role}
    </span>
  )
}

export function AuthSection() {
  const [status, setStatus] = useState<Status>('loading')
  const [me, setMe] = useState<MeResponse | null>(null)
  const [error, setError] = useState<string>()

  useEffect(() => {
    const {error: cbError, ssoRequired} = captureTokensFromUrl()
    if (cbError) {
      setError(ERROR_COPY[cbError] ?? `Sign-in failed: ${cbError}`)
      setStatus('error')
      return
    }
    if (ssoRequired) {
      // Silent SSO found no Entra session — show the landing page with the button.
      sessionStorage.setItem(SILENT_TRIED, '1')
      setStatus('anon')
      return
    }
    if (getAccessToken()) {
      getMe()
        .then((data) => {
          sessionStorage.removeItem(SILENT_TRIED)
          setMe(data)
          setStatus('authed')
        })
        .catch(() => {
          clearTokens()
          setStatus('anon')
        })
      return
    }
    // No local session: try silent SSO once per tab. If the user already has an Entra session
    // (e.g. signed into another app) they land signed in with no prompt; otherwise Entra returns
    // login_required and we show the button. The flag prevents a redirect loop on reload.
    if (sessionStorage.getItem(SILENT_TRIED)) {
      setStatus('anon')
      return
    }
    sessionStorage.setItem(SILENT_TRIED, '1')
    loginWithMicrosoft(true)
  }, [])

  async function handleLogout() {
    await logout()
    setMe(null)
    setStatus('anon')
  }

  return (
    <section className="max-w-6xl mx-auto px-6 pb-4">
      <div className="animate-fade-up mx-auto max-w-md" style={{animationDelay: '0.45s'}}>
        {status === 'loading' && (
          <div className="rounded-2xl border border-white/5 bg-surface-1/40 p-6">
            <div className="h-10 animate-pulse rounded-lg bg-white/5" />
          </div>
        )}

        {(status === 'anon' || status === 'error') && (
          <div className="rounded-2xl border border-white/5 bg-surface-1/40 p-6 text-center">
            <p className="mb-1 text-sm text-white/70">Sign in to view your profile</p>
            <p className="mb-5 text-xs text-white/35">Authenticate with your Microsoft Entra ID account</p>
            <button
              type="button"
              onClick={() => loginWithMicrosoft()}
              className="inline-flex w-full items-center justify-center gap-2.5 rounded-lg border border-white/10 bg-white px-4 py-2.5 text-sm font-medium text-gray-800 transition-colors hover:bg-white/90"
            >
              <MicrosoftLogo />
              Sign in with Microsoft
            </button>
            {status === 'error' && error && <p className="mt-4 text-xs text-red-400">{error}</p>}
          </div>
        )}

        {status === 'authed' && me && (
          <div className="rounded-2xl border border-white/5 bg-surface-1/40 overflow-hidden text-left">
            <div className="flex items-center gap-4 border-b border-white/5 px-6 py-5">
              {me.user.avatarUrl ? (
                <img
                  src={me.user.avatarUrl}
                  alt=""
                  className="size-12 shrink-0 rounded-full object-cover ring-1 ring-white/10"
                />
              ) : (
                <div className="flex size-12 shrink-0 items-center justify-center rounded-full bg-accent-500/15 text-lg font-semibold text-accent-300">
                  {me.user.name.charAt(0).toUpperCase()}
                </div>
              )}
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <h3 className="truncate text-sm font-semibold text-white/90">{me.user.name}</h3>
                  <RoleBadge role={me.user.role} />
                </div>
                <p className="truncate text-xs text-white/40">{me.user.email}</p>
              </div>
            </div>

            <div className="px-6 py-5">
              <div className="mb-2 flex items-center justify-between">
                <span className="font-mono text-[10px] uppercase tracking-widest text-white/30">Groups</span>
                <span className="font-mono text-[10px] text-white/20">{me.groups.length}</span>
              </div>
              {me.groups.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {me.groups.map((group) => (
                    <span
                      key={group}
                      className="rounded-md border border-white/5 bg-surface-2/60 px-2.5 py-1 text-xs text-white/60"
                    >
                      {group}
                    </span>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-white/30">
                  No groups found. (The app needs the <span className="font-mono">GroupMember.Read.All</span> permission
                  with admin consent in Entra to list groups.)
                </p>
              )}
            </div>

            <div className="border-t border-white/5 px-6 py-4">
              <button
                type="button"
                onClick={handleLogout}
                className="inline-flex w-full items-center justify-center rounded-lg border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/70 transition-colors hover:bg-white/10 hover:text-white/90"
              >
                Log out
              </button>
            </div>
          </div>
        )}
      </div>
    </section>
  )
}
