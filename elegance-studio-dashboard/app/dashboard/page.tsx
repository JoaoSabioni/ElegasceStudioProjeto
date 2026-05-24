'use client'

import { Suspense, useState, useEffect, useCallback, useMemo } from 'react'
import Image from 'next/image'
import { useRouter, useSearchParams } from 'next/navigation'
import { getUser, isAuthenticated, clearAuth } from '@/lib/auth'
import {
  getBarberDayBookings,
  getAllBookingsByDate,
  confirmBooking,
  deleteBooking,
  getBarbers,
} from '@/lib/api'
import { useSignalR } from '@/lib/useSignalR'
import NewBookingModal from '../components/NewBookingModal'

type Booking = {
  id: string
  barberName: string
  serviceName: string
  serviceDurationMinutes: number
  bookingDate: string
  bookingTime: string
  status: string
  clientName: string
  clientPhone: string
  clientEmail: string
  createdAt: string
  updatedAt: string | null
}

type Barber   = { id: string; name: string }
type ViewMode = 'list' | 'timeline'

const MESES = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']
const DIAS  = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb']
const HOURS = Array.from({ length: 11 }, (_, i) => i + 9) // 9..19

const STUDIO = {
  name: 'Elegance Studio',
  location: 'Pinhal Novo',
  country: 'Portugal',
  phone: '+351 933 320 269',
  hours: '09:00 - 19:00',
}

const BARBER_PROFILES = [
  {
    name: 'Edi',
    role: 'Fundador',
    phone: '+351 933 320 269',
    instagram: '@edisimoess',
    photo: '/Fotos_edi/edi2.png',
  },
  {
    name: 'Tomas',
    role: 'Colaborador',
    phone: '+351 914 302 079',
    instagram: '@_tomas21_',
    photo: '/Fotos_Tomas/tomas2.png',
  },
  {
    name: 'Abreu',
    role: 'Colaborador',
    phone: '+351 913 388 301',
    instagram: '@abreeubarber',
    photo: '/Fotos_Abreu/abreuPrincipal.jpg',
  },
]

