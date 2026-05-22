'use client'

import { useEffect, useState } from 'react'
import { createBooking, getAvailability, getBarbers, getServices } from '@/lib/api'
import { getUser } from '@/lib/auth'

interface NewBookingModalProps {
  onClose: () => void
  onCreated: () => void
}

type Barber = { id: string; name: string }
type Service = { id: string; name: string; durationMinutes: number }

const MONTHS = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']
const WEEK_DAYS = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S']

function getDaysInMonth(year: number, month: number) {
  return new Date(year, month + 1, 0).getDate()
}

function getFirstDay(year: number, month: number) {
  return new Date(year, month, 1).getDay()
}

function pad2(value: number) {
  return String(value).padStart(2, '0')
}

function toDateStr(year: number, month: number, day: number) {
  return `${year}-${pad2(month + 1)}-${pad2(day)}`
}

export default function NewBookingModal({ onClose, onCreated }: NewBookingModalProps) {
  const currentUser = getUser()
  const isBarber = currentUser?.role === 'Barber'
  const today = new Date()

  const [barbers, setBarbers] = useState<Barber[]>([])
  const [services, setServices] = useState<Service[]>([])
  const [barberId, setBarberId] = useState(isBarber && currentUser?.barberId ? currentUser.barberId : '')
  const [selectedServiceIds, setSelectedServiceIds] = useState<string[]>([])
  const [clientName, setClientName] = useState('')
  const [clientEmail, setClientEmail] = useState('')
  const [phone, setPhone] = useState('+351 ')
  const [calYear, setCalYear] = useState(today.getFullYear())
  const [calMonth, setCalMonth] = useState(today.getMonth())
  const [selectedDay, setSelectedDay] = useState<number | null>(null)
  const [selectedTime, setSelectedTime] = useState('')
  const [availableSlots, setAvailableSlots] = useState<string[]>([])
  const [loading, setLoading] = useState(false)
  const [slotsLoading, setSlotsLoading] = useState(false)
  const [error, setError] = useState('')

  const bookingDate = selectedDay ? toDateStr(calYear, calMonth, selectedDay) : ''
  const autoBarberName = barbers.find(b => b.id === barberId)?.name
  const availableServices = services.filter(service => !selectedServiceIds.includes(service.id))
  const totalDuration = selectedServiceIds.reduce(
    (total, id) => total + (services.find(service => service.id === id)?.durationMinutes ?? 0),
    0
  )
  const hasValidEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(clientEmail.trim())

  useEffect(() => {
    getBarbers().then(setBarbers).catch(() => {})
    getServices().then(setServices).catch(() => {})
  }, [])

  useEffect(() => {
    setSelectedTime('')
    setAvailableSlots([])

    if (!barberId || selectedServiceIds.length === 0 || !bookingDate) return

    let cancelled = false
    setSlotsLoading(true)
    setError('')

    getAvailability(barberId, bookingDate, selectedServiceIds)
      .then(data => {
        if (!cancelled) setAvailableSlots(data.availableSlots ?? [])
      })
      .catch(() => {
        if (!cancelled) {
          setAvailableSlots([])
          setError('Erro ao carregar disponibilidade.')
        }
      })
      .finally(() => {
        if (!cancelled) setSlotsLoading(false)
      })

    return () => { cancelled = true }
  }, [barberId, selectedServiceIds, bookingDate])

  const removeService = (id: string) =>
    setSelectedServiceIds(prev => prev.filter(serviceId => serviceId !== id))

  const prevMonth = () => {
    if (calMonth === 0) {
      setCalMonth(11)
      setCalYear(year => year - 1)
    } else {
      setCalMonth(month => month - 1)
    }
    setSelectedDay(null)
  }

  const nextMonth = () => {
    if (calMonth === 11) {
      setCalMonth(0)
      setCalYear(year => year + 1)
    } else {
      setCalMonth(month => month + 1)
    }
    setSelectedDay(null)
  }

  const handlePhone = (value: string) => {
    if (!value.startsWith('+351')) {
      setPhone('+351 ')
      return
    }
    setPhone(value)
  }

  const handleSubmit = async () => {
    if (!barberId || selectedServiceIds.length === 0 || !clientName.trim() ||
        !phone || phone.trim() === '+351' || !hasValidEmail || !bookingDate || !selectedTime) {
      setError('Preenche todos os campos e adiciona pelo menos um servico.')
      return
    }

    setLoading(true)
    setError('')

    try {
      await createBooking({
        barberId,
        serviceIds: selectedServiceIds,
        clientName: clientName.trim(),
        clientPhone: phone.replace(/\s+/g, '').trim(),
        clientEmail: clientEmail.trim().toLowerCase(),
        bookingDate,
        bookingTime: `${selectedTime}:00`,
      })
      onClose()
      onCreated()
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : ''
      setError(message === 'BOOKING_CONFLICT'
        ? 'Esse horario ja esta ocupado.'
        : 'Erro ao criar marcacao. Tenta novamente.')
      setLoading(false)
    }
  }

  const daysInMonth = getDaysInMonth(calYear, calMonth)
  const firstDay = getFirstDay(calYear, calMonth)
  const calendarCells: (number | null)[] = [
    ...Array(firstDay).fill(null),
    ...Array.from({ length: daysInMonth }, (_, index) => index + 1),
  ]
  while (calendarCells.length % 7 !== 0) calendarCells.push(null)

  return (
    <div
      className="fixed inset-0 z-[200] flex items-center justify-center bg-black/85 backdrop-blur-sm px-4"
      onClick={onClose}
    >
      <div
        className="bg-zinc-950 border border-white/15 w-full max-w-lg p-7 shadow-2xl max-h-[92vh] overflow-y-auto"
        onClick={event => event.stopPropagation()}
      >
        <div className="flex items-center justify-between mb-7">
          <div>
            <p className="text-[9px] tracking-[0.5em] text-zinc-500 uppercase">Agenda</p>
            <h2 className="mt-1 text-lg font-semibold text-white">Nova marcacao</h2>
          </div>
          <button onClick={onClose} className="text-zinc-600 hover:text-white transition-colors">x</button>
        </div>

        <div className="space-y-5">
          <div>
            <label className="block text-[9px] tracking-[0.3em] text-zinc-500 uppercase mb-1.5">Barbeiro</label>
            {isBarber ? (
              <div className="w-full bg-zinc-900/50 border border-white/8 text-zinc-300 text-[12px] px-3 py-2.5 flex items-center justify-between">
                <span>{autoBarberName ?? '...'}</span>
                <span className="text-[9px] text-zinc-700 uppercase tracking-wider">Auto</span>
              </div>
            ) : (
              <select
                value={barberId}
                onChange={event => setBarberId(event.target.value)}
                className="w-full bg-zinc-900 border border-white/10 text-white text-[12px] px-3 py-2.5 focus:outline-none focus:border-white/30"
              >
                <option value="">Selecionar...</option>
                {barbers.map(barber => <option key={barber.id} value={barber.id}>{barber.name}</option>)}
              </select>
            )}
          </div>

          <div>
            <label className="block text-[9px] tracking-[0.3em] text-zinc-500 uppercase mb-1.5">
              Servicos
              {totalDuration > 0 && (
                <span className="ml-2 text-zinc-600 normal-case tracking-normal text-[10px]">
                  {totalDuration} min total
                </span>
              )}
            </label>

            {selectedServiceIds.length > 0 && (
              <div className="mb-2 space-y-1.5">
                {selectedServiceIds.map(id => {
                  const service = services.find(item => item.id === id)
                  if (!service) return null
                  return (
                    <div key={id} className="flex items-center justify-between px-3 py-2 bg-zinc-900/60 border border-white/10">
                      <span className="text-[11px] text-zinc-300">
                        {service.name}
                        <span className="ml-2 text-zinc-600 text-[10px]">{service.durationMinutes} min</span>
                      </span>
                      <button onClick={() => removeService(id)} className="text-zinc-600 hover:text-red-400 transition-colors ml-3">x</button>
                    </div>
                  )
                })}
              </div>
            )}

            {availableServices.length > 0 ? (
              <select
                value=""
                onChange={event => {
                  const id = event.target.value
                  if (id && !selectedServiceIds.includes(id))
                    setSelectedServiceIds(prev => [...prev, id])
                }}
                className="w-full bg-zinc-900 border border-white/10 text-white text-[12px] px-3 py-2.5 focus:outline-none focus:border-white/30"
              >
                <option value="">Adicionar servico...</option>
                {availableServices.map(service => (
                  <option key={service.id} value={service.id}>{service.name}</option>
                ))}
              </select>
            ) : selectedServiceIds.length > 0 && (
              <p className="text-[10px] text-zinc-600 tracking-[0.2em] uppercase mt-1">Todos os servicos adicionados</p>
            )}
          </div>

          <div>
            <label className="block text-[9px] tracking-[0.3em] text-zinc-500 uppercase mb-1.5">Nome do cliente</label>
            <input
              type="text"
              value={clientName}
              onChange={event => setClientName(event.target.value)}
              placeholder="Nome completo"
              className="w-full bg-zinc-900 border border-white/10 text-white text-[12px] px-3 py-2.5 focus:outline-none focus:border-white/30 placeholder:text-zinc-700"
            />
          </div>

          <div>
            <label className="block text-[9px] tracking-[0.3em] text-zinc-500 uppercase mb-1.5">Telefone</label>
            <input
              type="tel"
              value={phone}
              onChange={event => handlePhone(event.target.value)}
              className="w-full bg-zinc-900 border border-white/10 text-white text-[12px] px-3 py-2.5 focus:outline-none focus:border-white/30"
            />
          </div>

          <div>
            <label className="block text-[9px] tracking-[0.3em] text-zinc-500 uppercase mb-1.5">Email</label>
            <input
              type="email"
              value={clientEmail}
              onChange={event => setClientEmail(event.target.value)}
              placeholder="email@exemplo.pt"
              className="w-full bg-zinc-900 border border-white/10 text-white text-[12px] px-3 py-2.5 focus:outline-none focus:border-white/30 placeholder:text-zinc-700"
            />
          </div>

          <div>
            <label className="block text-[9px] tracking-[0.3em] text-zinc-500 uppercase mb-2">Data</label>
            <div className="border border-white/10 bg-zinc-900/40 p-3">
              <div className="flex items-center justify-between mb-3">
                <button onClick={prevMonth} className="w-7 h-7 flex items-center justify-center text-zinc-500 hover:text-white transition-colors text-lg">{'<'}</button>
                <div className="text-center">
                  <span className="text-[11px] tracking-[0.3em] text-zinc-200 uppercase">{MONTHS[calMonth]}</span>
                  <span className="ml-2 text-[11px] text-zinc-600 font-mono">{calYear}</span>
                </div>
                <button onClick={nextMonth} className="w-7 h-7 flex items-center justify-center text-zinc-500 hover:text-white transition-colors text-lg">{'>'}</button>
              </div>

              <div className="grid grid-cols-7 mb-1">
                {WEEK_DAYS.map((day, index) => (
                  <div key={`${day}-${index}`} className="text-center text-[9px] text-zinc-700 py-1">{day}</div>
                ))}
              </div>

              <div className="grid grid-cols-7 gap-0.5">
                {calendarCells.map((day, index) => {
                  if (day === null) return <div key={`empty-${index}`} />

                  const isSelected = day === selectedDay
                  const cellDate = new Date(calYear, calMonth, day)
                  const todayDate = new Date(today.getFullYear(), today.getMonth(), today.getDate())
                  const isPast = cellDate < todayDate

                  return (
                    <button
                      key={day}
                      onClick={() => !isPast && setSelectedDay(day)}
                      disabled={isPast}
                      className={`h-8 text-[11px] font-mono flex items-center justify-center transition-all ${
                        isSelected
                          ? 'bg-white text-black font-semibold'
                          : isPast
                            ? 'text-zinc-800 cursor-not-allowed'
                            : 'text-zinc-400 hover:bg-white/8 hover:text-white'
                      }`}
                    >
                      {day}
                    </button>
                  )
                })}
              </div>

              {selectedDay && (
                <p className="text-center text-[10px] text-zinc-500 mt-2 tracking-wider font-mono">
                  {pad2(selectedDay)} / {pad2(calMonth + 1)} / {calYear}
                </p>
              )}
            </div>
          </div>

          <div>
            <label className="block text-[9px] tracking-[0.3em] text-zinc-500 uppercase mb-2">
              Hora
              {selectedServiceIds.length === 0 && (
                <span className="ml-2 text-zinc-700 normal-case tracking-normal text-[10px]">seleciona servico</span>
              )}
              {!selectedDay && (
                <span className="ml-2 text-zinc-700 normal-case tracking-normal text-[10px]">seleciona data</span>
              )}
            </label>
            <div className="border border-white/10 bg-zinc-900/40 p-3">
              {slotsLoading ? (
                <p className="text-center text-[10px] text-zinc-700 py-4">A carregar horarios disponiveis...</p>
              ) : availableSlots.length === 0 ? (
                <p className="text-center text-[10px] text-zinc-700 py-4">
                  Sem horarios disponiveis para esta selecao.<br />
                  <span className="text-zinc-800">Seleciona outro dia ou servico.</span>
                </p>
              ) : (
                <div className="grid grid-cols-6 gap-1">
                  {availableSlots.map(slot => (
                    <button
                      key={slot}
                      onClick={() => setSelectedTime(slot === selectedTime ? '' : slot)}
                      className={`py-2 font-mono text-[10px] tracking-wide transition-all border ${
                        selectedTime === slot
                          ? 'bg-white text-black border-white font-semibold'
                          : 'border-white/8 text-zinc-500 hover:border-white/30 hover:text-zinc-200'
                      }`}
                    >
                      {slot}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {error && (
          <p className="mt-4 text-[10px] text-red-400 tracking-[0.2em]">{error}</p>
        )}

        <div className="flex gap-2 mt-7">
          <button
            onClick={onClose}
            className="flex-1 py-3.5 border border-white/10 text-zinc-500 text-[10px] tracking-[0.3em] uppercase hover:border-white/25 hover:text-white transition-all"
          >
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={loading || slotsLoading}
            className="flex-1 py-3.5 bg-white text-black text-[10px] tracking-[0.3em] uppercase font-bold hover:bg-zinc-200 transition-all disabled:opacity-40"
          >
            {loading ? '...' : 'Criar marcacao'}
          </button>
        </div>
      </div>
    </div>
  )
}
