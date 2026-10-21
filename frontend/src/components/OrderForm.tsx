import { useState, type FormEvent } from 'react'
import { api, ApiError, type FieldErrors, type Product } from '../api'

interface Props {
  products: Product[]
  onCreated: () => void
}

const EMPTY_FORM = { clienteNombre: '', sku: '', cantidad: '1' }

// Las mismas reglas que valida el backend. Aquí son solo para dar feedback inmediato:
// la validación que manda es la del servidor.
function validate(form: typeof EMPTY_FORM): FieldErrors {
  const errors: FieldErrors = {}
  const cantidad = Number(form.cantidad)
  if (!form.clienteNombre.trim()) errors.clienteNombre = ['El nombre del cliente es obligatorio.']
  if (!form.sku) errors.sku = ['Selecciona un producto.']
  if (!Number.isInteger(cantidad) || cantidad < 1 || cantidad > 100)
    errors.cantidad = ['La cantidad debe estar entre 1 y 100.']
  return errors
}

export function OrderForm({ products, onCreated }: Props) {
  const [form, setForm] = useState(EMPTY_FORM)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [generalError, setGeneralError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const update = (field: keyof typeof EMPTY_FORM, value: string) => {
    setForm((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => ({ ...current, [field]: [] }))
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setGeneralError(null)
    setSuccess(null)

    const errors = validate(form)
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors)
      return
    }

    setSubmitting(true)
    try {
      const order = await api.createOrder({
        clienteNombre: form.clienteNombre,
        sku: form.sku,
        cantidad: Number(form.cantidad),
      })
      setSuccess(`Pedido de ${order.clienteNombre} registrado. Verificando stock…`)
      setForm(EMPTY_FORM)
      setFieldErrors({})
      onCreated()
    } catch (e) {
      if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) {
        setFieldErrors(e.fieldErrors)
      } else {
        setGeneralError(e instanceof Error ? e.message : 'Error desconocido.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  const errorFor = (field: string) => fieldErrors[field]?.[0]

  return (
    <section className="card">
      <h2>Nuevo pedido</h2>
      <form onSubmit={handleSubmit} noValidate>
        <label>
          Cliente
          <input
            value={form.clienteNombre}
            onChange={(e) => update('clienteNombre', e.target.value)}
            aria-invalid={!!errorFor('clienteNombre')}
          />
          {errorFor('clienteNombre') && <span className="field-error">{errorFor('clienteNombre')}</span>}
        </label>

        <label>
          Producto
          <select value={form.sku} onChange={(e) => update('sku', e.target.value)} aria-invalid={!!errorFor('sku')}>
            <option value="">Selecciona…</option>
            {products.map((p) => (
              <option key={p.sku} value={p.sku}>
                {p.sku} — {p.nombre}
              </option>
            ))}
          </select>
          {errorFor('sku') && <span className="field-error">{errorFor('sku')}</span>}
        </label>

        <label>
          Cantidad
          <input
            type="number"
            min={1}
            max={100}
            value={form.cantidad}
            onChange={(e) => update('cantidad', e.target.value)}
            aria-invalid={!!errorFor('cantidad')}
          />
          {errorFor('cantidad') && <span className="field-error">{errorFor('cantidad')}</span>}
        </label>

        {/* Deshabilitado mientras se envía: evita pedidos duplicados por doble clic. */}
        <button type="submit" disabled={submitting}>
          {submitting ? 'Enviando…' : 'Crear pedido'}
        </button>

        {generalError && <p className="alert">⚠ {generalError}</p>}
        {success && <p className="success">✓ {success}</p>}
      </form>
    </section>
  )
}