function formatDate(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`
}
function parseDateParam(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(year, month - 1, day)
}
function timeToMins(t: string) {
  const [h, m] = t.split(':').map(Number)
  return h * 60 + m
}
function statusBg(s: string) {
  if (s === 'Pending')   return 'bg-yellow-500/10 border-yellow-500/25 text-yellow-300'
  if (s === 'Confirmed') return 'bg-emerald-500/10 border-emerald-500/25 text-emerald-300'
  return 'bg-zinc-800/50 border-zinc-700/50 text-zinc-500'
}
function statusBadge(s: string) {
  if (s === 'Pending')   return 'bg-yellow-500/10 text-yellow-400 border border-yellow-500/25'
  if (s === 'Confirmed') return 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/25'
  return 'bg-zinc-800 text-zinc-500 border border-zinc-700'
}
function statusLabel(s: string) {
  if (s === 'Pending')   return 'Pendente'
  if (s === 'Confirmed') return 'Confirmada'
  if (s === 'Cancelled') return 'Cancelada'
  return s
}

export default function DashboardPage() {
  return (
    <Suspense fallback={null}>
      <DashboardContent />
    </Suspense>
  )
}

function DashboardContent() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const [user, setUser]                 = useState<ReturnType<typeof getUser>>(null)
  const [barbers, setBarbers]           = useState<Barber[]>([])
  const [selectedDate, setSelectedDate] = useState(new Date())
  const [bookings, setBookings]         = useState<Booking[]>([])
  const [loading, setLoading]           = useState(true)
  const [selectedBooking, setSelectedBooking] = useState<Booking | null>(null)
  const [actionLoading, setActionLoading]     = useState(false)
  const [showNewBooking, setShowNewBooking]   = useState(false)
  const [filterBarber, setFilterBarber]       = useState('all')
  const [viewMode, setViewMode]         = useState<ViewMode>('list')
  const [pendingLinkBookingId, setPendingLinkBookingId] = useState<string | null>(null)

  const selectedDateStr = formatDate(selectedDate)

  useEffect(() => {
    if (!isAuthenticated()) { router.push('/login'); return }
    setUser(getUser())
  }, [router])

  useEffect(() => {
    const bookingId = searchParams.get('bookingId')
    const date = searchParams.get('date')
    const parsedDate = date ? parseDateParam(date) : null

    if (parsedDate) setSelectedDate(parsedDate)
    if (bookingId) setPendingLinkBookingId(bookingId)
  }, [searchParams])

  useEffect(() => {
    if (user?.role === 'Admin') getBarbers().then(setBarbers).catch(() => {})
  }, [user])

  const fetchBookings = useCallback(async () => {
    if (!user) return
    setLoading(true)
    try {
      const data = user.role === 'Admin'
        ? await getAllBookingsByDate(selectedDateStr)
        : user.barberId
          ? await getBarberDayBookings(user.barberId, selectedDateStr)
          : []
      setBookings(data)
    } catch { setBookings([]) }
    finally  { setLoading(false) }
  }, [user, selectedDateStr])

  useEffect(() => { fetchBookings() }, [fetchBookings])

  useEffect(() => {
    if (!pendingLinkBookingId || loading) return

    const booking = bookings.find(b => b.id === pendingLinkBookingId)
    if (!booking) return

    setSelectedBooking(booking)
    setPendingLinkBookingId(null)
  }, [bookings, loading, pendingLinkBookingId])

  // ── SignalR — grupos a subscrever ─────────────────────────────────────────
  const signalRGroups = useMemo(() => {
    if (!user) return []
    if (user.role === 'Admin') {
      // Admin subscreve todos os barbeiros
      return barbers.map(b => `barber-${b.id}`)
    }
    if (user.barberId) return [`barber-${user.barberId}`]
    return []
  }, [user, barbers])

  // ── SignalR — handlers ────────────────────────────────────────────────────
  const signalREvents = useMemo(() => ({
    NewBooking: (booking: unknown) => {
      const b = booking as Booking
      // Só adiciona se for do dia seleccionado
      if (b.bookingDate !== selectedDateStr) return
      setBookings(prev => {
        // Evita duplicados
        if (prev.find(x => x.id === b.id)) return prev
        return [...prev, b].sort((a, z) => a.bookingTime.localeCompare(z.bookingTime))
      })
    },
    BookingUpdated: (booking: unknown) => {
      const b = booking as Booking
      setBookings(prev => prev.map(x => x.id === b.id ? b : x))
      // Actualiza o modal se estiver aberto
      setSelectedBooking(prev => prev?.id === b.id ? b : prev)
    },
    BookingDeleted: (bookingId: unknown) => {
      const id = String(bookingId)
      setBookings(prev => prev.filter(x => x.id !== id))
      setSelectedBooking(prev => prev?.id === id ? null : prev)
    },
  }), [selectedDateStr])

  useSignalR(signalREvents, signalRGroups, !!user)

  // ── Acções ────────────────────────────────────────────────────────────────
  const handleConfirm = async (id: string) => {
    setActionLoading(true)
    try {
      await confirmBooking(id)
      // O SignalR actualiza a lista — não precisamos de refetch
      setSelectedBooking(null)
    } catch {} finally { setActionLoading(false) }
  }

  const handleDelete = async (id: string) => {
    setActionLoading(true)
    try {
      await deleteBooking(id)
      setSelectedBooking(null)
      setBookings(p => p.filter(b => b.id !== id))
    } catch {} finally { setActionLoading(false) }
  }

  // ── Semana ────────────────────────────────────────────────────────────────
  const weekDays = Array.from({ length: 7 }, (_, i) => {
    const d   = new Date(selectedDate)
    const off = d.getDay() === 0 ? -6 : 1 - d.getDay()
    d.setDate(d.getDate() + off + i)
    return new Date(d)
  })
  const prevWeek = () => setSelectedDate(d => { const n = new Date(d); n.setDate(n.getDate()-7); return n })
  const nextWeek = () => setSelectedDate(d => { const n = new Date(d); n.setDate(n.getDate()+7); return n })
  const isToday    = (d: Date) => formatDate(d) === formatDate(new Date())
  const isSelected = (d: Date) => formatDate(d) === formatDate(selectedDate)

  const visibleBookings = bookings
    .filter(b => b.status !== 'Cancelled' && (filterBarber === 'all' || b.barberName === filterBarber))
    .sort((a, b) => a.bookingTime.localeCompare(b.bookingTime))

  const stats = {
    total:     visibleBookings.length,
    confirmed: visibleBookings.filter(b => b.status === 'Confirmed').length,
    pending:   visibleBookings.filter(b => b.status === 'Pending').length,
  }

  const isAdmin    = user?.role === 'Admin'
  const barberName = isAdmin
    ? 'Administrador'
    : barbers.find(b => b.id === user?.barberId)?.name ?? 'Barbeiro'
  const activeProfile = BARBER_PROFILES.find(profile => profile.name === barberName)
  const selectedProfile = isAdmin && filterBarber !== 'all'
    ? BARBER_PROFILES.find(profile => profile.name === filterBarber)
    : activeProfile

  const HOUR_HEIGHT    = 72
  const TIMELINE_START = 9 * 60

  if (!user) return null

  return (
    <div className="relative min-h-screen overflow-hidden bg-[#070604] text-white">
      <div aria-hidden="true" className="fixed inset-0 pointer-events-none">
        <div className="absolute inset-y-0 right-0 w-full md:w-[58%] opacity-[0.16]">
          <Image src="/Fotos_loja/loja3.png" alt="" fill className="object-cover object-center grayscale" priority />
        </div>
        <div className="absolute inset-0 bg-[linear-gradient(90deg,#050505_0%,rgba(5,5,5,0.94)_42%,rgba(5,5,5,0.78)_100%)]" />
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_48%_0%,rgba(255,255,255,0.08),transparent_32%)]" />
      </div>

      {/* ── Header ── */}
      <header className="fixed top-0 left-0 right-0 z-50 border-b border-white/10 bg-[#070604]/88 backdrop-blur-xl">
        <div className="max-w-7xl mx-auto flex items-center justify-between px-4 md:px-6 py-3">
          <div className="flex min-w-0 items-center gap-3">
            <div className="relative h-10 w-10 shrink-0 overflow-hidden border border-white/12 bg-white/5">
              <Image src="/logo.png" alt="Elegance Studio" fill className="object-contain p-1.5" priority />
            </div>
            <div className="min-w-0">
            <p className="text-[10px] text-zinc-500 uppercase tracking-[0.28em]">{STUDIO.name}</p>
            <h1 className="text-[15px] md:text-[18px] font-semibold tracking-wide text-white">
              Agenda de {barberName}
            </h1>
            <p className="text-[11px] text-zinc-500 mt-0.5">
              {selectedDate.getDate()} {MESES[selectedDate.getMonth()]} {selectedDate.getFullYear()}
              {isAdmin && <span className="ml-2 text-zinc-700">Admin</span>}
            </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <div className="hidden sm:flex border border-white/15 overflow-hidden">
              <button
                onClick={() => setViewMode('list')}
                className={`px-3 py-2 text-[10px] font-semibold uppercase tracking-[0.2em] transition-all ${viewMode === 'list' ? 'bg-white text-black' : 'text-zinc-500 hover:text-white'}`}
              >Lista</button>
              <button
                onClick={() => setViewMode('timeline')}
                className={`px-3 py-2 text-[10px] font-semibold uppercase tracking-[0.2em] border-l border-white/15 transition-all ${viewMode === 'timeline' ? 'bg-white text-black' : 'text-zinc-500 hover:text-white'}`}
              >Timeline</button>
            </div>
            {!isAdmin && (
              <button
                onClick={() => setShowNewBooking(true)}
                className="text-[10px] font-bold tracking-[0.22em] uppercase text-black bg-white px-3 md:px-4 py-2.5 hover:bg-zinc-200 transition-all"
              >
                <span className="hidden md:inline">Nova marcação</span>
                <span className="md:hidden">+</span>
              </button>
            )}
            <button
              onClick={() => { clearAuth(); router.push('/login') }}
              className="text-[10px] font-medium text-zinc-600 uppercase tracking-wider hover:text-zinc-300 transition-colors px-2 py-2"
            >Sair</button>
          </div>
        </div>
      </header>

      <main className="relative z-10 pt-[72px] pb-16 px-4 md:px-6">
        <div className="max-w-7xl mx-auto">

          {/* ── Navegação semana ── */}
          <div className="flex items-center justify-between py-4">
            <button onClick={prevWeek} className="w-9 h-9 flex items-center justify-center border border-white/15 hover:border-white/40 text-zinc-400 hover:text-white transition-all text-xl">‹</button>
            <div className="flex items-center gap-3">
              <span className="text-[13px] font-semibold tracking-widest uppercase text-zinc-200">
                {MESES[selectedDate.getMonth()]} {selectedDate.getFullYear()}
              </span>
              <button
                onClick={() => setSelectedDate(new Date())}
                className="text-[10px] font-medium tracking-widest uppercase text-zinc-600 hover:text-white border border-white/12 hover:border-white/30 px-2.5 py-1 transition-all"
              >Hoje</button>
            </div>
            <button onClick={nextWeek} className="w-9 h-9 flex items-center justify-center border border-white/15 hover:border-white/40 text-zinc-400 hover:text-white transition-all text-xl">›</button>
          </div>

          {/* ── Dias da semana ── */}
          <div className="grid grid-cols-7 gap-1 mb-5">
            {weekDays.map(d => (
              <button
                key={formatDate(d)}
                onClick={() => setSelectedDate(new Date(d))}
                className={`py-2.5 md:py-3 border text-center transition-all duration-150 ${
                  isSelected(d)
                    ? 'border-white bg-white text-black'
                    : isToday(d)
                    ? 'border-white/30 text-white bg-white/5'
                    : 'border-white/12 text-zinc-500 hover:border-white/30 hover:text-zinc-300'
                }`}
              >
                <span className="block text-[9px] md:text-[10px] font-semibold tracking-widest uppercase">{DIAS[d.getDay()].slice(0,3)}</span>
                <span className="block text-[15px] md:text-[18px] font-bold mt-0.5 tabular-nums">{d.getDate()}</span>
              </button>
            ))}
          </div>

          {/* ── Stats ── */}
          <section className="mb-5 grid gap-3 lg:grid-cols-[1.35fr_0.65fr]">
            <div className="border border-white/10 bg-[#0d0b08]/72 p-4 md:p-5 backdrop-blur-md">
              <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <p className="text-[10px] font-semibold uppercase tracking-[0.26em] text-zinc-500">
                    {STUDIO.location} · {STUDIO.country}
                  </p>
                  <h2 className="mt-2 text-[24px] font-semibold leading-tight text-white md:text-[32px]">
                    Agenda operacional da {STUDIO.name}
                  </h2>
                </div>
                <div className="grid grid-cols-2 gap-2 text-right md:min-w-[320px]">
                  <div className="border border-white/8 bg-white/[0.03] px-3 py-2.5">
                    <p className="text-[9px] uppercase tracking-[0.24em] text-zinc-600">Horario</p>
                    <p className="mt-1 text-[13px] font-semibold text-zinc-200">{STUDIO.hours}</p>
                  </div>
                  <div className="border border-white/8 bg-white/[0.03] px-3 py-2.5">
                    <p className="text-[9px] uppercase tracking-[0.24em] text-zinc-600">Telefone</p>
                    <p className="mt-1 text-[13px] font-semibold text-zinc-200">{STUDIO.phone}</p>
                  </div>
                </div>
              </div>
            </div>
            <div className="relative min-h-[150px] overflow-hidden border border-white/10 bg-black/20">
              <Image src="/Fotos_loja/loja4.png" alt={STUDIO.name} fill className="object-cover opacity-70" sizes="(max-width: 1024px) 100vw, 380px" />
              <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent" />
              <div className="absolute bottom-4 left-4 right-4">
                <p className="text-[10px] uppercase tracking-[0.28em] text-zinc-400">
                  {selectedProfile ? selectedProfile.role : 'Equipa'}
                </p>
                <p className="mt-1 text-[22px] font-semibold">{selectedProfile?.name ?? 'Edi · Tomas · Abreu'}</p>
              </div>
            </div>
          </section>

          <div className="grid grid-cols-3 gap-2 md:gap-3 mb-5">
            {[
              { label: 'Total',       value: stats.total,     color: 'text-white' },
              { label: 'Confirmadas', value: stats.confirmed, color: 'text-emerald-400' },
              { label: 'Pendentes',   value: stats.pending,   color: 'text-yellow-400' },
            ].map(s => (
              <div key={s.label} className="border border-white/10 px-3 md:px-5 py-4 bg-white/[0.025]">
                <p className="text-[9px] md:text-[10px] font-semibold text-zinc-600 uppercase tracking-[0.24em] mb-3">{s.label}</p>
                <p className={`text-[30px] md:text-[36px] font-semibold tabular-nums leading-none ${s.color}`}>{s.value}</p>
              </div>
            ))}
          </div>

          {isAdmin && (
            <div className="mb-5">
              <div className="mb-2 flex items-center justify-between gap-3">
                <p className="text-[9px] font-semibold uppercase tracking-[0.32em] text-zinc-600">Filtrar por barbeiro</p>
                {filterBarber !== 'all' && (
                  <button
                    onClick={() => setFilterBarber('all')}
                    className="text-[9px] font-bold uppercase tracking-[0.24em] text-zinc-500 hover:text-white"
                  >
                    Ver todos
                  </button>
                )}
              </div>

              <div className="grid gap-2 md:grid-cols-3">
                {BARBER_PROFILES.map(profile => (
                  <button
                    key={profile.name}
                    type="button"
                    onClick={() => setFilterBarber(profile.name)}
                    className={`group flex items-center gap-3 border p-3 text-left backdrop-blur-sm transition-all ${
                      filterBarber === profile.name
                        ? 'border-white/60 bg-white text-black'
                        : 'border-white/10 bg-black/22 hover:border-white/35 hover:bg-white/[0.06]'
                    }`}
                  >
                    <div className="relative h-12 w-12 shrink-0 overflow-hidden border border-white/10">
                      <Image src={profile.photo} alt={profile.name} fill className="object-cover" sizes="48px" />
                    </div>
                    <div className="min-w-0">
                      <p className={`truncate text-[13px] font-semibold ${filterBarber === profile.name ? 'text-black' : 'text-zinc-100'}`}>{profile.name}</p>
                      <p className="truncate text-[10px] uppercase tracking-[0.18em] text-zinc-500">{profile.role} · {profile.instagram}</p>
                      <p className={`mt-0.5 truncate text-[11px] ${filterBarber === profile.name ? 'text-zinc-700' : 'text-zinc-400'}`}>{profile.phone}</p>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="flex items-center gap-3 mb-4">
            <div className="h-px flex-1 bg-white/8" />
            <p className="text-[10px] font-semibold tracking-[0.4em] text-zinc-600 uppercase shrink-0">
              {selectedDate.getDate()} {MESES[selectedDate.getMonth()]} {selectedDate.getFullYear()} · {DIAS[selectedDate.getDay()]}
            </p>
            <div className="h-px flex-1 bg-white/8" />
          </div>

          {/* ══ LISTA ══ */}
          {viewMode === 'list' && (
            loading ? (
              <div className="space-y-2">
                {[1,2,3].map(i => <div key={i} className="h-20 border border-white/5 bg-zinc-900/20 animate-pulse rounded" />)}
              </div>
            ) : visibleBookings.length === 0 ? (
              <div className="border border-white/10 px-6 py-16 text-center bg-zinc-900/10">
                <p className="text-[12px] font-semibold text-zinc-700 uppercase tracking-widest mb-5">Sem marcações</p>
                {!isAdmin && (
                  <button
                    onClick={() => setShowNewBooking(true)}
                    className="text-[11px] font-semibold tracking-widest text-zinc-500 uppercase hover:text-white border border-white/12 hover:border-white/30 px-6 py-3 transition-all"
                  >+ Adicionar marcação</button>
                )}
              </div>
            ) : (
              <div className="flex flex-col gap-1.5">
                {visibleBookings.map(b => (
                  <button
                    key={b.id}
                    onClick={() => setSelectedBooking(b)}
                    className={`flex items-center justify-between px-4 md:px-5 py-4 border transition-all duration-150 text-left hover:brightness-110 rounded ${statusBg(b.status)}`}
                  >
                    <div className="flex items-center gap-3 md:gap-5 min-w-0">
                      <span className="text-[16px] md:text-[20px] font-bold w-14 md:w-16 shrink-0 tabular-nums">
                        {b.bookingTime.slice(0,5)}
                      </span>
                      <div className="min-w-0">
                        <p className="text-[13px] md:text-[15px] font-semibold tracking-wide leading-tight truncate">
                          {b.clientName}
                        </p>
                        <p className="text-[10px] md:text-[11px] font-medium text-zinc-500 mt-0.5 truncate">
                          {b.serviceName}
                          {isAdmin && b.barberName && <span className="ml-1.5 text-zinc-600">· {b.barberName}</span>}
                        </p>
                      </div>
                    </div>
                    <span className={`text-[8px] md:text-[9px] font-bold tracking-[0.3em] uppercase px-2 py-1 shrink-0 ml-2 rounded ${statusBadge(b.status)}`}>
                      {statusLabel(b.status)}
                    </span>
                  </button>
                ))}
              </div>
            )
          )}

          {/* ══ TIMELINE ══ */}
          {viewMode === 'timeline' && (
            <div className="border border-white/12 bg-zinc-900/20 overflow-hidden rounded">
              {loading ? (
                <div className="h-64 animate-pulse bg-zinc-900/40" />
              ) : (
                <div className="flex overflow-x-auto">
                  <div className="w-12 md:w-16 shrink-0 border-r border-white/10">
                    {HOURS.map(h => (
                      <div key={h} className="flex items-start justify-end pr-2 pt-1.5" style={{ height: HOUR_HEIGHT }}>
                        <span className="text-[9px] md:text-[10px] font-mono font-semibold text-zinc-700 tabular-nums">
                          {String(h).padStart(2,'0')}h
                        </span>
                      </div>
                    ))}
                  </div>

                  <div className="flex-1 relative min-w-0">
                    {HOURS.map(h => (
                      <div key={h} className="border-t border-white/6" style={{ height: HOUR_HEIGHT }}>
                        <div className="border-t border-white/[0.03]" style={{ marginTop: HOUR_HEIGHT / 2 }} />
                      </div>
                    ))}

                    {isToday(selectedDate) && (() => {
                      const now  = new Date()
                      const top  = ((now.getHours() * 60 + now.getMinutes() - TIMELINE_START) / 60) * HOUR_HEIGHT
                      if (top < 0 || top > HOURS.length * HOUR_HEIGHT) return null
                      return (
                        <div className="absolute left-0 right-0 flex items-center z-10 pointer-events-none" style={{ top }}>
                          <div className="w-2 h-2 rounded-full bg-red-500 -ml-1 shrink-0" />
                          <div className="flex-1 border-t border-red-500/60" />
                        </div>
                      )
                    })()}

                    {visibleBookings.map(b => {
                      const top    = ((timeToMins(b.bookingTime) - TIMELINE_START) / 60) * HOUR_HEIGHT
                      const height = Math.max((b.serviceDurationMinutes / 60) * HOUR_HEIGHT, 32)
                      return (
                        <button
                          key={b.id}
                          onClick={() => setSelectedBooking(b)}
                          className={`absolute left-1.5 right-1.5 rounded border text-left px-2.5 py-1.5 transition-all hover:brightness-110 hover:z-20 ${statusBg(b.status)}`}
                          style={{ top: top + 1, height: height - 2 }}
                        >
                          <p className="text-[12px] md:text-[13px] font-bold leading-tight truncate">{b.clientName}</p>
                          {height > 40 && (
                            <p className="text-[9px] md:text-[10px] font-medium opacity-60 truncate mt-0.5">
                              {b.bookingTime.slice(0,5)} · {b.serviceName}
                              {isAdmin && ` · ${b.barberName}`}
                            </p>
                          )}
                        </button>
                      )
                    })}

                    {!loading && visibleBookings.length === 0 && (
                      <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                        <p className="text-[11px] font-semibold text-zinc-700 uppercase tracking-widest">Sem marcações</p>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

        </div>
      </main>

      {/* ── Modal detalhe ── */}
      {selectedBooking && (
        <div
          className="fixed inset-0 z-[200] flex items-end md:items-center justify-center bg-black/85 backdrop-blur-sm"
          onClick={() => setSelectedBooking(null)}
        >
          <div
            className="bg-zinc-950 border border-white/15 w-full md:max-w-md p-6 md:p-8 shadow-2xl md:rounded"
            onClick={e => e.stopPropagation()}
          >
            <div className="flex items-center justify-between mb-6">
              <p className="text-[10px] font-bold tracking-[0.5em] text-zinc-500 uppercase">Detalhe</p>
              <button onClick={() => setSelectedBooking(null)} className="text-zinc-600 hover:text-white transition-colors w-8 h-8 flex items-center justify-center">✕</button>
            </div>

            <div className={`inline-flex items-center px-3 py-1 mb-5 text-[9px] font-bold tracking-[0.3em] uppercase rounded ${statusBadge(selectedBooking.status)}`}>
              {statusLabel(selectedBooking.status)}
            </div>

            <div className="space-y-1 mb-7">
              {[
                { label: 'Cliente',  value: selectedBooking.clientName },
                { label: 'Telefone', value: selectedBooking.clientPhone },
                { label: 'Email',    value: selectedBooking.clientEmail },
                { label: 'Serviço',  value: selectedBooking.serviceName },
                { label: 'Data',     value: selectedBooking.bookingDate },
                { label: 'Hora',     value: selectedBooking.bookingTime.slice(0,5) },
                ...(isAdmin ? [{ label: 'Barbeiro', value: selectedBooking.barberName }] : []),
              ].map(row => (
                <div key={row.label} className="flex items-center justify-between py-2.5 border-b border-white/6">
                  <span className="text-[10px] font-semibold text-zinc-600 uppercase tracking-wider">{row.label}</span>
                  <span className="text-[13px] font-medium text-zinc-200">{row.value}</span>
                </div>
              ))}
            </div>

            <div className="flex gap-2">
              {!isAdmin && selectedBooking.status === 'Pending' && (
                <button
                  onClick={() => handleConfirm(selectedBooking.id)}
                  disabled={actionLoading}
                  className="flex-1 py-3.5 border border-emerald-500/30 text-emerald-400 text-[11px] font-bold tracking-widest uppercase hover:bg-emerald-500/10 transition-all disabled:opacity-40 rounded"
                >{actionLoading ? '...' : 'Confirmar'}</button>
              )}
              <button
                onClick={() => handleDelete(selectedBooking.id)}
                disabled={actionLoading}
                className="flex-1 py-3.5 border border-red-500/25 text-red-400 text-[11px] font-bold tracking-widest uppercase hover:bg-red-500/8 transition-all disabled:opacity-40 rounded"
              >{actionLoading ? '...' : 'Apagar'}</button>
            </div>
          </div>
        </div>
      )}

      {showNewBooking && !isAdmin && (
        <NewBookingModal
          onClose={() => setShowNewBooking(false)}
          onCreated={() => { setShowNewBooking(false) }}
        />
      )}
    </div>
  )
}
