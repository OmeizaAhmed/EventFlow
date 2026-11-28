import { useState } from 'react'
import type { FormEvent } from 'react'

type Invitation = {
  projectName: string
  role: string
  status: 'Pending' | 'Accepted' | 'Revoked' | 'Expired'
  expiresAt: string
  canAccept: boolean
}

async function readResponse<T>(response: Response, fallback: string): Promise<T> {
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(typeof body?.error === 'string' ? body.error : fallback)
  }

  return response.json()
}

const buttonClass = 'rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-50'
const fieldClass = 'mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2'
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

export default function AcceptInvitation({ invitationId }: { invitationId: string | null }) {
  const [session, setSession] = useState<{ token: string; invitation: Invitation } | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const validId = invitationId !== null &&
    /^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i.test(invitationId)

  async function signIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true)
    setError('')
    try {
      const login = await readResponse<{ token: string }>(await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: form.get('email'), password: form.get('password') }),
      }), 'Could not sign in. Check your email and password.')

      const response = await fetch(`/api/project-invitations/${invitationId}`, {
        headers: { Authorization: `Bearer ${login.token}` },
      })
      if (response.status === 404) {
        throw new Error('This invitation is unavailable for this account. Sign in with the address that received the invitation.')
      }
      const invitation = await readResponse<Invitation>(response, 'Could not load the invitation.')
      setSession({ token: login.token, invitation })
    } catch (problem) {
      setError(problem instanceof Error ? problem.message : 'Could not load the invitation.')
    } finally {
      setBusy(false)
    }
  }

  async function accept() {
    if (!session) return

    setBusy(true)
    setError('')
    try {
      const response = await fetch(`/api/project-invitations/${invitationId}/accept`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${session.token}` },
      })
      await readResponse(response, 'Could not accept the invitation. Sign in again or ask for a new invitation.')
      setSession({ ...session, invitation: { ...session.invitation, status: 'Accepted' } })
    } catch (problem) {
      setError(problem instanceof Error ? problem.message : 'Could not accept the invitation.')
    } finally {
      setBusy(false)
    }
  }

  const invitation = session?.invitation

  return (
    <main className="grid min-h-screen place-items-center bg-slate-100 px-4 py-12 font-sans text-slate-900">
      <section className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-7 shadow-sm sm:p-9">
        <a href="/" className="text-sm font-semibold text-slate-600 underline-offset-4 hover:underline">
          EventFlow
        </a>
        <h1 className="mt-5 text-2xl font-semibold tracking-tight">
          {invitation ? `Join ${invitation.projectName}` : 'Project invitation'}
        </h1>

        {!validId ? (
          <p className="mt-4 text-sm text-slate-700">Open the invitation link from your email.</p>
        ) : !invitation ? (
          <form onSubmit={signIn} className="mt-4 space-y-4">
            <p className="text-sm text-slate-700">Sign in with the email address that received the invitation.</p>
            <label className="block text-sm font-medium">
              Email
              <input className={fieldClass} name="email" type="email" autoComplete="username" required />
            </label>
            <label className="block text-sm font-medium">
              Password
              <input className={fieldClass} name="password" type="password" autoComplete="current-password" required />
            </label>
            <button className={buttonClass} type="submit" disabled={busy}>
              {busy ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        ) : (
          <div className="mt-4 space-y-4 text-sm">
            {invitation.status === 'Pending' && (
              <>
                <p>Role offered: <strong>{invitation.role}</strong></p>
                <p className="text-slate-600">Expires {dateFormat.format(new Date(invitation.expiresAt))}</p>
                {invitation.canAccept ? (
                  <button className={buttonClass} type="button" onClick={accept} disabled={busy}>
                    {busy ? 'Accepting…' : 'Accept invitation'}
                  </button>
                ) : <p>Sign in with the invited account to accept.</p>}
              </>
            )}
            {invitation.status === 'Accepted' && <p role="status">Invitation accepted.</p>}
            {invitation.status === 'Revoked' && <p>This invitation was revoked.</p>}
            {invitation.status === 'Expired' && <p>This invitation expired. Ask a project manager for a new invitation.</p>}
            <button type="button" className="block underline underline-offset-4" disabled={busy} onClick={() => {
              setSession(null)
              setError('')
            }}>
              Use another account
            </button>
          </div>
        )}
        {error && <p className="mt-4 text-sm text-red-700" role="alert">{error}</p>}
      </section>
    </main>
  )
}
