const {createRequire}=require('node:module');
const path=require('node:path');
const fs=require('node:fs/promises');
const os=require('node:os');
const {spawn}=require('node:child_process');
const readline=require('node:readline');
const assert=require('node:assert/strict');
const {chromium,expect}=createRequire(path.resolve(__dirname,'../../frontend/package.json'))('@playwright/test');

(async()=>{
 const directory=await fs.mkdtemp(path.join(os.tmpdir(),'massar-embedded-ui-'));
 const engine=spawn(process.argv[2],['--center-integrated','--data-dir',directory,'--port','18875','--admin-port','18876']);
 engine.stderr.resume();let browser;
 const lines=readline.createInterface({input:engine.stdout});
 try {
  const ready=await Promise.race([
   new Promise((resolve,reject)=>{lines.on('line',line=>{try{const response=JSON.parse(line);if(response.ready)resolve(response);}catch{}});engine.on('exit',()=>reject(new Error('Engine exited before readiness')));}),
   new Promise((_,reject)=>{const timer=setTimeout(()=>reject(new Error('Engine readiness timeout')),15000);timer.unref();}),
  ]);
  browser=await chromium.launch({headless:true,channel:'chrome'});
  const office=await browser.newContext();
  const admin='http://127.0.0.1:18876';
  const post=async(route,payload)=>{
   const response=await office.request.post(admin+route,{data:payload,headers:{'X-Admin-Token':ready.adminToken,'X-Exam-Request':'1'}});
   assert(response.ok(),await response.text());return response.json();
  };
  const template=await post('/api/templates',{title:'امتحان الحصة الأولى',instructions:'',minutes:10,timerMode:'shared',allowLate:true,shuffle:false,questionCount:1,questions:[{kind:'essay',text:'اكتب إجابتك',points:12,modelAnswer:'إجابة'}]});
  await post('/api/center/catalog',{hostId:'paired-host',contexts:[{groupId:'cached-group',sessionId:'actual-session',activityId:'actual-exam',groupLabel:'الخميس ٦ · الأكاديمية سموحة',sessionLabel:'الشهر الثاني · الحصة الأولى',activityName:'امتحان الحصة الأولى',maxScore:10,allowedMaxScores:[10,12]}]});
  const access=await post('/api/center/admin-access',{enabled:true});
  await office.addInitScript(()=>{
   window.__massarCenterNavigationNonce='a'.repeat(48);
   window.nativeMessages=[];window.nativeListeners=new Set();window.nativeRequest=0;
   window.chrome=window.chrome||{};
   window.chrome.webview={postMessage:message=>window.nativeMessages.push(message),addEventListener:(_,listener)=>window.nativeListeners.add(listener),removeEventListener:(_,listener)=>window.nativeListeners.delete(listener)};
   window.sendNative=(action,destination)=>{const ready=window.nativeMessages.findLast(message=>message.action==='ready');const requestId=++window.nativeRequest;const message={kind:'massar-center-navigation',nonce:ready.nonce,documentId:ready.documentId,requestId,action,destination};for(const listener of window.nativeListeners)listener({data:message});return requestId;};
  });
  const page=await office.newPage(),errors=[];page.on('pageerror',error=>errors.push(error.message));
  const url=new URL(access.adminUrl);url.searchParams.set('embedded','center');url.searchParams.set('native-shell','1');
  await page.goto(url.href);
  await expect(page.getByRole('heading',{name:'إدارة الامتحانات',exact:true})).toBeVisible();
  await expect.poll(()=>page.evaluate(()=>window.nativeMessages.some(message=>message.action==='ready'))).toBe(true);
  const navigate=async destination=>{const requestId=await page.evaluate(destination=>window.sendNative('navigate',destination),destination);await expect.poll(()=>page.evaluate(requestId=>window.nativeMessages.some(message=>message.action==='state'&&message.requestId===requestId),requestId)).toBe(true);};
  await page.evaluate(()=>document.fonts.ready);
  assert.equal(await page.evaluate(()=>document.fonts.check('15px Tajawal')),true);
  const output=process.argv[3]||directory;await fs.mkdir(output,{recursive:true});
  async function capture(stage,width,mode){
   await page.setViewportSize({width,height:800});await page.evaluate(mode=>window.applyMassarCenterTheme({mode}),mode);
   if(await page.locator('#toast .toast-close').isVisible())await page.locator('#toast .toast-close').click();
   await page.evaluate(()=>document.activeElement?.blur());
   await expect(page.locator('.sidebar')).toBeHidden();await expect(page.locator('.topbar')).toBeHidden();
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'No horizontal page overflow');
   await page.screenshot({path:path.join(output,`exam-${stage}-${mode}-${width}.png`),fullPage:true});
  }
  for(const width of [1280,720,500])for(const mode of ['light','dark'])await capture('setup',width,mode);
  await navigate('exams');
  await page.getByRole('button',{name:/امتحان الحصة الأولى/}).click();
  await page.locator('[name=title]').fill('مسودة محفوظة في الشاشة');
  const cancelled=await page.evaluate(()=>window.sendNative('navigate','room'));
  await expect(page.locator('#confirm-dialog')).toBeVisible();await page.locator('#confirm-dialog [value=cancel]').click();
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.requestId===id&&message.action==='state'&&message.destination==='exams'),cancelled)).toBe(true);
  await expect(page.locator('[name=title]')).toHaveValue('مسودة محفوظة في الشاشة');
  const accepted=await page.evaluate(()=>window.sendNative('navigate','room'));
  await expect(page.locator('#confirm-dialog')).toBeVisible();await page.locator('#confirm-dialog [value=yes]').click();
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.requestId===id&&message.action==='state'&&message.destination==='room'),accepted)).toBe(true);
  await navigate('exams');await page.getByRole('button',{name:/امتحان الحصة الأولى/}).click();
  await page.locator('[name=title]').fill('اسم مؤقت للاختبار');await page.locator('[name=title]').fill('امتحان الحصة الأولى');
  let releaseSave,saveEntered=false;
  await page.route('**/api/templates/*/save',async route=>{saveEntered=true;await new Promise(resolve=>releaseSave=resolve);await route.continue();});
  await page.getByRole('button',{name:'حفظ الامتحان',exact:true}).click();await expect.poll(()=>saveEntered).toBe(true);
  const busyLeave=await page.evaluate(()=>window.sendNative('leave'));
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.requestId===id&&message.action==='leave-result'&&message.allowed===false),busyLeave)).toBe(true);
  releaseSave();await expect(page.getByRole('heading',{name:'مكتبة الامتحانات',exact:true})).toBeVisible();
  await page.unroute('**/api/templates/*/save');
  const finishedLeave=await page.evaluate(()=>window.sendNative('leave'));
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.requestId===id&&message.action==='leave-result'&&message.allowed===true),finishedLeave)).toBe(true);
  await navigate('room');
  await page.getByRole('combobox',{name:'اسم الامتحان',exact:true}).selectOption(template.id);
  await page.getByRole('combobox',{name:'المجموعة',exact:true}).selectOption('cached-group');
  await page.getByRole('combobox',{name:'الحصة الفعلية',exact:true}).selectOption('actual-session');
  await page.getByRole('combobox',{name:'امتحان الرصد',exact:true}).selectOption('actual-exam');
  await page.getByRole('button',{name:'فتح قاعة السنتر ←',exact:true}).click();
  await expect(page.getByRole('button',{name:'متابعة الطلاب',exact:true})).toHaveAttribute('aria-pressed','true');
  await expect(page.getByRole('button',{name:'ابدأ الامتحان للجميع ←',exact:true})).toBeVisible();
  await expect(page.locator('.center-room-context')).toContainText('الخميس ٦ · الأكاديمية سموحة');
  const links=page.locator('.center-room-link');
  await expect(links.locator('.qr')).toBeHidden();await links.locator('summary').click();await expect(links.locator('.qr')).toBeVisible();await links.locator('summary').click();
  const mobile=await browser.newContext({viewport:{width:390,height:844}});const phone=await mobile.newPage();
  await phone.goto('http://127.0.0.1:18875');
  for(const [field,value] of Object.entries({name:'طالب اختبار الفلاتر',phone:'01112345678',code:'00123'}))await phone.locator(`[name=${field}]`).fill(value);
  await phone.locator('#join-form button[type=submit]').click();await expect(phone.locator('.student-wait')).toBeVisible();
  await expect(page.getByRole('table').getByText('طالب اختبار الفلاتر')).toBeVisible({timeout:10000});
  await page.getByRole('button',{name:/^تم التسليم:/}).click();await expect(page.getByRole('table').getByText('طالب اختبار الفلاتر')).toBeHidden();
  await page.getByRole('button',{name:/^الكل:/}).click();await expect(page.getByRole('table').getByText('طالب اختبار الفلاتر')).toBeVisible();
  for(const width of [1280,720,500])for(const mode of ['light','dark']){
   await capture('monitor',width,mode);
   assert.equal(await page.locator('.center-roster-filters>div>.filter-chip:visible').count(),4);
   await expect(page.locator('.center-extra-filters')).not.toHaveAttribute('open','');
   const bounds=await page.getByRole('table').evaluate(element=>element.getBoundingClientRect().top);
   if(width>=720)assert(bounds<850,'The students table follows the compact controls');
  }
  await page.locator('.center-extra-filters>summary').click();await expect(page.getByRole('button',{name:/^في الانتظار:/})).toBeVisible();await page.locator('.center-extra-filters>summary').click();
  await page.locator('.center-student-actions>summary').click();await page.getByRole('button',{name:'إلغاء المحاولة',exact:true}).click();
  await page.getByRole('dialog').getByLabel('سبب الإلغاء').fill('سبب قيد المراجعة');
  const dialogLeave=await page.evaluate(()=>window.sendNative('leave'));
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.requestId===id&&message.action==='leave-result'&&message.allowed===false),dialogLeave)).toBe(true);
  await page.getByRole('button',{name:'التصحيح والرصد',exact:true}).evaluate(button=>button.click());
  await expect(page.getByRole('button',{name:'متابعة الطلاب',exact:true})).toHaveAttribute('aria-pressed','true');
  await expect(page.getByRole('dialog').getByLabel('سبب الإلغاء')).toHaveValue('سبب قيد المراجعة');
  await page.getByRole('dialog').getByRole('button',{name:'رجوع',exact:true}).click();
  await page.getByRole('button',{name:'ابدأ الامتحان للجميع ←',exact:true}).click();await page.locator('#confirm-dialog [value=yes]').click();
  await expect(phone.locator('.student-exam-heading')).toBeVisible({timeout:10000});
  const session=(await(await mobile.request.get('http://127.0.0.1:18875/api/session')).json()).session;
  const question=session.questions[0];
  const answer=await mobile.request.post('http://127.0.0.1:18875/api/answers',{data:{revision:1,answers:{[question.id]:'إجابة مكتوبة للاختبار'},submit:true},headers:{'X-Exam-Request':'1'}});assert(answer.ok(),await answer.text());
  await page.getByRole('button',{name:'إنهاء الجلسة',exact:true}).click();await page.locator('#confirm-dialog [value=yes]').click();
  await expect(page.getByRole('button',{name:'التصحيح والرصد',exact:true})).toHaveAttribute('aria-pressed','true');
  await expect(page.getByRole('button',{name:'مراجعة وتصحيح ←'})).toBeVisible();
  await page.getByRole('button',{name:'مراجعة وتصحيح ←'}).click();
  await page.locator('.grade-form [name=score]').fill('10');await page.locator('.grade-form [name=feedback]').fill('تصحيح يدوي محفوظ');
  const reviewNavigation=await page.evaluate(()=>window.sendNative('navigate','exams'));
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.action==='state'&&message.requestId===id&&message.destination==='room'),reviewNavigation)).toBe(true);
  await page.getByRole('button',{name:'متابعة الطلاب',exact:true}).click();
  await expect(page.getByRole('button',{name:'التصحيح والرصد',exact:true})).toHaveAttribute('aria-pressed','true');
  await expect(page.locator('.grade-form [name=score]')).toHaveValue('10');await expect(page.locator('.grade-form [name=feedback]')).toHaveValue('تصحيح يدوي محفوظ');
  const reviewLeave=await page.evaluate(()=>window.sendNative('leave'));
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.requestId===id&&message.action==='leave-result'&&message.allowed===false),reviewLeave)).toBe(true);
  let releaseGrade,gradeEntered=false;
  await page.route('**/api/attempts/*/grade',async route=>{gradeEntered=true;await new Promise(resolve=>releaseGrade=resolve);await route.continue();});
  await page.getByRole('button',{name:'حفظ الدرجة',exact:true}).click();
  await expect.poll(()=>gradeEntered).toBe(true);
  await expect(page.locator('#review-panel').getByRole('button',{name:'إغلاق',exact:true})).toBeDisabled();
  const pendingGradeLeave=await page.evaluate(()=>window.sendNative('leave'));
  await expect.poll(()=>page.evaluate(id=>window.nativeMessages.some(message=>message.requestId===id&&message.action==='leave-result'&&message.allowed===false),pendingGradeLeave)).toBe(true);
  releaseGrade();
  await expect(page.locator('#review-panel').getByRole('button',{name:'إغلاق',exact:true})).toBeEnabled();
  await page.unroute('**/api/attempts/*/grade');
  await expect(page.locator('.grade-form [name=score]')).toHaveValue('10');
  const saved=await (await office.request.get(admin+`/api/exams/${session.examId}`)).json();assert.equal(saved.attempts[0].score,10);assert.equal(saved.attempts[0].maximum,12);assert.equal(saved.attempts[0].pending,0);
  const beforeExport=page.url();
  await page.evaluate(()=>{window.savedDownloads=[];window.pywebview={api:{download_exam_file:async(path,name,type)=>{window.savedDownloads.push({path,name,type});return{saved:true};}}};});
  await page.getByRole('button',{name:'تنزيل تقرير الطالب PDF',exact:true}).click();
  await expect.poll(()=>page.evaluate(()=>window.savedDownloads.length)).toBe(1);
  const exported=await page.evaluate(()=>window.savedDownloads[0]);assert.equal(exported.path,`/api/attempts/${session.id}/report.pdf`);assert.equal(exported.type,'application/pdf');assert.equal(page.url(),beforeExport);
  assert.equal(await page.locator('#review-panel a[href^="/report/"]').count(),0);
  await page.locator('#review-panel').getByRole('button',{name:'إغلاق',exact:true}).click();
  await navigate('exams');await expect(page.getByRole('heading',{name:'مكتبة الامتحانات',exact:true})).toBeVisible();await navigate('room');
  for(const width of [1280,720,500])for(const mode of ['light','dark'])await capture('grades',width,mode);
  await page.locator('.center-batch-grading>summary').click();await expect(page.getByRole('heading',{name:'المقالي لكل الطلاب'})).toBeVisible();await page.locator('.center-batch-grading>summary').click();
  await page.locator('.center-result-exports>summary').click();await expect(page.getByRole('button',{name:/تنزيل تقارير PDF/})).toBeVisible();await page.locator('.center-result-exports>summary').click();
  assert.deepEqual(errors,[]);
  const ordinary=await office.newPage();const ordinaryAccess=await post('/api/center/admin-access',{enabled:true});await ordinary.goto(ordinaryAccess.adminUrl);await expect(ordinary.locator('.sidebar')).toBeVisible();await expect(ordinary.locator('.exam-steps')).toBeVisible();assert.equal(await ordinary.locator('.center-exam-stages').count(),0);await ordinary.locator('[data-view=exams]').click();await expect(ordinary.getByRole('heading',{name:'مكتبة الامتحانات',exact:true})).toBeVisible();await ordinary.getByRole('button',{name:/امتحان الحصة الأولى/}).click();await expect(ordinary.getByRole('heading',{name:'تعديل الامتحان',exact:true})).toBeVisible();await ordinary.getByRole('button',{name:'رجوع للمكتبة',exact:true}).click();await expect(ordinary.getByRole('heading',{name:'مكتبة الامتحانات',exact:true})).toBeVisible();await ordinary.locator('[data-view=room]').click();await expect(ordinary.locator('.exam-steps')).toBeVisible();
  const student=await mobile.newPage();await student.goto('http://127.0.0.1:18875/?embedded=center');assert.equal(await student.evaluate(()=>window.applyMassarCenterTheme),undefined);assert.equal(await student.locator('body').evaluate(element=>element.classList.contains('center-embedded')),false);
  console.log('PASS: native three-stage exam, setup/monitor/grades light-dark at 1280/720/500, real room entry/start/submission/manual10of12, filters, QR, advanced controls, dirty/busy/review/dialog native-navigation and stage guards, native PDF export keeps shell, ordinary browser and student isolation.');
 }finally{
  await browser?.close();lines.close();
  if(engine.exitCode===null&&engine.signalCode===null){const stopped=new Promise(resolve=>engine.once('exit',resolve));engine.kill('SIGTERM');await stopped;}
  await fs.rm(directory,{recursive:true,force:true});
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
