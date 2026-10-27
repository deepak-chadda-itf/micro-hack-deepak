import { test, expect } from '@playwright/test';

async function addProductAndOpenCheckout(page: import('@playwright/test').Page) {
  await page.goto('/products');
  await page.getByRole('button', { name: 'Increase quantity of SmartFeeder One' }).click();
  await page.getByRole('button', { name: 'Add 1 SmartFeeder One to cart' }).click();
  await page.getByRole('link', { name: /Cart, 1 items/ }).click();
  await page.getByRole('link', { name: 'Proceed to checkout' }).click();
  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible();
  await page.getByLabel('Customer name').fill('Casey Customer');
}

test('approved payment creates an order and clears the cart', async ({ page }) => {
  await addProductAndOpenCheckout(page);
  const checkoutResponsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/checkout/') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Pay now' }).click();
  const checkoutResponse = await checkoutResponsePromise;
  expect(checkoutResponse.status()).toBe(201);
  const checkoutResult = await checkoutResponse.json() as { orderId: number };

  const [orderResponse, detailsResponse] = await Promise.all([
    page.request.get(`http://localhost:3000/api/orders/${checkoutResult.orderId}`),
    page.request.get('http://localhost:3000/api/order-details'),
  ]);
  expect(orderResponse.ok()).toBeTruthy();
  const order = await orderResponse.json() as { status: string; name: string };
  expect(order.status).toBe('confirmed');
  expect(order.name).toBe('Casey Customer');
  expect(detailsResponse.ok()).toBeTruthy();
  const details = await detailsResponse.json() as Array<{
    orderId: number;
    productId: number;
    quantity: number;
    unitPrice: number;
  }>;
  expect(details).toContainEqual(expect.objectContaining({
    orderId: checkoutResult.orderId,
    productId: 1,
    quantity: 1,
    unitPrice: 97.49,
  }));

  await expect(page.getByRole('heading', { name: 'Order confirmed' })).toBeVisible();
  await expect(page.getByRole('status')).toContainText('has been paid and confirmed');
  await expect(page.getByRole('link', { name: /Cart, 0 items/ })).toBeVisible();
});

test('declined payment displays an error and preserves the cart', async ({ page }) => {
  await addProductAndOpenCheckout(page);
  await page.getByLabel('Demo payment result').selectOption('demo-decline');
  await page.getByRole('button', { name: 'Pay now' }).click();

  await expect(page.getByRole('alert')).toContainText('declined');
  await expect(page.getByRole('link', { name: /Cart, 1 items/ })).toBeVisible();
  await page.getByRole('link', { name: /Cart, 1 items/ }).click();
  await expect(page.getByRole('heading', { name: 'SmartFeeder One' })).toBeVisible();
});