import axios from 'axios';
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { api } from '../api/config';

export interface CartItem {
  productId: number;
  productName: string;
  imgName: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface CartState {
  cartId: string;
  items: CartItem[];
  itemCount: number;
  total: number;
}

interface CartContextValue {
  cart: CartState;
  error: string | null;
  isLoading: boolean;
  addItem: (productId: number, quantity: number) => Promise<boolean>;
  setItemQuantity: (productId: number, quantity: number) => Promise<boolean>;
  removeItem: (productId: number) => Promise<boolean>;
}

const CART_STORAGE_KEY = 'octosupply-cart-id';
const CartContext = createContext<CartContextValue | null>(null);

function createCartId() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }

  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (character) => {
    const random = Math.floor(Math.random() * 16);
    return (character === 'x' ? random : (random & 0x3) | 0x8).toString(16);
  });
}

function getOrCreateCartId() {
  const existingId = window.localStorage.getItem(CART_STORAGE_KEY);
  if (existingId) {
    return existingId;
  }

  const cartId = createCartId();
  window.localStorage.setItem(CART_STORAGE_KEY, cartId);
  return cartId;
}

export function CartProvider({ children }: { children: ReactNode }) {
  const [cartId] = useState(getOrCreateCartId);
  const [cart, setCart] = useState<CartState>({
    cartId,
    items: [],
    itemCount: 0,
    total: 0,
  });
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const cartUrl = `${api.baseURL}${api.endpoints.cart}/${cartId}`;

  useEffect(() => {
    let active = true;

    axios.get<CartState>(cartUrl)
      .then(({ data }) => {
        if (active) {
          setCart(data);
          setError(null);
        }
      })
      .catch(() => {
        if (active) {
          setError('Unable to load your cart. Please try again.');
        }
      })
      .finally(() => {
        if (active) {
          setIsLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [cartUrl]);

  async function saveCart(request: Promise<{ data: CartState }>) {
    setError(null);
    try {
      const { data } = await request;
      setCart(data);
      return true;
    } catch {
      setError('Unable to update your cart. Please try again.');
      return false;
    }
  }

  function addItem(productId: number, quantity: number) {
    return saveCart(axios.post<CartState>(`${cartUrl}/items`, { productId, quantity }));
  }

  function setItemQuantity(productId: number, quantity: number) {
    return saveCart(axios.put<CartState>(`${cartUrl}/items/${productId}`, { quantity }));
  }

  function removeItem(productId: number) {
    return saveCart(axios.delete<CartState>(`${cartUrl}/items/${productId}`));
  }

  return (
    <CartContext.Provider value={{ cart, error, isLoading, addItem, setItemQuantity, removeItem }}>
      {children}
    </CartContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCart() {
  const context = useContext(CartContext);
  if (!context) {
    throw new Error('useCart must be used within a CartProvider');
  }
  return context;
}