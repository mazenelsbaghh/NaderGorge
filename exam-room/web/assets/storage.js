import {$, api, escapeHtml as e, number, toast, copyText} from './common.js';

const size = bytes => bytes >= 1024 ** 3 ? `${number(bytes / 1024 ** 3)} GB` : bytes >= 1024 ** 2 ? `${number(bytes / 1024 ** 2)} MB` : `${number(bytes / 1024)} KB`;
const date = seconds => new Date(seconds * 1000).toLocaleString('ar-EG', {dateStyle:'medium', timeStyle:'short'});

export async function renderStorage(container, token) {
  container.innerHTML = '<div class="loading" role="status">جارٍ قراءة بيانات التخزين…</div>';
  const loading = container.firstElementChild;
  try {
    const [details, gemini] = await Promise.all([api('/api/storage'), api('/api/settings/gemini')]);
    if (!loading.isConnected) return;
    container.innerHTML = `<div class="page-heading"><div><span class="context-label">كل شيء على جهازك</span><h1>الإعدادات والبيانات</h1><p>مفتاح التصحيح وبيانات الامتحانات محفوظة على هذا الكمبيوتر.</p></div><button class="button" id="create-backup">إنشاء نسخة احتياطية</button></div>
      <section class="panel gemini-settings"><div class="section-top"><div><span class="context-label">إعدادات التصحيح</span><h2>مفتاح Gemini API</h2><p class="help">أدخل المفتاح مرة واحدة لتشغيل تصحيح المقالي للجميع بعد انتهاء الامتحان.</p></div><span class="badge ${gemini.configured ? 'complete' : 'quiet'}" id="gemini-key-badge">${gemini.configured ? 'المفتاح محفوظ' : 'لم يُضف مفتاح'}</span></div>
      <form id="gemini-key-form"><label>مفتاح API<input name="apiKey" type="password" autocomplete="new-password" required placeholder="الصق المفتاح الجديد هنا"></label><button class="button" type="submit">حفظ المفتاح</button></form>
      <p class="help" id="gemini-key-status" role="status">المفتاح يُحفظ في ملف <code dir="ltr">.env</code> داخل مجلد بيانات البرنامج، خارج قاعدة SQLite والنسخ الاحتياطية. لا يظهر المفتاح بعد حفظه.</p><label class="path-label">مكان ملف الإعدادات<input class="file-path" readonly dir="ltr" value="${e(gemini.path)}"></label></section>
      <div class="storage-layout"><section class="panel storage-main"><div class="section-top"><div><h2>مكان حفظ البيانات</h2><p class="help">قاعدة محلية مستقلة عن منصة مسار.</p></div><span class="badge complete">SQLite · محلي</span></div>
      <label class="path-label">ملف قاعدة البيانات<input class="file-path" readonly dir="ltr" value="${e(details.databasePath)}"></label>
      <div class="actions"><button class="button secondary small" id="open-data-folder">فتح مجلد البيانات ↗</button><button class="button ghost small" id="copy-data-path">نسخ المسار</button></div>
      <dl class="storage-facts"><div><dt>حجم البيانات مع سجل الحفظ</dt><dd dir="ltr">${size(details.databaseBytes + details.pendingBytes)}</dd></div><div><dt>المساحة المتاحة على القرص</dt><dd dir="ltr">${size(details.freeBytes)}</dd></div><div><dt>الامتحانات المحفوظة</dt><dd>${number(details.counts.exams)}</dd></div><div><dt>محاولات الطلاب / المسلّمة</dt><dd>${number(details.counts.students)} / ${number(details.counts.submissions)}</dd></div></dl>
      <div class="integrity-row"><div><strong>فحص سلامة قاعدة البيانات</strong><p id="integrity-status" class="help" role="status">يمكنك تشغيل فحص بنية الملف بدون تغيير البيانات.</p></div><button class="button secondary small" id="check-data">تشغيل الفحص</button></div></section>
      <aside class="storage-explainer"><h2>ما الذي يتم حفظه؟</h2><ul><li>الأسئلة والإجابات النموذجية وإعدادات الوقت.</li><li>بيانات الطلاب وإجاباتهم التي وصلت للكمبيوتر.</li><li>التسليم والدرجات وملاحظات التصحيح.</li></ul><div class="storage-tip"><strong>احتفظ بنسخة خارج الكمبيوتر</strong><p>النسخ الموجودة هنا على نفس القرص. نزّل نسخة إلى فلاشة أو قرص آخر بعد كل جلسة.</p></div><p class="help">حفظ إجابات الطلاب تلقائي. النسخة الاحتياطية لقطة إضافية لاسترجاع البيانات، وليست بديلًا عن الحفظ.</p></aside></div>
      <section class="panel backups-panel"><div class="section-top padded"><div><h2>النسخ المحفوظة</h2><p class="help">كل خمس دقائق أثناء التشغيل، وعند التشغيل والإغلاق وتغيير حالة الجلسة من اللوحة.</p></div><span class="badge quiet">${number(details.backupCount)} نسخة · ${size(details.backupBytes)}</span></div>
      <div class="table-scroll"><table><thead><tr><th>تاريخ النسخة</th><th>الحجم</th><th>الملف</th><th>تنزيل</th></tr></thead><tbody>${details.backups.length ? details.backups.map((backup, index) => `<tr><td><strong>${date(backup.createdAt)}</strong>${index === 0 ? '<small>أحدث نسخة</small>' : ''}</td><td dir="ltr">${size(backup.bytes)}</td><td><span class="backup-filename" dir="ltr">${e(backup.name)}</span></td><td><a class="button secondary small" download href="/api/backups/${encodeURIComponent(backup.name)}">تنزيل النسخة ↓</a></td></tr>`).join('') : '<tr><td class="empty" colspan="4">لا توجد نسخ بعد. أنشئ أول نسخة الآن.</td></tr>'}</tbody></table></div><p class="help padded">نعرض أحدث ٣٠ نسخة. النسخ الأقدم تظل في مجلد backups؛ لا تُحذف تلقائيًا.</p></section>
      <details class="restore-guide"><summary>إزاي أسترجع نسخة احتياطية؟</summary><ol><li>نزّل النسخة المطلوبة، وافتح مجلد البيانات من الزر أعلاه، ثم أغلق برنامج مسار بالكامل.</li><li>غيّر اسم مجلد البيانات الحالي للاحتفاظ به، ثم أنشئ مجلدًا فارغًا بنفس اسمه وفي نفس مكانه:<code dir="ltr">${e(details.folderPath)}</code></li><li>انسخ النسخة المطلوبة إلى المجلد الفارغ وسمّها <b dir="ltr">exams.sqlite3</b>. لا تنقل ملفات أخرى من المجلد القديم.</li><li>افتح البرنامج بالطريقة المعتادة وراجع الامتحانات والنتائج قبل استقبال الطلاب. الوقت المنقضي أثناء التوقف يظل محسوبًا.</li></ol><p class="help">الاسترجاع هنا يدوي. احتفظ بالمجلد الأصلي حتى تتأكد من النسخة. لا تستبدل قاعدة البيانات أثناء تشغيل البرنامج.</p></details>`;
    const screen = container.firstElementChild;
    const integrity = $('#integrity-status');
    $('#gemini-key-form').onsubmit = event => {
      event.preventDefault();
      const form = event.currentTarget;
      perform($('button', form), async () => {
        await api('/api/settings/gemini', {apiKey: form.elements.apiKey.value.trim()}, token);
        form.elements.apiKey.value = '';
        $('#gemini-key-badge').textContent = 'المفتاح محفوظ';
        $('#gemini-key-badge').className = 'badge complete';
        $('#gemini-key-status').textContent = 'تم حفظ المفتاح في ملف .env على هذا الجهاز. يمكنك الآن تصحيح المقالي للجميع.';
        toast('تم حفظ مفتاح Gemini');
      });
    };
    $('#copy-data-path').onclick = () => copyText(details.databasePath).catch(error => toast(error.message, true));
    $('#open-data-folder').onclick = event => perform(event.currentTarget, async () => { await api('/api/storage/open-folder', {}, token); toast('تم فتح مجلد البيانات على الكمبيوتر'); });
    $('#check-data').onclick = event => perform(event.currentTarget, async () => {
      integrity.textContent = 'جارٍ الفحص…';
      try {
        const result = await api('/api/storage/check', {}, token);
        integrity.textContent = result.healthy ? 'اكتمل الفحص: بنية قاعدة البيانات سليمة.' : 'الفحص اكتشف مشكلة. احتفظ بنسخة من الملفات وراجع الدعم قبل المتابعة.';
        integrity.className = result.healthy ? 'help saved' : 'inline-error';
      } catch (error) { integrity.textContent = 'لم يكتمل الفحص. حاول مرة أخرى.'; throw error; }
    });
    $('#create-backup').onclick = event => perform(event.currentTarget, async () => {
      await api('/api/storage/backup', {}, token);
      if (screen.isConnected) await renderStorage(container, token); toast('تم إنشاء نسخة احتياطية جديدة');
    });
  } catch (error) {
    if (!loading.isConnected) return;
    container.innerHTML = `<div class="empty"><h2>تعذر قراءة بيانات التخزين</h2><p>${e(error.message)}</p><button class="button" id="retry-storage">إعادة المحاولة</button></div>`;
    $('#retry-storage').onclick = () => renderStorage(container, token);
  }
}
async function perform(button, action) {
  button.disabled = true;
  try { await action(); }
  catch (error) { toast(error.message, true); }
  finally { if (button.isConnected) button.disabled = false; }
}
