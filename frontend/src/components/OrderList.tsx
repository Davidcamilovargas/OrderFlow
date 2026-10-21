import type { Order } from '../api'
import { StatusBadge } from './StatusBadge'

interface Props {
  orders: Order[]
  loading: boolean
  error: string | null
}

export function OrderList({ orders, loading, error }: Props) {
  return (
    <section className="card">
      <h2>Pedidos</h2>

      {error && <p className="alert">⚠ {error} Reintentando…</p>}

      {loading ? (
        <p className="muted">Cargando pedidos…</p>
      ) : orders.length === 0 ? (
        <p className="muted">Todavía no hay pedidos.</p>
      ) : (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Cliente</th>
                <th>SKU</th>
                <th>Cantidad</th>
                <th>Estado</th>
                <th>Creado</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr key={order.id}>
                  <td>{order.clienteNombre}</td>
                  <td>{order.sku}</td>
                  <td>{order.cantidad}</td>
                  <td>
                    <StatusBadge status={order.estado} />
                    {order.motivoRechazo && <div className="reason">{order.motivoRechazo}</div>}
                  </td>
                  <td>{new Date(order.creadoEn).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}