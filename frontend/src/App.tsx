import { OrderForm } from './components/OrderForm'
import { OrderList } from './components/OrderList'
import { useOrders } from './hooks/useOrders'
import { useProducts } from './hooks/useProducts'

export default function App() {
  const { orders, error, loading, refresh } = useOrders()
  const { products, error: productsError } = useProducts()

  return (
    <main className="layout">
      <header>
        <h1>OrderFlow</h1>
        <p className="muted">Panel de pedidos</p>
      </header>

      {productsError && <p className="alert">⚠ No se pudo cargar el catálogo: {productsError}</p>}

      <OrderForm products={products} onCreated={refresh} />
      <OrderList orders={orders} loading={loading} error={error} />
    </main>
  )
}