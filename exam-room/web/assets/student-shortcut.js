import {copyText,escapeHtml} from './common.js';

export function createStudentShortcut(canOffer,readShortcutRisk) {
  let card=null, deferredPrompt=null, installed=false, registrationField=null;
  const standalone=()=>navigator.standalone===true || (typeof window.matchMedia==='function'&&window.matchMedia('(display-mode: standalone)').matches);
  const allowed=()=>!installed&&!standalone()&&canOffer();
  const hide=()=>{if(card)card.remove();card=null;deferredPrompt=null;};
  window.addEventListener('beforeinstallprompt',event=>{
    // Keep browser installation opt-in, including when the event arrives after joining.
    event.preventDefault();
    deferredPrompt=event;
    if(card&&allowed())refreshRestriction();
  });
  window.addEventListener('appinstalled',()=>{installed=true;hide();});
  window.addEventListener('storage',()=>{
    if(!allowed()){hide();return;}
    if(registrationField&&document.documentElement.contains(registrationField))render(registrationField);
  });

  function refreshRestriction() {
    const risk=readShortcutRisk();
    const warning=card.querySelector('[data-shortcut-risk]');
    const wasBlocked=!warning.hidden;
    warning.textContent=risk;warning.hidden=!risk;
    card.querySelector('[data-shortcut-steps]').hidden=Boolean(risk);
    card.querySelector('[data-install-shortcut]').hidden=Boolean(risk)||!deferredPrompt;
    if(wasBlocked&&!risk){
      card.querySelector('#shortcut-instructions').hidden=true;
      card.querySelector('[data-shortcut-toggle]').setAttribute('aria-expanded','false');
    }
  }

  async function installFromBrowser() {
    if(!allowed()||!deferredPrompt){hide();return;}
    if(readShortcutRisk()){refreshRestriction();return;}
    const prompt=deferredPrompt, currentCard=card;
    deferredPrompt=null;
    const button=currentCard.querySelector('[data-install-shortcut]');
    const feedback=currentCard.querySelector('[data-shortcut-status]');
    button.hidden=true;
    try {
      await prompt.prompt();
      const choice=await prompt.userChoice;
      feedback.textContent=choice.outcome==='accepted'?'وافقت على طلب المتصفح. لو ظهرت أيقونة مسار، افتحها قبل تسجيل الدخول.':'أغلقت طلب الإضافة. يمكنك اتباع خطوات المتصفح أدناه لاحقًا، قبل تسجيل الدخول.';
    } catch {feedback.textContent='لم تكتمل نافذة الإضافة. استخدم خطوات المتصفح أدناه قبل تسجيل الدخول.';}
  }

  function render(beforeField) {
    if(!beforeField||!allowed()){hide();return;}
    registrationField=beforeField;
    if(card&&document.documentElement.contains(card)){refreshRestriction();return;}
    const root=location.origin+'/';
    card=document.createElement('section');card.className='student-shortcut';card.setAttribute('aria-label','اختصار مسار');
    card.innerHTML=`<button type="button" class="button secondary small" aria-expanded="false" aria-controls="shortcut-instructions" data-shortcut-toggle>إضافة مسار للشاشة الرئيسية</button>
      <div id="shortcut-instructions" hidden>
        <p role="status" data-shortcut-risk hidden></p>
        <div data-shortcut-steps>
        <p>اختياري: افتح مسار من أيقونة على موبايلك وأنت متصل بواي فاي الامتحان، بلا إنترنت.</p>
        <strong>أضف وافتح الاختصار قبل تسجيل الدخول، ثم كمّل في نفس النافذة حتى التسليم.</strong>
        <p>لو دي نافذة تسجيل دخول الواي فاي، انسخ الرابط وافتحه في Chrome أو Samsung Internet أو Safari أولًا.</p>
        <label>رابط مسار<input readonly dir="ltr" value="${escapeHtml(root)}" data-shortcut-url></label>
        <button type="button" class="button secondary small" data-copy-shortcut>نسخ رابط مسار</button>
        <a class="button secondary small" href="${escapeHtml(root)}" data-root-shortcut hidden>فتح رابط مسار الأساسي قبل الإضافة</a>
        <button type="button" class="button full" data-install-shortcut hidden>فتح نافذة الإضافة في المتصفح</button>
        <p role="status" data-shortcut-status></p>
        <details><summary>أندرويد · Chrome</summary><ol><li>افتح القائمة ⋮ ثم «تثبيت وإنشاء اختصار» ← «إنشاء اختصار». في بعض الإصدارات يظهر «إضافة إلى الشاشة الرئيسية» مباشرة.</li><li>سمِّ الاختصار «مسار»، ثم اضغط «إضافة».</li></ol></details>
        <details><summary>سامسونج · Samsung Internet</summary><ol><li>افتح قائمة المتصفح ☰ ثم «إضافة الصفحة إلى».</li><li>اختر «الشاشة الرئيسية»، وسمِّ الاختصار «مسار»، ثم اضغط «إضافة».</li></ol></details>
        <details><summary>آيفون · Safari</summary><ol><li>افتح «مشاركة»، ثم «إضافة إلى الشاشة الرئيسية».</li><li>سمِّه «مسار»، ثم اضغط «إضافة». إذا ظهر «فتح كتطبيق ويب»، يمكنك تفعيله.</li></ol></details>
        <p>أسماء الخيارات تختلف حسب الجهاز. لو خيار الإضافة مش ظاهر، افتح الرابط في المتصفح العادي؛ الإضافة بضغطة من الصفحة ليست متاحة في كل المتصفحات.</p>
        <p>الاختصار يحتاج واي فاي الامتحان وجهاز الإدارة شغالًا. لا يحمّل الامتحان على الموبايل للعمل بعيدًا عن الشبكة.</p>
        </div>
      </div>`;
    const toggle=card.querySelector('[data-shortcut-toggle]');
    toggle.onclick=()=>{
      if(!allowed()){hide();return;}
      refreshRestriction();
      const instructions=card.querySelector('#shortcut-instructions');
      if(instructions.hidden&&!readShortcutRisk()&&(location.pathname!=='/'||location.search||location.hash)){
        try {history.replaceState(history.state,'',root);}
        catch {
          card.querySelector('[data-root-shortcut]').hidden=false;
          card.querySelector('[data-shortcut-status]').textContent='افتح رابط مسار الأساسي أو انسخه إلى المتصفح قبل الإضافة، حتى لا يحفظ الاختصار بيانات من عنوان الصفحة.';
        }
      }
      instructions.hidden=!instructions.hidden;toggle.setAttribute('aria-expanded',String(!instructions.hidden));
    };
    card.querySelector('[data-root-shortcut]').onclick=event=>{
      if(!allowed()){event.preventDefault();hide();}
      else if(readShortcutRisk()){event.preventDefault();refreshRestriction();}
    };
    card.querySelector('[data-copy-shortcut]').onclick=async()=>{
      if(!allowed()){hide();return;}
      if(readShortcutRisk()){refreshRestriction();return;}
      const currentCard=card;
      try {await copyText(root);}
      catch(error){currentCard.querySelector('[data-shortcut-status]').textContent=error.message||'انسخ الرابط يدويًا من الحقل.';}
    };
    const install=card.querySelector('[data-install-shortcut]');
    install.hidden=!deferredPrompt;install.onclick=installFromBrowser;
    refreshRestriction();
    beforeField.parentNode.insertBefore(card,beforeField);
  }
  return {render,hide};
}
