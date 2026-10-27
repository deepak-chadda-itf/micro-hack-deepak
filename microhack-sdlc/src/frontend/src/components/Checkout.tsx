import axios from 'axios';
import { CardElement, Elements, useElements, useStripe } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/config';
import { useCart } from '../context/CartContext';
import { useTheme } from '../context/ThemeContext';

interface Branch {
  branchId: number;
  name: string;
  address?: string | null;
}

interface PaymentConfiguration {
  provider: 'demo' | 'stripe';
  publishableKey: string | null;
}

interface CheckoutResult {
  orderId: number;
  status: string;
  total: number;
  paymentProvider: string;
}

interface CheckoutFormProps {
  branches: Branch[];
  branchId: string;
  customerName: string;
  error: string | null;
  isSubmitting: boolean;
  provider: 'demo' | 'stripe';
  onBranchChange: (branchId: string) => void;
  onCustomerNameChange: (name: string) => void;
  onSubmit: (paymentMethodId: string) => Promise<void>;
}

const formatPrice = (value: number) =>
  value.toLocaleString('en-US', { style: 'currency', currency: 'USD' });

function DemoCheckoutForm(props: CheckoutFormProps) {
  const [paymentMethodId, setPaymentMethodId] = useState('demo-success');

  return (
    <CheckoutFields {...props} onSubmit={() => props.onSubmit(paymentMethodId)}>
      <label className="block space-y-2">
        <span className="font-medium">Demo payment result</span>
        <select
          value={paymentMethodId}
          onChange={(event) => setPaymentMethodId(event.target.value)}
          className="w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-gray-900"
          aria-label="Demo payment result"
        >
          <option value="demo-success">Approved test payment</option>
          <option value="demo-decline">Declined test payment</option>
        </select>
      </label>
      <p className="text-sm text-gray-500">Demo mode does not collect or transmit card numbers.</p>
    </CheckoutFields>
  );
}

function StripeCheckoutForm(props: CheckoutFormProps) {
  const stripe = useStripe();
  const elements = useElements();
  const [cardError, setCardError] = useState<string | null>(null);

  async function submitStripePayment() {
    if (!stripe || !elements) {
      setCardError('Secure payment form is still loading. Please try again.');
      return;
    }

    const card = elements.getElement(CardElement);
    if (!card) {
      setCardError('Enter your payment details to continue.');
      return;
    }

    const result = await stripe.createPaymentMethod({
      type: 'card',
      card,
      billing_details: { name: props.customerName },
    });

    if (result.error || !result.paymentMethod) {
      setCardError(result.error?.message ?? 'Could not validate payment details.');
      return;
    }

    setCardError(null);
    await props.onSubmit(result.paymentMethod.id);
  }

  return (
    <CheckoutFields {...props} onSubmit={submitStripePayment}>
      <label className="block space-y-2">
        <span className="font-medium">Card details</span>
        <span className="block rounded-md border border-gray-300 bg-white p-3">
          <CardElement options={{ hidePostalCode: true }} />
        </span>
      </label>
      {cardError && <p className="text-red-600" role="alert">{cardError}</p>}
      <p className="text-sm text-gray-500">Card details are tokenized by Stripe and are not sent to this API.</p>
    </CheckoutFields>
  );
}

function CheckoutFields({
  branches,
  branchId,
  customerName,
  error,
  isSubmitting,
  onBranchChange,
  onCustomerNameChange,
  onSubmit,
  children,
}: CheckoutFormProps & { children: React.ReactNode; onSubmit: () => Promise<void> }) {
  const { darkMode } = useTheme();

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSubmit();
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <label className="block space-y-2">
        <span className="font-medium">Customer name</span>
        <input
          value={customerName}
          onChange={(event) => onCustomerNameChange(event.target.value)}
          required
          maxLength={120}
          autoComplete="name"
          className={`w-full rounded-md border px-3 py-2 ${darkMode ? 'bg-gray-900 border-gray-700 text-light' : 'bg-white border-gray-300 text-gray-900'}`}
        />
      </label>
      <label className="block space-y-2">
        <span className="font-medium">Pickup branch</span>
        <select
          value={branchId}
          onChange={(event) => onBranchChange(event.target.value)}
          required
          className={`w-full rounded-md border px-3 py-2 ${darkMode ? 'bg-gray-900 border-gray-700 text-light' : 'bg-white border-gray-300 text-gray-900'}`}
          aria-label="Pickup branch"
        >
          {branches.map((branch) => (
            <option key={branch.branchId} value={branch.branchId}>
              {branch.name}{branch.address ? `, ${branch.address}` : ''}
            </option>
          ))}
        </select>
      </label>
      {children}
      {error && <p className="text-red-600" role="alert">{error}</p>}
      <button
        type="submit"
        disabled={isSubmitting || !branchId}
        className="w-full rounded-md bg-primary px-5 py-3 font-semibold text-white hover:bg-accent disabled:cursor-not-allowed disabled:opacity-60"
      >
        {isSubmitting ? 'Processing payment...' : 'Pay now'}
      </button>
    </form>
  );
}

