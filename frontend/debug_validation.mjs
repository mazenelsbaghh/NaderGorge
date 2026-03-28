import { chromium } from 'playwright';
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  await page.goto('http://localhost:3000/register');
  
  await page.fill('input[name="fullName"]', `Student 42 test test`);
  await page.fill('input[name="studentCode"]', 'STU42');
  await page.fill('input[name="phoneNumber"]', '01512345678');
  await page.fill('input[name="dateOfBirth"]', '2005-06-15');
  await page.selectOption('select[name="gender"]', 'Male');
  await page.selectOption('select[name="governorate"]', 'القاهرة');
  await page.fill('input[name="address"]', '123 Test St');
  await page.click('button:has-text("التالي")'); // NO FORCE
  
  await page.waitForTimeout(2000);
  const errors = await page.$$eval('.auth-field-error', els => els.map(e => e.textContent));
  const isStep2 = await page.isVisible('input[name="parentPhone"]');
  console.log("Errors: ", errors);
  console.log("Is Step 2 visible:", isStep2);
  await browser.close();
})();
