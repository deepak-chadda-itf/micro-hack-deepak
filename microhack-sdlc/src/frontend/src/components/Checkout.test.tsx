import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Checkout from './Checkout';

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  clearCart: vi.fn(),
  cart: {
    cartId: 'd1b332f2-d678-4316-8a72-d3297c808e99',
    items: [{ productId: 7, productName: 'Test Product', imgName: null, quantity: 2, unitPrice: 12.5, lineTotal: 25 }],
    itemCount: 2,
    total: 25,
  },
}));

vi.mock('axios', () => ({
  default: {
    get: mocks.get,
    post: mocks.post,
    isAxiosError: (error: unknown) => Boolean(error && typeof error === 'object' && 'isAxiosError' in error),
  },
}));

vi.mock('../context/CartContext', () => ({
  useCart: () => ({ cart: mocks.cart, clearCart: mocks.clearCart, isLoading: false }),
}));

vi.mock('../context/ThemeContext', () => ({
  useTheme: () => ({ darkMode: false }),
}));

describe('Checkout', () => {
  beforeEach(() => {
    mocks.get.mockReset()
      .mockResolvedValueOnce({ data: [{ branchId: 2, name: 'Meowtown Branch' }] })
      .mockResolvedValueOnce({ data: { provider: 'demo', publishableKey: null } });
    mocks.post.mockReset();
    mocks.clearCart.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it('clears local cart state only after checkout succeeds', async () => {
    mocks.post.mockResolvedValue({
      data: { orderId: 321, status: 'confirmed', total: 25, paymentProvider: 'demo' },
    });
    render(<MemoryRouter><Checkout /></MemoryRouter>);

    await screen.findByRole('heading', { name: 'Checkout' });
    fireEvent.change(screen.getByLabelText('Customer name'), { target: { value: 'Casey Customer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Pay now' }));

    expect(await screen.findByRole('heading', { name: 'Order confirmed' })).toBeTruthy();
    expect(mocks.post).toHaveBeenCalledWith(
      expect.stringContaining(`/api/checkout/${mocks.cart.cartId}`),
      { branchId: 2, customerName: 'Casey Customer', paymentMethodId: 'demo-success' },
    );
    expect(mocks.clearCart).toHaveBeenCalledOnce();
  });

  it('shows declined payment errors and preserves cart state', async () => {
    const decline = Object.assign(new Error('Card declined'), {
      isAxiosError: true,
      response: { data: { error: { message: 'The card was declined.' } } },
    });
    mocks.post.mockRejectedValue(decline);
    render(<MemoryRouter><Checkout /></MemoryRouter>);

    await screen.findByRole('heading', { name: 'Checkout' });
    fireEvent.change(screen.getByLabelText('Customer name'), { target: { value: 'Casey Customer' } });
    fireEvent.change(screen.getByLabelText('Demo payment result'), { target: { value: 'demo-decline' } });
    fireEvent.click(screen.getByRole('button', { name: 'Pay now' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The card was declined.');
    expect(mocks.clearCart).not.toHaveBeenCalled();
    expect(mocks.cart.items).toHaveLength(1);
    expect(mocks.cart.items[0].quantity).toBe(2);
  });
});