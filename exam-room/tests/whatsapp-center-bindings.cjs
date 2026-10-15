const {createRequire}=require('node:module');
const path=require('node:path');
const fs=require('node:fs/promises');
const os=require('node:os');
const {spawn}=require('node:child_process');
const readline=require('node:readline');
const assert=require('node:assert/strict');
const {chromium,expect}=createRequire(path.resolve(__dirname,'../../frontend/package.json'))('@playwright/test');
(async()=>{
 const directory=await fs.mkdtemp(path.join(os.tmpdir(),'massar-whatsapp-bindings-'));
 const engine=spawn(process.argv[2],['--center-integrated','--data-dir',directory,'--port','18975','--admin-port','18976']);
 engine.stderr.resume();const lines=readline.createInterface({input:engine.stdout});let browser;
 try{
  const ready=await Promise.race([
   new Promise((resolve,reject)=>{lines.on('line',line=>{try{const state=JSON.parse(line);if(state.ready)resolve(state);}catch{}});engine.on('exit',()=>reject(new Error('Engine exited before readiness')));}),
   new Promise((_,reject)=>{const timer=setTimeout(()=>reject(new Error('Engine readiness timeout')),15000);timer.unref();}),
  ]);
  browser=await chromium.launch({headless:true,channel:'chrome'});
  const office=await browser.newContext(),admin='http://127.0.0.1:18976';
  const response=await office.request.post(admin+'/api/center/admin-access',{data:{enabled:true},headers:{'X-Admin-Token':ready.adminToken,'X-Exam-Request':'1'}});
  assert(response.ok());const access=await response.json();
  const page=await office.newPage(),errors=[];page.on('pageerror',error=>errors.push(error.message));
  const template={id:'parent',name:'parent_follow_center',language:'ar',reportSupported:true,bindingSchema:'revision-one',savedBindings:[],components:[{type:'HEADER',format:'TEXT',text:'ولي أمر {{1}}'},{type:'BODY',text:'الطالب {{1}} حضر المحاضرة {{2}} في {{3}} بسنتر {{4}} الساعة {{5}} جاب {{6}} من {{7}} و{{8}} الواجب'}]};
  const shared={group:'مجموعة التجربة',lesson:'الأولى',month:'الثاني',center:'سنتر التجربة',time:'18:00'};
  const records=[{...shared,id:'paper',name:'طالب ورقي',code:'001',phone:'01012345678',guardianPhone:'01112345678',homework:'كامل',attendance:'حاضر',score:8,maximum:10},{...shared,id:'online',name:'طالب إلكتروني',code:'002',phone:'01012345679',guardianPhone:'01112345679',homework:'لم يعمل',attendance:'حاضر',score:9,maximum:12},{...shared,id:'absent',name:'طالب غائب',code:'003',phone:'01012345670',guardianPhone:'01112345670',homework:'لم يُراجع',attendance:'غائب',score:null,maximum:null}];
  let sends=0;
  await page.route('**/api/whatsapp/**',async route=>{
   const request=route.request(),pathname=new URL(request.url()).pathname,body=request.method()==='POST'?request.postDataJSON():null;
   const json=value=>route.fulfill({json:value});
   if(pathname.endsWith('/configuration'))return json({configured:true,phoneNumberId:'123456',businessAccountId:'654321',apiVersion:'v23.0',tokenConfigured:true});
   if(pathname.endsWith('/templates'))return json({templates:[template]});
   if(pathname.endsWith('/history')||pathname.endsWith('/jobs')){if(body)sends++;return json({items:[]});}
   if(pathname.includes('/center/batches/'))return json({id:pathname.split('/').at(-1),title:'سنتر التجربة · الحصة الأولى',messageKind:'followup',records});
   if(pathname.endsWith('/template-bindings')){const saved={bindingSchema:body.bindingSchema,recipientKind:body.recipientKind,reportKind:body.reportKind,mappings:body.mappings};template.savedBindings=[...template.savedBindings.filter(value=>value.recipientKind!==saved.recipientKind||value.reportKind!==saved.reportKind),saved];return json(saved);}
   if(pathname.endsWith('/reports/preview'))return json({items:body.attemptIds.map(id=>{const row=records.find(value=>value.id===id);if(row.attendance==='غائب')return {...row,attemptId:id,error:'قالب الحضور لا يطابق حالة الطالب؛ اختر قالب الغياب'};return {...row,attemptId:id,phone:body.recipientKind==='guardian'?row.guardianPhone:row.phone,parameters:body.mappings.map(mapping=>mapping.source==='literal'?mapping.text:mapping.source==='homework'?({كامل:'عمل',ناقص:'عمل جزءًا من','لم يُراجع':'لم تُراجع حالة','لم يحدد واجب':'لم يُحدد',معفى:'معفى من'}[row.homework]||row.homework):String(row[mapping.source])),fingerprint:'synthetic:'+id};})});
   throw new Error('Unexpected WhatsApp boundary: '+pathname);
  });
  const url=new URL(access.adminUrl);for(const [key,value] of Object.entries({view:'whatsapp-center',embedded:'center',theme:'light',batch:'first'}))url.searchParams.set(key,value);
  await page.goto(url.href);url.hash="";
  await expect(page.getByRole('heading',{name:'رسائل واتساب',exact:true})).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByLabel('قالب رسالة التقرير').selectOption('parent');
  assert.deepEqual(await page.locator('.whatsapp-fields select').evaluateAll(elements=>elements.map(element=>element.value)),['name','name','lesson','month','center','time','score','maximum','homework']);
  await page.getByRole('button',{name:'تثبيت ربط المتغيرات',exact:true}).click();
  await expect(page.getByRole('button',{name:'ربط المتغيرات مثبت',exact:true})).toBeDisabled();
  await page.getByRole('button',{name:/^معاينة الرسائل/}).click();
  await expect(page.locator('.whatsapp-message').filter({hasText:'جاب 8 من 10 وعمل الواجب'})).toBeVisible();
  await expect(page.locator('.whatsapp-message').filter({hasText:'جاب 9 من 12 ولم يعمل الواجب'})).toBeVisible();
  await expect(page.getByRole('cell').filter({hasText:'مستبعد من الإرسال'})).toBeVisible();
  await expect(page.getByRole('button',{name:'إرسال الرسائل الجديدة (٢)',exact:true})).toBeVisible();
  const output=process.argv[3];if(output)await fs.mkdir(output,{recursive:true});
  for(const width of [1280,720]){
   await page.setViewportSize({width,height:900});
   const panels=page.locator('main>section.padded');
   assert.equal(await panels.count(),2);
   for(const panel of await panels.all()){
    const spacing=await panel.evaluate(element=>{const style=getComputedStyle(element),bounds=element.getBoundingClientRect();return {top:parseFloat(style.paddingTop),right:parseFloat(style.paddingRight),left:parseFloat(style.paddingLeft),width:bounds.width};});
    assert(spacing.top>=16&&spacing.right>=16&&spacing.left>=16,'Center form content must be inset from its border');
   }
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Center WhatsApp must not overflow at narrow widths');
   if(output)await page.screenshot({path:path.join(output,`whatsapp-inline-form-${width}.png`),fullPage:true});
  }
  await page.setViewportSize({width:1280,height:900});
  await page.getByLabel('المستلم').selectOption('student');
  await expect(page.getByRole('button',{name:/^إرسال الرسائل الجديدة/})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'تثبيت ربط المتغيرات',exact:true})).toBeEnabled();
  await page.getByLabel('المستلم').selectOption('guardian');
  await expect(page.getByRole('button',{name:'ربط المتغيرات مثبت',exact:true})).toBeDisabled();
  url.searchParams.set('batch','second');await page.goto(url.href);
  await page.getByLabel('قالب رسالة التقرير').selectOption('parent');
  await expect(page.getByRole('button',{name:'ربط المتغيرات مثبت',exact:true})).toBeDisabled();
  await expect(page.getByText('ربط المتغيرات مثبت · تعديل الربط',{exact:true})).toBeVisible();
  const settingsUrl=new URL(await page.getByRole('link',{name:'إعداد حساب واتساب والقوالب'}).getAttribute('href'),url);
  assert.equal(settingsUrl.searchParams.get('embedded'),'center');assert.equal(settingsUrl.searchParams.get('theme'),'light');
  await page.goto(settingsUrl.href);
  await expect(page.getByRole('heading',{name:'الحساب الرسمي',exact:true})).toBeVisible();
  for(const panel of await page.locator('section.whatsapp-settings').all())assert(await panel.evaluate(element=>parseFloat(getComputedStyle(element).paddingRight)>=16));
  await page.getByRole('link',{name:'العودة لإرسال تقرير السنتر'}).click();
  await page.getByLabel('قالب رسالة التقرير').selectOption('parent');
  template.bindingSchema='revision-two';template.components[1].text+=' نهاية معدلة';
  await page.reload();await page.getByLabel('قالب رسالة التقرير').selectOption('parent');
  await expect(page.getByRole('button',{name:'تثبيت ربط المتغيرات',exact:true})).toBeEnabled();
  records.forEach(row=>{row.attendance='غائب';});
  await page.reload();await page.getByLabel('قالب رسالة التقرير').selectOption('parent');
  await page.getByRole('button',{name:/^معاينة الرسائل/}).click();
  await expect(page.getByRole('button',{name:/^إرسال الرسائل الجديدة/})).toBeDisabled();
  await expect(page.getByRole('cell').filter({hasText:'مستبعد من الإرسال'})).toHaveCount(3);
  assert.equal(sends,0);assert.deepEqual(errors,[]);
  if(output)await page.screenshot({path:path.join(output,'whatsapp-all-rejected.png'),fullPage:true});
  console.log('PASS: inline form, one-time scoped bindings, actual /10 and /12, rejected absent rows, fresh preview after recipient change, persistence across batches, schema invalidation, embedded links; no messages sent.');
 }finally{await browser?.close();lines.close();engine.kill('SIGTERM');await fs.rm(directory,{recursive:true,force:true});}
})().catch(error=>{console.error(error);process.exitCode=1;});
