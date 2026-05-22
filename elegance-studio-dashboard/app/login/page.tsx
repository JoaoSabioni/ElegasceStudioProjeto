'use client'

import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { login } from '@/lib/api'
import { saveAuth } from '@/lib/auth'

const CLIENT_MURAL_IMAGES = [
  '/Fotos_edi/edi3.png',
  '/Fotos_Tomas/tomas3.png',
  '/Fotos_Abreu/abreu1.png',
  '/Fotos_edi/edi4.png',
  '/Fotos_Tomas/tomas4.png',
  '/Fotos_Abreu/abreu2.png',
  '/Fotos_edi/edi5.png',
  '/Fotos_Tomas/tomas5.png',
  '/Fotos_Abreu/abreu3.png',
  '/Fotos_Tomas/tomas6.png',
  '/Fotos_Abreu/abreu4.png',
  '/Fotos_Abreu/abreu5.png',
]

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
    <main className="min-h-screen bg-[#070604] text-white grid lg:grid-cols-[1fr_460px]">
      <section className="hidden lg:flex relative overflow-hidden border-r border-white/10">
        <div className="absolute inset-0 grid grid-cols-4 opacity-[0.34]">
          {CLIENT_MURAL_IMAGES.map((src, index) => (
            <div key={src} className={`relative min-h-[24vh] ${index % 5 === 0 ? 'row-span-2' : ''}`}>
              <Image src={src} alt="" fill className="object-cover grayscale" sizes="25vw" priority={index < 4} />
            </div>
          ))}
        </div>
        <div className="absolute inset-0 bg-[linear-gradient(90deg,#070604_0%,rgba(7,6,4,0.78)_46%,rgba(7,6,4,0.94)_100%)]" />
        <div className="relative z-10 flex flex-col justify-between w-full p-12">
          <div className="flex items-center gap-4">
            <Image src="/logo.png" alt="Elegance Studio" width={74} height={74} className="h-16 w-auto" priority />
            <div>
              <p className="text-[11px] tracking-[0.32em] uppercase text-zinc-500">Elegance Studio</p>
              <h1 className="font-serif text-5xl uppercase leading-none tracking-tight">Gestao interna</h1>
            </div>
          </div>

          <div className="max-w-xl">
            <p className="text-[11px] tracking-[0.4em] uppercase text-zinc-500 mb-5">Pinhal Novo · Portugal</p>
            <h2 className="font-serif text-[72px] uppercase leading-[0.88] tracking-tight">
              Elegance<br />
              <span className="text-zinc-500">Studio</span>
            </h2>
            <p className="mt-8 max-w-md text-sm leading-7 text-zinc-400">
              Painel reservado para gerir a agenda da equipa, acompanhar marcacoes e manter o atendimento organizado durante o dia.
            </p>
          </div>

          <div className="grid grid-cols-3 gap-3 max-w-xl">
            {[
              ['Equipa', 'Edi · Tomas · Abreu'],
              ['Horario', '09:00 - 19:00'],
              ['Contacto', '+351 933 320 269'],
            ].map(([title, desc]) => (
              <div key={title} className="border border-white/10 bg-black/30 px-4 py-4 backdrop-blur-sm">
                <p className="text-[10px] uppercase tracking-[0.24em] text-zinc-300">{title}</p>
                <p className="mt-2 text-[11px] text-zinc-500">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="relative flex min-h-screen items-center justify-center overflow-hidden px-6 py-12">
        <div className="absolute inset-0 lg:hidden">
          <Image src="/Fotos_Tomas/tomas5.png" alt="" fill className="object-cover opacity-20" priority />
          <div className="absolute inset-0 bg-[#070604]/88" />
        </div>
        <div className={`w-full max-w-sm transition-all duration-500 ${mounted ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-3'}`}>
          <div className="relative lg:hidden mb-10 flex justify-center">
            <Image src="/logo.png" alt="Elegance Studio" width={92} height={92} priority />
          </div>

          <div className="relative mb-9">
            <p className="text-[10px] uppercase tracking-[0.42em] text-zinc-500 mb-4">Elegance Studio</p>
            <h1 className="font-serif text-5xl uppercase leading-none tracking-tight">Entrar</h1>
          </div>

          <div className="relative mb-7 grid grid-cols-3 gap-2">
            {[
              ['/Fotos_edi/edi2.png', 'Edi'],
              ['/Fotos_Tomas/tomas2.png', 'Tomas'],
              ['/Fotos_Abreu/abreuPrincipal.jpg', 'Abreu'],
            ].map(([src, name]) => (
              <div key={name} className="relative aspect-square overflow-hidden border border-white/10 bg-white/[0.03]">
                <Image src={src} alt={name} fill className="object-cover opacity-80" sizes="110px" />
              </div>
            ))}
          </div>

          <div className="relative space-y-5">
            <label className="block">
              <span className="mb-2 block text-[10px] font-semibold uppercase tracking-[0.32em] text-zinc-500">Utilizador</span>
              <input
                value={username}
                onChange={e => setUsername(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleLogin()}
                autoComplete="username"
                className="input-elegant"
                placeholder="Utilizador"
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

          <p className="relative mt-10 border-t border-white/8 pt-6 text-[10px] uppercase tracking-[0.28em] text-zinc-700">
            Pinhal Novo · Gestao interna
          </p>
        </div>
      </section>
    </main>
  )
}
