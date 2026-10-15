const {createRequire} = require('node:module');
const path = require('node:path');
const fs = require('node:fs/promises');
const os = require('node:os');
const {spawn} = require('node:child_process');
const readline = require('node:readline');
const assert = require('node:assert/strict');
const {chromium, expect} = createRequire(path.resolve(__dirname, '../../frontend/package.json'))('@playwright/test');

(async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'massar-wired-browser-'));
  const engine = spawn(process.argv[2], ['--center-integrated', '--data-dir', directory, '--port', '18775', '--admin-port', '18776']);
  engine.stderr.resume();
  const lines = readline.createInterface({input: engine.stdout});
  let browser;
  try {
    const ready = await Promise.race([
      new Promise((resolve, reject) => {
        lines.on('line', line => {try {const response=JSON.parse(line);if(response.ready)resolve(response);} catch {}});
        engine.on('exit', () => reject(new Error('Exam test engine exited before readiness.')));
      }),
      new Promise((_, reject) => {const timer=setTimeout(()=>reject(new Error('Engine readiness timeout.')),15000);timer.unref();}),
    ]);
    browser = await chromium.launch({headless:true, channel:'chrome'});
    const office = await browser.newContext();
    const admin = 'http://127.0.0.1:18776', student = 'http://127.0.0.1:18775';
    async function post(route, payload) {
      const response = await office.request.post(admin+route, {data:payload, headers:{'X-Admin-Token':ready.adminToken,'X-Exam-Request':'1'}});
      assert(response.ok(), 'Exam fixture setup failed: '+route+' '+await response.text());
      return response.json();
    }
    const template = await post('/api/templates', {title:'الامتحان الموجود',instructions:'',minutes:10,timerMode:'shared',allowLate:true,shuffle:false,questionCount:1,questions:[{kind:'essay',text:'اكتب إجابتك',points:10,modelAnswer:'إجابة'}]});
    const access = await post('/api/center/admin-access', {enabled:true});
    const page = await office.newPage();
    const errors=[];page.on('pageerror',error=>errors.push(error.message));
    await page.goto(access.adminUrl);
    await page.locator('[data-view="room"]').click();
    await expect(page.getByRole('heading', {name:'اختر الامتحان والمجموعة والحصة'})).toBeVisible();
    await expect(page.getByLabel('الحصة المجهّزة من السنتر')).not.toBeVisible();
    await page.getByLabel(/اسم الامتحان/).first().selectOption(template.id);
    await expect(page.getByText(/دليل الحصص غير موجود بعد/)).toBeVisible();
    await expect(page.getByRole('button', {name:'فتح قاعة السنتر ←', exact:true})).toBeEnabled();
    await post('/api/center/catalog', {hostId:'paired-host',contexts:[{groupId:'cached-group',sessionId:'actual-session',activityId:'actual-exam',groupLabel:'مجموعة التجربة',sessionLabel:'الشهر الثاني · الحصة الأولى',activityName:'امتحان الرصد',maxScore:10}]});
    await expect(page.getByRole('combobox', {name:'المجموعة',exact:true})).toBeVisible({timeout:10000});
    await page.getByRole('combobox', {name:'المجموعة',exact:true}).selectOption('cached-group');
    await page.getByRole('combobox', {name:'الحصة الفعلية',exact:true}).selectOption('actual-session');
    await page.getByRole('combobox', {name:'امتحان الرصد',exact:true}).selectOption('actual-exam');
    const openingRequest=page.waitForRequest(request=>request.url().endsWith('/api/room/open'));
    await page.getByRole('button', {name:'فتح قاعة السنتر ←', exact:true}).click();
    const opening=(await openingRequest).postDataJSON();
    assert.equal(opening.standalone,true);assert(opening.cachedContextId);assert.equal(opening.roster,undefined);
    await expect(page.getByRole('button', {name:'ابدأ الامتحان للجميع ←', exact:true})).toBeVisible();
    const mobile = await browser.newContext({viewport:{width:390,height:844}});
    const phone = await mobile.newPage();phone.on('pageerror',error=>errors.push(error.message));
    await phone.goto(student);
    await expect(phone.locator('#join-room-status')).toHaveText('القاعة مفتوحة');
    await expect(phone.locator('#join-room-title')).toHaveText('الامتحان الموجود');
    for (const [field,value] of Object.entries({name:'اسم كتبه الطالب',phone:'01112345678',code:'00123'})) {
      await expect(phone.locator(`[name=${field}]`)).toBeVisible();
      await phone.locator(`[name=${field}]`).fill(value);
    }
    const entryRequest = phone.waitForRequest(request=>request.url().endsWith('/api/join'));
    await phone.locator('#join-form button[type=submit]').click();
    const entry = (await entryRequest).postDataJSON();
    await expect(phone.locator('.student-wait')).toBeVisible();
    const saved = await (await mobile.request.get(student+'/api/session')).json();
    assert.equal(saved.session.name,'اسم كتبه الطالب');
    assert.equal(saved.session.code,'00123');
    const catalogResponse=await office.request.get(admin+'/api/center/standalone-rooms',{headers:{'X-Admin-Token':ready.adminToken,'X-Exam-Request':'1'}});
    assert(catalogResponse.ok());
    const catalog=await catalogResponse.json();
    assert.equal(catalog.rooms.length,1);
    assert.equal(catalog.rooms[0].attemptCount,1);
    assert.equal(catalog.rooms[0].target.groupId,'cached-group');
    assert.equal(catalog.rooms[0].target.sessionId,'actual-session');
    const exported = await post('/api/center/standalone-results', {examId:catalog.rooms[0].examId});
    assert.equal(exported.attempts[0].phone,'01112345678');
    assert.equal(entry.phone,'01112345678');
    assert.deepEqual(errors,[]);
    console.log('PASS: no-cache offline fallback stays available; automatic cached academic catalog supplies group/session/exam selectors without roster preparation; manual student entry and immutable transfer destination work.');
  } finally {
    if (browser) await browser.close();
    lines.close();
    const exited = new Promise(resolve=>engine.once('exit',resolve));
    if(engine.exitCode===null){engine.kill();await exited;}
    await fs.rm(directory,{recursive:true,force:true});
  }
})().catch(error=>{console.error(error);process.exitCode=1;});
