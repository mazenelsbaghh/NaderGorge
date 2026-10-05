document.querySelector('#print-report').addEventListener('click', () => window.print());
document.querySelector('#download-report').addEventListener('click', async event => {
  const button=event.currentTarget, status=document.querySelector('#download-status');
  button.disabled=true;status.textContent='جارٍ تجهيز ملف PDF…';
  try {
    const response=await fetch(button.dataset.url);
    if(!response.ok)throw new Error((await response.json()).error);
    const url=URL.createObjectURL(await response.blob());
    const link=document.createElement('a');link.href=url;link.download=button.dataset.filename;
    document.body.append(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(url),60000);
    status.textContent='تم تنزيل التقرير.';
  } catch(error) {status.textContent=error.message||'تعذر إنشاء PDF. حاول مرة أخرى.';}
  finally{button.disabled=false;}
});
document.querySelector('#send-report').addEventListener('click', async()=>{
  const status=document.querySelector('#download-status');
  try {
    const response=await fetch('/api/settings/whatsapp');
    if(!response.ok)throw new Error((await response.json()).error);
    const settings=await response.json();
    const dialog=document.createElement('dialog');
    const heading=document.createElement('h2');heading.textContent='إرسال تقرير الطالب عبر واتساب';
    const info=document.createElement('p');info.textContent=`الطالب: ${document.querySelector('.identity strong').textContent} · القالب: ${settings.templateName||'لم يُحدد'}`;
    const note=document.createElement('p');note.textContent='التقرير جاهز للإرفاق. تفعيل الإرسال ينتظر ربط حساب واتساب الرسمي وبيانات القالب.';
    const link=document.createElement('a');link.href='/?view=storage';link.textContent='فتح إعدادات واتساب';
    const close=document.createElement('button');close.textContent='إغلاق';close.onclick=()=>{dialog.close();dialog.remove();};
    dialog.append(heading,info,note,link,close);document.body.append(dialog);dialog.showModal();
  } catch(error){status.textContent=error.message;}
});
