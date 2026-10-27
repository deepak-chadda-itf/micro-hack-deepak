import { test, expect } from '@playwright/test';

test('add, update, reload, and remove cart items', async ({ page }) => {
  await page.goto('/products');

  await page.getByRole('button', { name: 'Increase quantity of SmartFeeder One' }).click();
  await page.getByRole('button', { name: 'Increase quantity of SmartFeeder One' }).click();
  await page.getByRole('button', { name: 'Add 2 SmartFeeder One to cart' }).click();
  await page.getByRole('link', { name: /Cart, 2 items/ }).click();

  await expect(page.getByRole('heading', { name: 'Your Cart' })).toBeVisible();
  const cartItem = page.locator('article').filter({ hasText: 'SmartFeeder One' });
  await expect(cartItem.locator('strong')).toHaveText('$194.98');

  await page.getByRole('button', { name: 'Increase SmartFeeder One quantity' }).click();
  await expect(cartItem.locator('strong')).toHaveText('$292.47');
  await page.reload();

  await expect(page.locator('article').filter({ hasText: 'SmartFeeder One' }).locator('strong')).toHaveText('$292.47');
  await page.getByRole('link', { name: 'Continue shopping' }).click();
  await expect(page.getByText('In cart: 3')).toBeVisible();
  await page.getByRole('link', { name: /Cart, 3 items/ }).click();
  await page.getByRole('button', { name: 'Remove SmartFeeder One from cart' }).click();
  await expect(page.getByRole('heading', { name: 'Your cart is empty' })).toBeVisible();
});