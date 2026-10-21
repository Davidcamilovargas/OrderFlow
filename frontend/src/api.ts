// Cliente HTTP de Orders API. Todas las rutas van bajo /api: en desarrollo las redirige el proxy
// de Vite y en Docker las redirige nginx, así el frontend nunca necesita conocer la URL real de la API.
const BASE_URL = '/api'

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected'

export interface Order {
  id: string
  clienteNombre: string
  sku: string
  cantidad: number
  estado: OrderStatus
  motivoRechazo: string | null
  creadoEn: string
}

export interface Product {
  sku: string
  nombre: string
}

export interface CreateOrderRequest {
  clienteNombre: string
  sku: string
  cantidad: number
}

export type FieldErrors = Record<string, string[]>

/** Error de la API con los errores por campo del ProblemDetails (si vienen). */
export class ApiError extends Error {
  readonly fieldErrors: FieldErrors

  constructor(message: string, fieldErrors: FieldErrors = {}) {
    super(message)
    this.fieldErrors = fieldErrors
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers },
    })
  } catch {
    throw new ApiError('No se pudo conectar con el servidor.')
  }

  if (response.ok) {
    return (await response.json()) as T
  }

  // 502/503/504: el proxy (Vite o nginx) responde, pero la API detrás no está disponible.
  if (response.status >= 502 && response.status <= 504) {
    throw new ApiError('No se pudo conectar con el servidor.')
  }

  // 400 de validación: ASP.NET devuelve ProblemDetails con un diccionario "errors" por campo.
  const problem = await response.json().catch(() => null)
  throw new ApiError(problem?.title ?? `Error ${response.status} del servidor.`, problem?.errors ?? {})
}

export const api = {
  getOrders: () => request<Order[]>('/orders'),
  getProducts: () => request<Product[]>('/products'),
  createOrder: (body: CreateOrderRequest) =>
    request<Order>('/orders', { method: 'POST', body: JSON.stringify(body) }),
}