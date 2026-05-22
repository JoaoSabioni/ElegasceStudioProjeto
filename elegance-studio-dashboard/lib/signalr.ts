import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr'
import { getToken } from './auth'

const LOCAL_API = 'http://localhost:5134'
const PRODUCTION_API = 'https://elegascestudioprojeto-production.up.railway.app'
const API = process.env.NEXT_PUBLIC_API_URL ||
  (typeof window !== 'undefined' && window.location.hostname !== 'localhost'
    ? PRODUCTION_API
    : LOCAL_API)

let connection: HubConnection | null = null

export function getConnection(): HubConnection {
  if (connection) return connection

  const token = getToken()

  connection = new HubConnectionBuilder()
    .withUrl(`${API}/hubs/bookings`, {
      accessTokenFactory: () => token || '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()

  return connection
}

export async function startConnection(): Promise<HubConnection> {
  const conn = getConnection()
  if (conn.state === 'Disconnected') {
    try {
      await conn.start()
      console.log('[SignalR] Conectado')
    } catch (err) {
      console.error('[SignalR] Erro ao conectar:', err)
    }
  }
  return conn
}

export async function joinBarberGroup(barberId: string) {
  const conn = await startConnection()
  await conn.invoke('JoinBarberGroup', barberId)
  console.log(`[SignalR] Subscrito ao grupo barber-${barberId}`)
}

export function onNewBooking(callback: (booking: unknown) => void) {
  const conn = getConnection()
  conn.on('NewBooking', callback)
}

export function onBookingUpdated(callback: (booking: unknown) => void) {
  const conn = getConnection()
  conn.on('BookingUpdated', callback)
}

export async function stopConnection() {
  if (connection) {
    await connection.stop()
    connection = null
    console.log('[SignalR] Desconectado')
  }
}
