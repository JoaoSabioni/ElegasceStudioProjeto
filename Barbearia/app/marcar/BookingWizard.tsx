'use client'

import { useState, useEffect, useMemo, useRef } from 'react'
import Image from 'next/image'
import Link from 'next/link'
import Navbar from '../components/Navbar'

// ─── Config ───────────────────────────────────────────────────────────────────
const LOCAL_API = 'http://localhost:5134'
const PRODUCTION_API = 'https://elegascestudioprojeto-production.up.railway.app'
const API = process.env.NEXT_PUBLIC_API_URL ||
  (typeof window !== 'undefined' && window.location.hostname !== 'localhost'
    ? PRODUCTION_API
    : LOCAL_API)

// Cache de disponibilidade — evita re-fetches para o mesmo barbeiro/data/serviços
const availabilityCache = new Map<string, string[]>()

// ─── Types ────────────────────────────────────────────────────────────────────
type Barber  = { id: string; name: string; specialty: string; phone: string }
type Service = { id: string; name: string; price: number; durationMinutes: number }
type Step    = 1 | 2 | 3 | 4 | 5 | 6

// ─── Fotos / roles dos barbeiros ──────────────────────────────────────────────
const BARBER_PHOTOS: Record<string, string> = {
  'Edi':   '/Fotos_edi/edi2.png',
  'Tomas': '/Fotos_Tomas/tomas2.png',
  'Abreu': '/Fotos_Abreu/abreuPrincipal.jpg',
}
const BARBER_ROLES: Record<string, string> = {
  'Edi':   'Fundador',
  'Tomas': 'Colaborador',
  'Abreu': 'Colaborador',
}

// ─── Helpers ──────────────────────────────────────────────────────────────────
const formatDateApi = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

const DIAS_SEMANA = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']
const MESES = ['Janeiro','Fevereiro','Março','Abril','Maio','Junho',
               'Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']

