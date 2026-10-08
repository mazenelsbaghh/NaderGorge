// The fake HTTP host is the network boundary: failures exercise browser retries.
const {test} = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require('../../../frontend/node_modules/playwright');
const assetRoot = path.resolve(__dirname, '../assets/mobile-homework');

test('mobile preview requires presence, clears stale selection and retries uncertain confirmation with same identity', async () => {
  const confirmed = [];
  const server = http.createServer((req, res) => {
    res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; media-src 'self' blob:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'");
    const file = {'/mobile/':'index.html','/mobile/logo.svg':'../logo.svg','/mobile/style.css':'style.css','/mobile/app.js':'app.js','/mobile/scanner.js':'scanner.js','/mobile/font.ttf':'../fonts/Tajawal-Regular.ttf'}[req.url];
    if (file) {
      res.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : file.endsWith('.html') ? 'text/html' : file.endsWith('.svg') ? 'image/svg+xml' : 'font/ttf');
      res.end(fs.readFileSync(path.join(assetRoot,file))); return;
    }
    res.setHeader('Content-Type','application/json');
    if (req.url === '/mobile/context') {res.end(JSON.stringify({group:'مجموعة تجريبية',session:'حصة ١',homework:'واجب الدرس الأول'}));return;}
    if (!['/mobile/lookup','/mobile/confirm'].includes(req.url)) {res.statusCode=404;res.end('{}');return;}
    let body=''; req.on('data',chunk=>body+=chunk);req.on('end',()=>{
      const request=JSON.parse(body);
      if (req.url==='/mobile/lookup') {
        res.end(JSON.stringify({id:request.code,name:request.code==='absent'?'طالب غير حاضر':'طالب تجربة',code:request.code,group:'مجموعة تجريبية',present:request.code!=='absent',missing:false,status:'اتعمل'}));return;
      }
      confirmed.push(request);
      if (confirmed.length===1) {res.statusCode=500;res.end(JSON.stringify({message:'تعذر تأكيد الحفظ'}));return;}
      res.end(JSON.stringify({saved:true}));
    });
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  let browser;
  try {
    browser=await chromium.launch({headless:true, ...(process.env.MASSAR_BROWSER_CHANNEL ? {channel:process.env.MASSAR_BROWSER_CHANNEL}: {})});
    const page=await browser.newPage({viewport:{width:360,height:800}});
    const errors=[];page.on('pageerror',error=>errors.push(error.message));
    await page.goto(`http://127.0.0.1:${server.address().port}/mobile/#fixture-token`);
    await page.waitForFunction(()=>document.getElementById('scope').textContent.includes('واجب الدرس الأول'));
    await page.fill('#code','absent');await page.click('#show');
    await page.waitForFunction(()=>document.getElementById('attendance').textContent==='غير حاضر في الحصة');
    assert.equal(await page.isDisabled('#confirm'),true);
    await page.fill('#code','00123');
    assert.equal(await page.isVisible('#student'),false);
    await page.click('#show');await page.waitForFunction(()=>!document.getElementById('confirm').disabled);
    const barcodePage = await browser.newPage({viewport:{width:640,height:200}});
    await barcodePage.setContent(fs.readFileSync(path.join(__dirname,'fixtures/mobile_barcode.svg'),'utf8'));
    const barcodePhoto = await barcodePage.screenshot(); await barcodePage.close();
    await page.setInputFiles('#photo', {name:'card.png', mimeType:'image/png', buffer:barcodePhoto});
    await page.waitForFunction(()=>document.getElementById('student-code').textContent==='CARD-00123');
    assert.equal(confirmed.length,0);
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
    if (process.env.MASSAR_MOBILE_SCREENSHOT) await page.screenshot({path:process.env.MASSAR_MOBILE_SCREENSHOT,fullPage:true});
    await page.click('#confirm');await page.waitForFunction(()=>document.getElementById('confirm').textContent.includes('إعادة'));
    assert.equal(await page.isDisabled('#code'),true);
    await page.reload();await page.waitForFunction(()=>document.getElementById('name').textContent.includes('تسجيل سابق'));
    await page.click('#confirm');await page.waitForFunction(()=>document.getElementById('notice').textContent.includes('تم التسجيل'));
    assert.equal(confirmed.length,2);assert.deepEqual(confirmed[0],confirmed[1]);
    assert.equal(await page.inputValue('#code'),'');
    assert.equal(await page.isDisabled('#show'),false);
    assert.deepEqual(errors,[]);
  } finally {if(browser) await browser.close();await new Promise(resolve=>server.close(resolve));}
});
