import { Link } from 'react-router-dom';
import { useCart } from '../context/CartContext';
import { useTheme } from '../context/ThemeContext';

const formatPrice = (value: number) =>
  value.toLocaleString('en-US', { style: 'currency', currency: 'USD' });

export default function Cart() {
  const { cart, error, isLoading, removeItem, setItemQuantity } = useCart();
  const { darkMode } = useTheme();

  return (
    <section className={`min-h-screen pt-24 pb-16 px-4 ${darkMode ? 'bg-dark text-light' : 'bg-gray-100 text-gray-800'}`}>
      <div className="max-w-6xl mx-auto space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="text-3xl font-bold">Your Cart</h1>
            <p className={`${darkMode ? 'text-gray-400' : 'text-gray-600'} mt-1`}>
              {cart.itemCount} {cart.itemCount === 1 ? 'item' : 'items'}
            </p>
          </div>
          <Link to="/products" className="text-primary hover:underline">Continue shopping</Link>
        </header>

        {error && <p className="text-red-600" role="alert">{error}</p>}

        {isLoading ? (
          <p role="status">Loading your cart...</p>
        ) : cart.items.length === 0 ? (
          <div className={`${darkMode ? 'bg-gray-800 border-gray-700' : 'bg-white border-gray-200'} border rounded-lg p-10 text-center`}>
            <h2 className="text-xl font-semibold">Your cart is empty</h2>
            <p className={`${darkMode ? 'text-gray-400' : 'text-gray-600'} mt-2`}>Find something useful for your cat.</p>
            <Link to="/products" className="inline-block mt-5 bg-primary hover:bg-accent text-white px-5 py-2 rounded-md">
              Browse products
            </Link>
          </div>
        ) : (
          <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_20rem] gap-6 items-start">
            <div className={`${darkMode ? 'bg-gray-800 divide-gray-700' : 'bg-white divide-gray-200'} rounded-lg border ${darkMode ? 'border-gray-700' : 'border-gray-200'} divide-y`}>
              {cart.items.map((item) => (
                <article key={item.productId} className="p-4 sm:p-6 flex flex-wrap sm:flex-nowrap items-center gap-4">
                  {item.imgName && (
                    <img src={`/${item.imgName}`} alt="" className="w-20 h-20 object-contain shrink-0" />
                  )}
                  <div className="min-w-0 flex-1">
                    <h2 className="font-semibold text-lg">{item.productName}</h2>
                    <p className={`${darkMode ? 'text-gray-400' : 'text-gray-600'} text-sm mt-1`}>
                      {formatPrice(item.unitPrice)} each
                    </p>
                    <button
                      type="button"
                      onClick={() => removeItem(item.productId)}
                      className="text-sm text-red-600 hover:underline mt-3"
                      aria-label={`Remove ${item.productName} from cart`}
                    >
                      Remove
                    </button>
                  </div>
                  <div className="flex items-center gap-3">
                    <button
                      type="button"
                      onClick={() => setItemQuantity(item.productId, item.quantity - 1)}
                      disabled={item.quantity <= 1}
                      className="w-8 h-8 border rounded disabled:opacity-40"
                      aria-label={`Decrease ${item.productName} quantity`}
                    >
                      -
                    </button>
                    <span className="min-w-6 text-center" aria-label={`${item.productName} quantity`}>
                      {item.quantity}
                    </span>
                    <button
                      type="button"
                      onClick={() => setItemQuantity(item.productId, item.quantity + 1)}
                      className="w-8 h-8 border rounded"
                      aria-label={`Increase ${item.productName} quantity`}
                    >
                      +
                    </button>
                  </div>
                  <strong className="w-24 text-right">{formatPrice(item.lineTotal)}</strong>
                </article>
              ))}
            </div>

            <aside className={`${darkMode ? 'bg-gray-800 border-gray-700' : 'bg-white border-gray-200'} border rounded-lg p-5`} aria-label="Order summary">
              <h2 className="font-semibold text-lg">Order summary</h2>
              <div className="flex justify-between mt-5">
                <span>Subtotal</span>
                <span>{formatPrice(cart.total)}</span>
              </div>
              <div className={`flex justify-between border-t ${darkMode ? 'border-gray-700' : 'border-gray-200'} mt-4 pt-4 text-lg font-bold`} aria-live="polite">
                <span>Total</span>
                <span>{formatPrice(cart.total)}</span>
              </div>
            </aside>
          </div>
        )}
      </div>
    </section>
  );
}