export default function BookingWizard() {
  const [step,       setStep]       = useState<Step>(1)
  const [barbers,    setBarbers]    = useState<Barber[]>([])
  const [services,   setServices]   = useState<Service[]>([])
  const [slots,      setSlots]      = useState<string[]>([])
  const [loading,    setLoading]    = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error,      setError]      = useState('')
  const [success,    setSuccess]    = useState(false)

  const [selectedBarber,   setSelectedBarber]   = useState<Barber | null>(null)
  const [selectedServices, setSelectedServices] = useState<Service[]>([])
  const [selectedDate,     setSelectedDate]     = useState<Date | null>(null)
  const [selectedSlot,     setSelectedSlot]     = useState<string | null>(null)
  const [clientName,       setClientName]       = useState('')
  const [clientEmail,      setClientEmail]      = useState('')
  const [phoneDigits,      setPhoneDigits]      = useState('')

  const [calendarMonth, setCalendarMonth] = useState(new Date().getMonth())
  const [calendarYear,  setCalendarYear]  = useState(new Date().getFullYear())

  const fetchAbortRef = useRef<AbortController | null>(null)

  // ─── Derived ────────────────────────────────────────────────────────────────
  const totalPrice = useMemo(
    () => selectedServices.reduce((acc, s) => acc + s.price, 0),
    [selectedServices]
  )
  const totalDuration = useMemo(
    () => selectedServices.reduce((acc, s) => acc + s.durationMinutes, 0),
    [selectedServices]
  )
  const clientPhone = '+351' + phoneDigits
  const hasValidEmail = /^[^\s@]+@[^\s@]+$/.test(clientEmail.trim())

  // ─── Fetch barbers + services ────────────────────────────────────────────────
  useEffect(() => {
    fetch(`${API}/api/barbers`)
      .then(r => r.json()).then(setBarbers)
      .catch(() => setError('Erro ao carregar barbeiros.'))
  }, [])

  useEffect(() => {
    fetch(`${API}/api/services`)
      .then(r => r.json()).then(setServices)
      .catch(() => setError('Erro ao carregar serviços.'))
  }, [])

  // ─── Fetch slots com cache + abort ──────────────────────────────────────────
  useEffect(() => {
    if (!selectedBarber || selectedServices.length === 0 || !selectedDate) return

    const dateStr  = formatDateApi(selectedDate)
    const cacheKey = `${selectedBarber.id}|${selectedServices.map(s => s.id).sort().join(',')}|${dateStr}`

    if (availabilityCache.has(cacheKey)) {
      setSlots(availabilityCache.get(cacheKey)!)
      setSelectedSlot(null)
      return
    }

    fetchAbortRef.current?.abort()
    const controller = new AbortController()
    fetchAbortRef.current = controller

    setLoading(true)
    setSlots([])
    setSelectedSlot(null)

    const params = new URLSearchParams({
      barberId: selectedBarber.id,
      date: dateStr,
    })
    selectedServices.forEach(service => params.append('serviceIds', service.id))

    fetch(`${API}/api/availability?${params}`, { signal: controller.signal })
      .then(r => r.json())
      .then(data => {
        const available = data.availableSlots || []
        availabilityCache.set(cacheKey, available)
        setSlots(available)
        setLoading(false)
      })
      .catch(err => {
        if (err.name === 'AbortError') return
        setError('Erro ao carregar horários.')
        setLoading(false)
      })
  }, [selectedBarber, selectedServices, selectedDate, totalDuration])

  // ─── Toggle serviço ──────────────────────────────────────────────────────────
  const toggleService = (service: Service) => {
    setSelectedServices(prev => {
      const exists = prev.find(s => s.id === service.id)
      return exists ? prev.filter(s => s.id !== service.id) : [...prev, service]
    })
    setSlots([])
    setSelectedSlot(null)
  }

  // ─── Navegar para step anterior via progress squares ─────────────────────────
  const goToStep = (target: number) => {
    if (target < step && target >= 1) setStep(target as Step)
  }

  // ─── Submit ──────────────────────────────────────────────────────────────────
  const handleSubmit = async () => {
    if (!selectedBarber || selectedServices.length === 0 ||
        !selectedDate || !selectedSlot || !clientName.trim() ||
        phoneDigits.length !== 9 || !hasValidEmail) return

    setSubmitting(true)
    setError('')

    try {
      const res = await fetch(`${API}/api/bookings`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          barberId:    selectedBarber.id,
          serviceIds:  selectedServices.map(s => s.id),
          bookingDate: formatDateApi(selectedDate),
          bookingTime: selectedSlot,
          clientName:  clientName.trim(),
          clientPhone,
          clientEmail: clientEmail.trim().toLowerCase(),
        }),
      })

      if (res.status === 201) {
        // Invalida cache para este barbeiro/data — slot acabou de ser ocupado
        const dateStr  = formatDateApi(selectedDate)
        const cacheKey = `${selectedBarber.id}|${selectedServices.map(s => s.id).sort().join(',')}|${dateStr}`
        availabilityCache.delete(cacheKey)

        setSuccess(true)
        setStep(6)
      } else if (res.status === 409) {
        setError('Este horário já foi reservado. Escolhe outro.')
        setSlots(prev => prev.filter(s => s !== selectedSlot))
        setSelectedSlot(null)
        setStep(4)
      } else {
        setError('Erro ao criar marcação. Tente novamente.')
      }
    } catch {
      setError('Erro de ligação ao servidor.')
    } finally {
      setSubmitting(false)
    }
  }

  // ─── Calendar helpers ────────────────────────────────────────────────────────
  const today = new Date(); today.setHours(0, 0, 0, 0)
  const maxDate = new Date(); maxDate.setDate(maxDate.getDate() + 60)

  const daysInMonth    = new Date(calendarYear, calendarMonth + 1, 0).getDate()
  const firstDayOfWeek = new Date(calendarYear, calendarMonth, 1).getDay()

  const canGoBack    = calendarYear > today.getFullYear() ||
    (calendarYear === today.getFullYear() && calendarMonth > today.getMonth())
  const canGoForward = new Date(calendarYear, calendarMonth + 1, 1) <= maxDate

  const isDateSelectable = (day: number) => {
    const d = new Date(calendarYear, calendarMonth, day); d.setHours(0, 0, 0, 0)
    return d >= today && d <= maxDate
  }
  const isDateSelected = (day: number) =>
    selectedDate?.getFullYear() === calendarYear &&
    selectedDate?.getMonth()    === calendarMonth &&
    selectedDate?.getDate()     === day

  const STEPS = [
    { num: 1, label: 'Barbeiro' },
    { num: 2, label: 'Serviços' },
    { num: 3, label: 'Data'     },
    { num: 4, label: 'Hora'     },
    { num: 5, label: 'Dados'    },
    { num: 6, label: 'Confirma' },
  ]

  return (
    <div className="bg-black text-white font-sans selection:bg-white selection:text-black min-h-screen overflow-x-hidden">
      <Navbar activePage="marcar" />

      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <section className="pt-36 md:pt-44 pb-12 md:pb-16 px-6 md:px-8 border-b border-white/5">
        <div className="max-w-3xl mx-auto">
          <div className="flex items-center gap-4 text-zinc-400 text-[11px] tracking-[0.5em] uppercase mb-8">
            <span className="w-12 h-px bg-zinc-500" /> MARCAÇÃO ONLINE
          </div>
          <h1 className="font-serif text-[clamp(3rem,10vw,90px)] leading-[0.85] font-medium uppercase tracking-tighter">MARCAR</h1>
        </div>
      </section>

      {/* ── Progress ─────────────────────────────────────────────────────────── */}
      <div className="px-6 md:px-8 py-8 border-b border-white/5">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          {STEPS.map((s, i) => (
            <div key={s.num} className="flex items-center">
              <div className="flex flex-col items-center">
                <button
                  onClick={() => goToStep(s.num)}
                  disabled={s.num >= step}
                  className={`w-8 h-8 flex items-center justify-center border text-[10px] font-semibold transition-all duration-300 ${
                    step > s.num
                      ? 'border-white bg-white text-black hover:bg-zinc-200 cursor-pointer'
                      : step === s.num
                      ? 'border-white bg-white text-black cursor-default'
                      : 'border-white/20 text-zinc-600 cursor-default'
                  }`}
                >
                  {step > s.num ? '✓' : s.num}
                </button>
                <span className={`text-[8px] tracking-[0.2em] uppercase mt-2 hidden sm:block transition-colors ${
                  step >= s.num ? 'text-zinc-300' : 'text-zinc-700'
                }`}>{s.label}</span>
              </div>
              {i < STEPS.length - 1 && (
                <div className={`w-6 sm:w-12 md:w-20 h-px mx-1 sm:mx-2 transition-colors ${
                  step > s.num ? 'bg-white/40' : 'bg-white/10'
                }`} />
              )}
            </div>
          ))}
        </div>
      </div>

      {/* ── Error ───────────────────────────────────────────────────────────── */}
      {error && (
        <div className="px-6 md:px-8 pt-6">
          <div className="max-w-3xl mx-auto border border-red-500/30 bg-red-500/5 px-6 py-4 flex items-center justify-between">
            <span className="text-[11px] tracking-wider text-red-400">{error}</span>
            <button onClick={() => setError('')} className="text-red-400 hover:text-white text-[16px] ml-4">×</button>
          </div>
        </div>
      )}

      {/* ── Content ─────────────────────────────────────────────────────────── */}
      <section className="px-6 md:px-8 py-12 md:py-16">
        <div className="max-w-3xl mx-auto">

          {/* ── STEP 1: Barbeiro ──────────────────────────────────────────── */}
          {step === 1 && (
            <div>
              <p className="text-[10px] tracking-[0.8em] text-zinc-300 uppercase mb-8">01 · Escolhe o teu barbeiro</p>
              <div className="flex flex-col gap-3">
                {barbers.map(b => (
                  <button
                    key={b.id}
                    onClick={() => { setSelectedBarber(b); setStep(2) }}
                    className={`flex items-center gap-5 px-6 py-5 border transition-all duration-300 group ${
                      selectedBarber?.id === b.id ? 'border-white bg-white/5' : 'border-white/10 hover:border-white/30'
                    }`}
                  >
                    <div className="relative w-14 h-14 overflow-hidden flex-shrink-0">
                      <Image
                        src={BARBER_PHOTOS[b.name] || '/logo.png'}
                        alt={b.name} fill
                        className="object-cover grayscale group-hover:grayscale-0 transition-all duration-500"
                      />
                    </div>
                    <div className="text-left">
                      <p className="text-[14px] tracking-[0.15em] uppercase font-semibold group-hover:text-white transition-colors">{b.name}</p>
                      <p className="text-[10px] tracking-[0.4em] text-zinc-400 uppercase">{BARBER_ROLES[b.name] || 'Barbeiro'}</p>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* ── STEP 2: Serviços ──────────────────────────────────────────── */}
          {step === 2 && (
            <div>
              <p className="text-[10px] tracking-[0.8em] text-zinc-300 uppercase mb-2">02 · Escolhe os serviços</p>
              <p className="text-[10px] text-zinc-600 tracking-wider mb-8">Podes selecionar mais do que um serviço.</p>

              <div className="flex flex-col gap-2 mb-6">
                {services.map(s => {
                  const isSelected = !!selectedServices.find(sel => sel.id === s.id)
                  return (
                    <button
                      key={s.id}
                      onClick={() => toggleService(s)}
                      className={`flex items-center justify-between px-6 py-5 border text-left transition-all duration-300 group ${
                        isSelected ? 'border-white bg-white/5' : 'border-white/10 hover:border-white/30'
                      }`}
                    >
                      <div className="flex items-center gap-4">
                        <div className={`w-4 h-4 border flex items-center justify-center flex-shrink-0 transition-all ${
                          isSelected ? 'border-white bg-white' : 'border-white/25'
                        }`}>
                          {isSelected && (
                            <svg width="10" height="10" viewBox="0 0 12 12" fill="none">
                              <path d="M2 6l3 3 5-5" stroke="black" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                            </svg>
                          )}
                        </div>
                        <p className="text-[13px] tracking-[0.15em] uppercase font-semibold group-hover:text-white transition-colors">{s.name}</p>
                      </div>
                      <span className="font-serif text-[28px] text-zinc-400 group-hover:text-white transition-colors">{s.price}€</span>
                    </button>
                  )
                })}
              </div>

              {selectedServices.length > 0 && (
                <div className="border border-white/10 bg-white/[0.02] px-6 py-4 mb-6 flex items-center justify-between">
                  <p className="text-[9px] tracking-[0.5em] text-zinc-500 uppercase">
                    {selectedServices.length} {selectedServices.length === 1 ? 'serviço' : 'serviços'}
                  </p>
                  <span className="font-serif text-[32px] text-white">{totalPrice}€</span>
                </div>
              )}

              <button
                onClick={() => setStep(3)}
                disabled={selectedServices.length === 0}
                className={`w-full flex items-center justify-between px-8 py-5 border transition-all duration-300 ${
                  selectedServices.length > 0
                    ? 'border-white/20 hover:bg-white hover:text-black cursor-pointer'
                    : 'border-white/5 text-zinc-700 cursor-not-allowed'
                }`}
              >
                <span className="text-[11px] tracking-[0.3em] uppercase font-semibold">Continuar</span>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M5 12h14M12 5l7 7-7 7"/>
                </svg>
              </button>

              <button onClick={() => setStep(1)} className="mt-6 text-[10px] tracking-[0.3em] text-zinc-500 uppercase hover:text-white flex items-center gap-3 transition-colors">
                <span className="w-6 h-px bg-zinc-700"/> Voltar
              </button>
            </div>
          )}

          {/* ── STEP 3: Data ──────────────────────────────────────────────── */}
          {step === 3 && (
            <div>
              <p className="text-[10px] tracking-[0.8em] text-zinc-300 uppercase mb-8">03 · Escolhe a data</p>
              <div className="flex items-center justify-between mb-6">
                <button
                  onClick={() => { if (calendarMonth === 0) { setCalendarMonth(11); setCalendarYear(y => y - 1) } else setCalendarMonth(m => m - 1) }}
                  disabled={!canGoBack}
                  className={`w-10 h-10 flex items-center justify-center border transition-all ${canGoBack ? 'border-white/20 hover:border-white/50 text-zinc-400 hover:text-white' : 'border-white/5 text-zinc-800 cursor-not-allowed'}`}
                >‹</button>
                <span className="text-[12px] tracking-[0.4em] uppercase text-zinc-300">{MESES[calendarMonth]} {calendarYear}</span>
                <button
                  onClick={() => { if (calendarMonth === 11) { setCalendarMonth(0); setCalendarYear(y => y + 1) } else setCalendarMonth(m => m + 1) }}
                  disabled={!canGoForward}
                  className={`w-10 h-10 flex items-center justify-center border transition-all ${canGoForward ? 'border-white/20 hover:border-white/50 text-zinc-400 hover:text-white' : 'border-white/5 text-zinc-800 cursor-not-allowed'}`}
                >›</button>
              </div>
              <div className="grid grid-cols-7 gap-1 mb-2">
                {DIAS_SEMANA.map(d => (
                  <div key={d} className="text-center text-[9px] tracking-[0.2em] text-zinc-600 uppercase py-2">{d}</div>
                ))}
              </div>
              <div className="grid grid-cols-7 gap-1">
                {Array.from({ length: firstDayOfWeek }, (_, i) => <div key={`e-${i}`}/>)}
                {Array.from({ length: daysInMonth }, (_, i) => {
                  const day        = i + 1
                  const selectable = isDateSelectable(day)
                  const selected   = isDateSelected(day)
                  return (
                    <button key={day} disabled={!selectable}
                      onClick={() => { setSelectedDate(new Date(calendarYear, calendarMonth, day)); setStep(4) }}
                      className={`py-3 text-[12px] border transition-all duration-200 ${
                        selected     ? 'border-white bg-white text-black font-semibold'
                        : selectable ? 'border-white/10 text-zinc-300 hover:border-white/40 hover:text-white'
                                     : 'border-transparent text-zinc-800 cursor-not-allowed'
                      }`}>{day}</button>
                  )
                })}
              </div>
              <button onClick={() => setStep(2)} className="mt-8 text-[10px] tracking-[0.3em] text-zinc-500 uppercase hover:text-white flex items-center gap-3 transition-colors">
                <span className="w-6 h-px bg-zinc-700"/> Voltar
              </button>
            </div>
          )}

          {/* ── STEP 4: Hora ──────────────────────────────────────────────── */}
          {step === 4 && (
            <div>
              <p className="text-[10px] tracking-[0.8em] text-zinc-300 uppercase mb-4">04 · Escolhe o horário</p>
              {selectedDate && (
                <p className="text-[10px] tracking-[0.3em] text-zinc-500 uppercase mb-8">
                  {selectedDate.getDate()} de {MESES[selectedDate.getMonth()]} {selectedDate.getFullYear()}
                </p>
              )}
              {loading ? (
                <div className="flex items-center justify-center py-20">
                  <div className="w-6 h-6 border border-white/30 border-t-white animate-spin"/>
                </div>
              ) : slots.length === 0 ? (
                <div className="border border-white/10 px-6 py-12 text-center">
                  <p className="text-[11px] tracking-[0.3em] text-zinc-500 uppercase">Sem horários disponíveis para este dia</p>
                  <button onClick={() => setStep(3)} className="mt-6 text-[10px] tracking-[0.3em] text-zinc-400 uppercase hover:text-white transition-colors">Escolher outra data</button>
                </div>
              ) : (
                <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
                  {slots.map(slot => (
                    <button key={slot}
                      onClick={() => { setSelectedSlot(slot); setStep(5) }}
                      className={`py-5 border text-center transition-all duration-200 ${
                        selectedSlot === slot
                          ? 'border-white bg-white text-black'
                          : 'border-white/15 text-zinc-300 hover:border-white/40 hover:text-white'
                      }`}>
                      <span className="text-[14px] font-semibold tracking-wide">{slot}</span>
                    </button>
                  ))}
                </div>
              )}
              <button onClick={() => setStep(3)} className="mt-8 text-[10px] tracking-[0.3em] text-zinc-500 uppercase hover:text-white flex items-center gap-3 transition-colors">
                <span className="w-6 h-px bg-zinc-700"/> Voltar
              </button>
            </div>
          )}

          {/* ── STEP 5: Dados ─────────────────────────────────────────────── */}
          {step === 5 && (
            <div>
              <p className="text-[10px] tracking-[0.8em] text-zinc-300 uppercase mb-8">05 · Os teus dados</p>

              <div className="border border-white/10 px-6 py-5 mb-10 bg-white/[0.02]">
                <p className="text-[9px] tracking-[0.6em] text-zinc-500 uppercase mb-4">Resumo</p>
                <div className="grid grid-cols-2 gap-y-3 text-[11px]">
                  <span className="text-zinc-500 uppercase tracking-wider">Barbeiro</span>
                  <span className="text-zinc-200 text-right">{selectedBarber?.name}</span>
                  <span className="text-zinc-500 uppercase tracking-wider">
                    {selectedServices.length === 1 ? 'Serviço' : 'Serviços'}
                  </span>
                  <span className="text-zinc-200 text-right">{selectedServices.map(s => s.name).join(', ')}</span>
                  <span className="text-zinc-500 uppercase tracking-wider">Data</span>
                  <span className="text-zinc-200 text-right">
                    {selectedDate?.getDate()} de {MESES[selectedDate?.getMonth() || 0]} {selectedDate?.getFullYear()}
                  </span>
                  <span className="text-zinc-500 uppercase tracking-wider">Hora</span>
                  <span className="text-zinc-200 text-right">{selectedSlot}</span>
                  <span className="text-zinc-500 uppercase tracking-wider">Total</span>
                  <span className="text-zinc-200 text-right font-semibold">{totalPrice}€</span>
                </div>
              </div>

              <div className="flex flex-col gap-6">
                <div>
                  <label className="text-[10px] tracking-[0.4em] text-zinc-400 uppercase mb-3 block">Nome completo</label>
                  <input
                    type="text"
                    value={clientName}
                    onChange={e => setClientName(e.target.value)}
                    placeholder="O teu nome"
                    maxLength={100}
                    className="w-full bg-transparent border border-white/20 focus:border-white/60 outline-none px-6 py-4 text-[13px] text-white transition-all"
                  />
                </div>

                <div>
                  <label className="text-[10px] tracking-[0.4em] text-zinc-400 uppercase mb-3 block">Telemóvel</label>
                  <div className="flex border border-white/20 focus-within:border-white/60 transition-all">
                    <span className="px-5 py-4 text-[13px] text-zinc-400 border-r border-white/20 select-none bg-white/[0.03] flex items-center">
                      +351
                    </span>
                    <input
                      type="tel"
                      value={phoneDigits}
                      onChange={e => setPhoneDigits(e.target.value.replace(/\D/g, '').slice(0, 9))}
                      placeholder="9XXXXXXXX"
                      maxLength={9}
                      className="flex-1 bg-transparent outline-none px-4 py-4 text-[13px] text-white"
                    />
                  </div>
                </div>

                <div>
                  <label className="text-[10px] tracking-[0.4em] text-zinc-400 uppercase mb-3 block">Email</label>
                  <input
                    type="email"
                    value={clientEmail}
                    onChange={e => setClientEmail(e.target.value)}
                    placeholder="email@exemplo.pt"
                    maxLength={160}
                    className="w-full bg-transparent border border-white/20 focus:border-white/60 outline-none px-6 py-4 text-[13px] text-white transition-all"
                  />
                </div>
              </div>

              <button
                onClick={handleSubmit}
                disabled={!clientName.trim() || phoneDigits.length !== 9 || !hasValidEmail || submitting}
                className={`mt-10 w-full flex items-center justify-between px-8 py-6 border transition-all duration-300 ${
                  clientName.trim() && phoneDigits.length === 9 && hasValidEmail && !submitting
                    ? 'border-white/20 hover:bg-white hover:text-black cursor-pointer'
                    : 'border-white/5 text-zinc-700 cursor-not-allowed'
                }`}
              >
                <span className="text-[11px] tracking-[0.3em] uppercase font-semibold">
                  {submitting ? 'A processar...' : 'Confirmar Marcação'}
                </span>
                {submitting
                  ? <div className="w-4 h-4 border border-current border-t-transparent animate-spin"/>
                  : <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
                }
              </button>

              <button onClick={() => setStep(4)} className="mt-6 text-[10px] tracking-[0.3em] text-zinc-500 uppercase hover:text-white flex items-center gap-3 transition-colors">
                <span className="w-6 h-px bg-zinc-700"/> Voltar
              </button>
            </div>
          )}

          {/* ── STEP 6: Confirmação ───────────────────────────────────────── */}
          {step === 6 && success && (
            <div className="text-center py-12">
              <div className="w-16 h-16 border border-white bg-white text-black flex items-center justify-center mx-auto mb-8 text-[24px]">✓</div>
              <h2 className="font-serif text-[clamp(2rem,5vw,48px)] uppercase tracking-tighter mb-4">Marcação Recebida</h2>
              <p className="text-[12px] text-zinc-400 tracking-wider leading-relaxed max-w-md mx-auto mb-12">
                Enviámos um email para <span className="text-zinc-200">{clientEmail}</span> com o link para confirmares a tua marcação.
              </p>

              <div className="border border-white/10 px-6 py-5 max-w-sm mx-auto bg-white/[0.02] mb-12">
                <div className="grid grid-cols-2 gap-y-3 text-[11px]">
                  <span className="text-zinc-500 uppercase tracking-wider">Barbeiro</span>
                  <span className="text-zinc-200 text-right">{selectedBarber?.name}</span>
                  <span className="text-zinc-500 uppercase tracking-wider">
                    {selectedServices.length === 1 ? 'Serviço' : 'Serviços'}
                  </span>
                  <span className="text-zinc-200 text-right">{selectedServices.map(s => s.name).join(', ')}</span>
                  <span className="text-zinc-500 uppercase tracking-wider">Data</span>
                  <span className="text-zinc-200 text-right">
                    {selectedDate?.getDate()} de {MESES[selectedDate?.getMonth() || 0]}
                  </span>
                  <span className="text-zinc-500 uppercase tracking-wider">Hora</span>
                  <span className="text-zinc-200 text-right">{selectedSlot}</span>
                  <span className="text-zinc-500 uppercase tracking-wider">Total</span>
                  <span className="text-zinc-200 text-right">{totalPrice}€</span>
                  <span className="text-zinc-500 uppercase tracking-wider">Estado</span>
                  <span className="text-yellow-500 text-right uppercase tracking-wider">Pendente</span>
                </div>
              </div>

              <Link href="/main" className="inline-block border border-white/20 px-12 py-5 text-[11px] tracking-[0.5em] uppercase hover:bg-white hover:text-black font-bold transition-all duration-500">
                Voltar ao Início
              </Link>
            </div>
          )}

        </div>
      </section>
    </div>
  )
}
