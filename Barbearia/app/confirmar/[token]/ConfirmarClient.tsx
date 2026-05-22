'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import Navbar from '../../components/Navbar'

const API = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5134'

type State = 'loading' | 'success' | 'error'

export default function ConfirmarClient({ token }: { token: string }) {
  const [state, setState] = useState<State>('loading')
  const [message, setMessage] = useState('A confirmar a tua marcacao...')

  useEffect(() => {
    fetch(`${API}/api/bookings/confirm/${encodeURIComponent(token)}`)
      .then(async response => {
        if (!response.ok) {
          const data = await response.json().catch(() => null)
          throw new Error(data?.title || 'Link invalido ou expirado.')
        }

        setState('success')
        setMessage('A tua marcacao foi confirmada.')
      })
      .catch(error => {
        setState('error')
        setMessage(error instanceof Error ? error.message : 'Nao foi possivel confirmar a marcacao.')
      })
  }, [token])

  return (
    <main className="min-h-screen bg-black text-white">
      <Navbar activePage="marcar" />

      <section className="min-h-screen px-6 pt-36 pb-16 flex items-center justify-center">
        <div className="w-full max-w-md text-center">
          <div className={`w-16 h-16 mx-auto mb-8 border flex items-center justify-center text-2xl ${
            state === 'success'
              ? 'border-emerald-400 text-emerald-300'
              : state === 'error'
                ? 'border-red-400 text-red-300'
                : 'border-white/30 text-zinc-300'
          }`}>
            {state === 'loading' ? '...' : state === 'success' ? '✓' : '!'}
          </div>

          <p className="text-[10px] tracking-[0.6em] text-zinc-500 uppercase mb-4">Elegance Studio</p>
          <h1 className="font-serif text-[clamp(2.5rem,8vw,64px)] leading-none uppercase tracking-tight mb-6">
            Confirmacao
          </h1>
          <p className="text-sm text-zinc-400 leading-relaxed mb-10">{message}</p>

          <Link
            href="/main"
            className="inline-flex border border-white/20 px-8 py-4 text-[11px] tracking-[0.35em] uppercase hover:bg-white hover:text-black transition-all"
          >
            Voltar ao inicio
          </Link>
        </div>
      </section>
    </main>
  )
}
