'use client'

import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { login } from '@/lib/api'
import { saveAuth } from '@/lib/auth'

export default function LoginPage() {
  const router = useRouter()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [mounted, setMounted] = useState(false)

  useEffect(() => {
    setMounted(true)
  }, [])

  const handleLogin = async () => {
    if (!username.trim() || !password || loading) return

    setError('')
    setLoading(true)
    try {
      const data = await login(username.trim(), password)
      saveAuth(data)
      router.push('/dashboard')
    } catch {
      setError('Credenciais invalidas.')
      setLoading(false)
    }
  }

  return (
    <main className="min-h-screen bg-[#080808] text-white grid lg:grid-cols-[1fr_460px]">
      <section className="hidden lg:flex relative overflow-hidden border-r border-white/10">
        <div className="absolute inset-0 bg-[linear-gradient(rgba(255,255,255,0.025)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.025)_1px,transparent_1px)] bg-[size:56px_56px]" />
        <div className="relative z-10 flex flex-col justify-between w-full p-12">
          <div className="flex items-center gap-4">
            <Image src="/logo.png" alt="Elegance Studio" width={74} height={74} className="h-16 w-auto" priority />
            <div>
              <p className="text-[11px] tracking-[0.32em] uppercase text-zinc-500">Elegance Studio</p>
              <h1 className="font-serif text-5xl uppercase leading-none tracking-tight">Gestao</h1>
            </div>
          </div>

          <div className="max-w-xl">
            <p className="text-[11px] tracking-[0.4em] uppercase text-zinc-500 mb-5">Area reservada</p>
            <h2 className="font-serif text-[76px] uppercase leading-[0.86] tracking-tight">
              Agenda<br />
              <span className="text-zinc-600">sem ruido</span>
            </h2>
            <p className="mt-8 max-w-md text-sm leading-7 text-zinc-400">
              Consulta marcacoes, confirma clientes e mantem o dia organizado num painel pensado para trabalho real de barbearia.
            </p>
          </div>

          <div className="grid grid-cols-3 gap-3 max-w-xl">
            {[
              ['Tempo real', 'SignalR ativo'],
              ['Acesso seguro', 'JWT por perfil'],
              ['Agenda diaria', 'Lista e timeline'],
            ].map(([title, desc]) => (
              <div key={title} className="border border-white/10 bg-white/[0.025] px-4 py-4">
                <p className="text-[10px] uppercase tracking-[0.24em] text-zinc-300">{title}</p>
                <p className="mt-2 text-[11px] text-zinc-600">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="flex min-h-screen items-center justify-center px-6 py-12">
        <div className={`w-full max-w-sm transition-all duration-500 ${mounted ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-3'}`}>
          <div className="lg:hidden mb-10 flex justify-center">
            <Image src="/logo.png" alt="Elegance Studio" width={92} height={92} priority />
          </div>

          <div className="mb-9">
            <p className="text-[10px] uppercase tracking-[0.42em] text-zinc-500 mb-4">Dashboard</p>
            <h1 className="font-serif text-5xl uppercase leading-none tracking-tight">Entrar</h1>
            <p className="mt-4 text-sm leading-6 text-zinc-500">Usa as credenciais do barbeiro ou administrador.</p>
          </div>

          <div className="space-y-5">
            <label className="block">
              <span className="mb-2 block text-[10px] font-semibold uppercase tracking-[0.32em] text-zinc-500">Utilizador</span>
              <input
                value={username}
                onChange={e => setUsername(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleLogin()}
                autoComplete="username"
                className="input-elegant"
                placeholder="ex: edi"
              />
            </label>

            <label className="block">
              <span className="mb-2 block text-[10px] font-semibold uppercase tracking-[0.32em] text-zinc-500">Password</span>
              <input
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleLogin()}
                autoComplete="current-password"
                className="input-elegant"
                placeholder="A tua password"
              />
            </label>

            {error && (
              <div className="border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-300">
                {error}
              </div>
            )}

            <button
              onClick={handleLogin}
              disabled={!username.trim() || !password || loading}
              className={`w-full min-h-14 border px-6 text-[11px] font-bold uppercase tracking-[0.32em] transition-all ${
                username.trim() && password && !loading
                  ? 'border-white bg-white text-black hover:bg-zinc-200'
                  : 'border-white/10 text-zinc-700 cursor-not-allowed'
              }`}
            >
              {loading ? 'A entrar...' : 'Entrar'}
            </button>
          </div>

          <p className="mt-10 border-t border-white/8 pt-6 text-[10px] uppercase tracking-[0.28em] text-zinc-700">
            Elegance Studio - Gestao interna
          </p>
        </div>
      </section>
    </main>
  )
}
