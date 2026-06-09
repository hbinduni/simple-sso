import {AuthSection} from '@client/components/auth'
import {config} from '@client/config'
import {useEffect, useState} from 'react'

const techStack = [
  {name: '.NET 10', color: 'var(--color-dotnet)', icon: '.NET'},
  {name: 'ASP.NET Core', color: 'var(--color-aspnet)', icon: 'API'},
  {name: 'Entra ID', color: 'var(--color-aspnet)', icon: 'ID'},
  {name: 'React 19', color: 'var(--color-react)', icon: 'R'},
  {name: 'Vite 7', color: 'var(--color-vite)', icon: 'V'},
  {name: 'Tailwind 4', color: 'var(--color-tailwind)', icon: 'T'},
  {name: 'tsgo', color: 'var(--color-tsgo)', icon: 'TS'},
  {name: 'Docker', color: 'var(--color-docker)', icon: 'D'},
]

function StatusDot({alive}: {alive: boolean}) {
  return (
    <span className="relative flex size-2">
      {alive && (
        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
      )}
      <span className={`relative inline-flex size-2 rounded-full ${alive ? 'bg-emerald-400' : 'bg-white/20'}`} />
    </span>
  )
}

export function Home() {
  const [apiAlive, setApiAlive] = useState(false)
  const [checking, setChecking] = useState(true)

  useEffect(() => {
    fetch(`${config.VITE_API_URL}/health`)
      .then((res) => setApiAlive(res.ok))
      .catch(() => setApiAlive(false))
      .finally(() => setChecking(false))
  }, [])

  return (
    <div className="w-full">
      {/* Hero */}
      <section className="relative overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-b from-accent-500/5 via-transparent to-transparent" />
        <div className="relative max-w-6xl mx-auto px-6 pt-20 pb-16 text-center">
          <p
            className="animate-fade-up font-mono text-xs tracking-widest uppercase text-white/40 mb-6"
            style={{animationDelay: '0.05s'}}
          >
            Stateless Single Sign-On
          </p>
          <h1
            className="animate-fade-up text-4xl sm:text-5xl md:text-6xl font-display font-bold tracking-tight leading-[1.1] mb-6"
            style={{animationDelay: '0.15s'}}
          >
            <span className="text-gradient">Sign in with</span>
            <br />
            <span className="text-white/90">Microsoft Entra ID</span>
          </h1>
          <p
            className="animate-fade-up max-w-xl mx-auto text-sm sm:text-base text-white/40 leading-relaxed font-light mb-10"
            style={{animationDelay: '0.25s'}}
          >
            A lightweight SSO broker: authenticate against Microsoft Entra ID and receive stateless JWTs. No database,
            no user store — identity and group memberships come straight from the token.
          </p>

          {/* API status pill */}
          <div
            className="animate-fade-up inline-flex items-center gap-2 rounded-full border border-white/10 bg-surface-1/80 backdrop-blur px-4 py-2 text-xs font-mono text-white/50"
            style={{animationDelay: '0.35s'}}
          >
            <StatusDot alive={apiAlive} />
            <span className={apiAlive ? 'text-emerald-400' : 'text-white/30'}>
              {checking ? 'connecting...' : apiAlive ? 'API connected' : 'API offline'}
            </span>
            <span className="text-white/15">|</span>
            <span className="text-white/30 truncate max-w-[200px]">{config.VITE_API_URL}</span>
          </div>
        </div>
      </section>

      {/* Microsoft Entra ID auth */}
      <AuthSection />

      {/* Tech Stack */}
      <section className="max-w-6xl mx-auto px-6 pb-20 pt-8">
        <div className="animate-fade-up grid grid-cols-2 sm:grid-cols-4 gap-3" style={{animationDelay: '0.4s'}}>
          {techStack.map((tech) => (
            <div
              key={tech.name}
              className="tech-badge group flex items-center gap-3 rounded-xl border border-white/5 bg-surface-1/60 px-4 py-3"
            >
              <span
                className="flex size-8 shrink-0 items-center justify-center rounded-lg text-[11px] font-mono font-semibold"
                style={{
                  backgroundColor: `color-mix(in srgb, ${tech.color} 15%, transparent)`,
                  color: tech.color,
                }}
              >
                {tech.icon}
              </span>
              <span className="text-sm text-white/60 group-hover:text-white/80 transition-colors">{tech.name}</span>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}
