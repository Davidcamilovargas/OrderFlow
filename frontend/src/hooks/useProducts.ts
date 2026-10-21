import { useEffect, useState } from 'react'
import { api, type Product } from '../api'

/** Catálogo de productos para el selector del formulario. Se carga una sola vez. */
export function useProducts() {
  const [products, setProducts] = useState<Product[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api
      .getProducts()
      .then(setProducts)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'Error desconocido.'))
  }, [])

  return { products, error }
}