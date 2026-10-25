/**
 * Browser-level accessibility checks. Automated scans catch roughly a third of WCAG issues;
 * the keyboard journeys below cover behaviour axe can't see (focus order, focus return, announcements).
 */
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

async function expectNoAxeViolations(page, include) {
  let builder = new AxeBuilder({ page }).withTags(WCAG_TAGS);
  if (include) builder = builder.include(include);
  const { violations } = await builder.analyze();
  expect(violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target).join(', ')}`)).toEqual([]);
}

test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('#requests-body tr').first()).toBeVisible();
});

test.describe('automated scans (axe-core, WCAG 2.2 AA)', () => {
  test('home page, light theme', async ({ page }) => {
    await expectNoAxeViolations(page);
  });

  test('home page, dark theme', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await expectNoAxeViolations(page);
  });

  test('form showing validation errors', async ({ page }) => {
    await page.getByRole('button', { name: 'Submit request' }).click();
    await expect(page.getByRole('heading', { name: 'There is a problem' })).toBeVisible();
    await expectNoAxeViolations(page);
  });

  test('request details dialog', async ({ page }) => {
    await page.locator('.row-link').first().click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expectNoAxeViolations(page, '#details-dialog');
  });
});

test.describe('keyboard journeys', () => {
  test('skip link is the first stop and moves focus to main content', async ({ page }) => {
    await page.keyboard.press('Tab');
    await expect(page.getByRole('link', { name: 'Skip to main content' })).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.locator('#main')).toBeFocused();
  });

  test('failed submit focuses the error summary, and its links focus the field', async ({ page }) => {
    await page.getByRole('button', { name: 'Submit request' }).click();
    await expect(page.locator('#error-summary')).toBeFocused();
    await expect(page).toHaveTitle(/^Error: /);

    await page.getByRole('link', { name: 'Enter a subject.' }).click();
    await expect(page.getByLabel('Subject')).toBeFocused();
    await expect(page.getByLabel('Subject')).toHaveAttribute('aria-invalid', 'true');
  });

  test('details dialog opens on its title and Escape returns focus to the row', async ({ page }) => {
    const trigger = page.locator('.row-link').first();
    await trigger.focus();
    await page.keyboard.press('Enter');
    await expect(page.locator('#details-title')).toBeFocused();

    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(page.locator('.row-link').first()).toBeFocused();
  });

  test('sorting exposes aria-sort and announces the change', async ({ page }) => {
    await page.getByRole('button', { name: 'Priority' }).click();
    await expect(page.locator('th[data-sort-key="priority"]')).toHaveAttribute('aria-sort', 'descending');
    await expect(page.locator('#announcer-polite')).toHaveText('Sorted by Priority, descending.');
  });

  test('submitting a valid request moves focus to the confirmation', async ({ page }) => {
    await page.getByLabel('Subject').fill('Keyboard trap in signing modal');
    await page.getByLabel('Category').selectOption('Accessibility barrier');
    await page.getByRole('radio', { name: 'High' }).check();
    await page.getByLabel('What happened?').fill('Focus cannot leave the signing modal using Tab or Escape in Edge.');
    await page.getByLabel('Full name').fill('Alex Morgan');
    await page.getByLabel('Email address').fill('alex@example.com');
    await page.getByRole('button', { name: 'Submit request' }).click();

    await expect(page.locator('#submission-banner')).toBeFocused();
    await expect(page.locator('#submission-title')).toHaveText(/^Request SR-\d{6} submitted$/);
  });
});

test.describe('reflow and preferences', () => {
  test('no horizontal page scrolling at 320 CSS px (WCAG 1.4.10)', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 800 });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });

  test('reduced motion disables animation', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.locator('.row-link').first().click();
    const duration = await page.locator('#details-dialog').evaluate((el) => getComputedStyle(el).animationDuration);
    expect(parseFloat(duration)).toBeLessThan(0.01);
  });
});
