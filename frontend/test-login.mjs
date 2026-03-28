import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true }); // Watch it natively! 
  const page = await browser.newPage();
  
  page.on('console', msg => console.log('BROWSER LOG:', msg.text()));
  
  await page.goto('http://localhost:3000/login');
  
  await page.waitForSelector('form');
  // Expose a quick intercept
  await page.evaluate(() => {
    document.querySelector('form').addEventListener('submit', (e) => {
      console.log('FORM SUBMITTED');
    });
  });

  await page.fill('input[name="phoneNumber"]', '20000000000');
  await page.fill('input[name="password"]', 'password');
  
  // Try evaluating the click directly to bypass playwright magic
  await page.evaluate(() => {
    document.querySelector('button[type="submit"]').click();
  });
  
  await page.waitForTimeout(3000);
  console.log('Final URL:', page.url());

  const err = await page.evaluate(() => document.querySelector('.auth-error-banner')?.textContent);
  console.log('Error Banner:', err);
  
  await browser.close();
})();
