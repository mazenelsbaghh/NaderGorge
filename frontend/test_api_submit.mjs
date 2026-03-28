import { chromium } from 'playwright';
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  
  page.on('request', request => {
    if (request.url().includes('/api/auth/register')) {
      console.log('REQUEST POST DATA:', request.postData());
    }
  });

  await page.goto('http://localhost:3000/register');
  
  const randomSuffix = Math.floor(10 + Math.random() * 90);
  await page.fill('input[name="fullName"]', `Student ${randomSuffix} test test`, {force:true});
  await page.fill('input[name="studentCode"]', 'STU' + randomSuffix, {force:true});
  await page.fill('input[name="phoneNumber"]', '015' + '123456' + randomSuffix, {force:true});
  await page.fill('input[name="dateOfBirth"]', '2005-06-15', {force:true});
  await page.selectOption('select[name="gender"]', 'Male', {force:true});
  await page.selectOption('select[name="governorate"]', 'القاهرة', {force:true});
  await page.fill('input[name="address"]', '123 Test St', {force:true});
  await page.click('button:has-text("التالي")', {force:true});
  
  await page.fill('input[name="parentPhone"]', '010123456' + randomSuffix, { force: true });
  await page.click('button:has-text("التالي")', { force: true });
  
  await page.selectOption('select[name="educationStage"]', 'Secondary', { force: true });
  await page.selectOption('select[name="gradeLevel"]', 'FirstSecondary', { force: true });
  await page.click('button:has-text("التالي")', { force: true });
  
  await page.fill('input[name="password"]', 'TestPass123!', { force: true });
  await page.fill('input[name="confirmPassword"]', 'TestPass123!', { force: true });
  await page.click('button[type="submit"]', {force: true});
  
  await page.waitForTimeout(4000);
  await browser.close();
})();