export default function Checkout() {
  const { cart, clearCart, isLoading: isCartLoading } = useCart();
  const { darkMode } = useTheme();
  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [configuration, setConfiguration] = useState<PaymentConfiguration | null>(null);
  const [loadingError, setLoadingError] = useState<string | null>(null);
  const [checkoutError, setCheckoutError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [result, setResult] = useState<CheckoutResult | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([
      axios.get<Branch[]>(`${api.baseURL}${api.endpoints.branches}`),
      axios.get<PaymentConfiguration>(`${api.baseURL}${api.endpoints.checkout}/config`),
    ])
      .then(([branchResponse, configurationResponse]) => {
        if (active) {
          setBranches(branchResponse.data);
          setBranchId(branchResponse.data[0]?.branchId.toString() ?? '');
          setConfiguration(configurationResponse.data);
        }
      })
      .catch(() => {
        if (active) {
          setLoadingError('Checkout is unavailable right now. Please try again.');
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
  }, []);

  async function submitCheckout(paymentMethodId: string) {
    setIsSubmitting(true);
    setCheckoutError(null);
    try {
      const { data } = await axios.post<CheckoutResult>(
        `${api.baseURL}${api.endpoints.checkout}/${cart.cartId}`,
        { branchId: Number(branchId), customerName, paymentMethodId },
      );
      clearCart();
      setResult(data);
    } catch (requestError) {
      const message = axios.isAxiosError<{ error?: { message?: string } }>(requestError)
        ? requestError.response?.data?.error?.message
        : null;
      setCheckoutError(message ?? 'Payment failed. Your cart is unchanged. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  }

  const containerClass = `min-h-screen pt-24 pb-16 px-4 ${darkMode ? 'bg-dark text-light' : 'bg-gray-100 text-gray-800'}`;

  if (result) {
    return (
      <section className={containerClass}>
        <div className="max-w-3xl mx-auto text-center space-y-4">
          <h1 className="text-3xl font-bold">Order confirmed</h1>
          <p role="status">Order #{result.orderId} has been paid and confirmed.</p>
          <p>Total paid: {formatPrice(result.total)}</p>
          <Link to="/products" className="inline-block mt-3 bg-primary hover:bg-accent text-white px-5 py-3 rounded-md">
            Continue shopping
          </Link>
        </div>
      </section>
    );
  }

  if (isCartLoading || isLoading) {
    return <section className={containerClass}><p role="status">Loading checkout...</p></section>;
  }

  if (loadingError) {
    return <section className={containerClass}><p role="alert">{loadingError}</p></section>;
  }

  if (cart.items.length === 0) {
    return (
      <section className={containerClass}>
        <div className="max-w-3xl mx-auto text-center space-y-4">
          <h1 className="text-3xl font-bold">Your cart is empty</h1>
          <Link to="/products" className="text-primary hover:underline">Browse products</Link>
        </div>
      </section>
    );
  }

  if (!configuration || branches.length === 0) {
    return <section className={containerClass}><p role="alert">No pickup branches are available for checkout.</p></section>;
  }

  if (configuration.provider === 'stripe' && !configuration.publishableKey) {
    return <section className={containerClass}><p role="alert">Stripe checkout is not configured. Contact the site administrator.</p></section>;
  }

  const formProps: CheckoutFormProps = {
    branches,
    branchId,
    customerName,
    error: checkoutError,
    isSubmitting,
    provider: configuration.provider,
    onBranchChange: setBranchId,
    onCustomerNameChange: setCustomerName,
    onSubmit: submitCheckout,
  };

  return (
    <section className={containerClass}>
      <div className="max-w-6xl mx-auto space-y-6">
        <h1 className="text-3xl font-bold">Checkout</h1>
        <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_20rem] gap-6 items-start">
          <div className={`${darkMode ? 'bg-gray-800 border-gray-700' : 'bg-white border-gray-200'} border rounded-lg p-5 sm:p-7`}>
            {configuration.provider === 'stripe' && configuration.publishableKey ? (
              <Elements stripe={loadStripe(configuration.publishableKey)}>
                <StripeCheckoutForm {...formProps} />
              </Elements>
            ) : (
              <DemoCheckoutForm {...formProps} />
            )}
          </div>
          <aside className={`${darkMode ? 'bg-gray-800 border-gray-700' : 'bg-white border-gray-200'} border rounded-lg p-5`} aria-label="Order summary">
            <h2 className="text-lg font-semibold">Order summary</h2>
            <ul className="divide-y divide-gray-200">
              {cart.items.map((item) => (
                <li key={item.productId} className="flex justify-between gap-4 py-3 text-sm">
                  <span>{item.productName} × {item.quantity}</span>
                  <span>{formatPrice(item.lineTotal)}</span>
                </li>
              ))}
            </ul>
            <div className="flex justify-between border-t border-gray-300 pt-4 mt-2 font-bold">
              <span>Total</span>
              <span>{formatPrice(cart.total)}</span>
            </div>
          </aside>
        </div>
      </div>
    </section>
  );
}