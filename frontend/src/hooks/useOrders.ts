import { useCallback, useEffect, useState } from 'react'
import { api, type Order } from '../api'

const POLL_INTERVAL_MS = 2000

/** Lista de pedidos que se refresca sola cada 2 segundos (polling). */
export function useOrders() {
  const [orders, setOrders] = useState<Order[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    try {
      setOrders(await api.getOrders())
      setError(null)
    } catch (e) {
      // Se conservan los últimos datos y se avisa: es mejor que dejar la tabla vacía.
      setError(e instanceof Error ? e.message : 'Error desconocido.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
    const id = setInterval(() => {
      // Si la pestaña no está visible no tiene sentido consultar la API.
      if (!document.hidden) void refresh()
    }, POLL_INTERVAL_MS)
    return () => clearInterval(id)
  }, [refresh])

  return { orders, error, loading, refresh }
}