import React, {useState} from 'react';
import {number, toast} from '../../web/assets/common.js';

const stages = [
  {kind:'grades',title:'الصفوف الدراسية',singular:'الصف الدراسي',parent:null},
  {kind:'centers',title:'السناتر',singular:'السنتر',parent:'grades',key:'gradeId'},
  {kind:'groups',title:'المجموعات',singular:'المجموعة',parent:'centers',key:'centerId'},
  {kind:'lessons',title:'الحصص',singular:'الحصة',parent:'groups',key:'groupId'},
];
export default function CenterSetup({catalog,request,refresh}) {
  const [selected,setSelected]=useState({});
  const [names,setNames]=useState({});
  const [busy,setBusy]=useState(false);
  const update=(kind,id)=>setSelected(current=>{
    const next={...current,[kind]:id};const position=stages.findIndex(stage=>stage.kind===kind);
    stages.slice(position+1).forEach(stage=>delete next[stage.kind]);return next;
  });
  const add=async(stage,event)=>{event.preventDefault();const name=(names[stage.kind]||'').trim();if(!name)return;
    setBusy(true);try{const payload={name};if(stage.parent)payload[stage.key]=selected[stage.parent];
      const result=await request(`/api/catalog/${stage.kind}`,payload);await refresh();
      setNames(current=>({...current,[stage.kind]:''}));update(stage.kind,result.id);toast(`تمت إضافة ${stage.singular}`);
    }catch(error){toast(error.message,true);}finally{setBusy(false);}};
  const rename=async(stage,item)=>{const name=window.prompt(`الاسم الجديد لـ${stage.singular}`,item.name)?.trim();if(!name||name===item.name)return;
    setBusy(true);try{await request(`/api/catalog/${stage.kind}/${item.id}/rename`,{name});await refresh();toast('تم تعديل الاسم');}
    catch(error){toast(error.message,true);}finally{setBusy(false);}};
  return <><div className="page-heading"><div><span className="context-label">تنظيم السنتر</span><h1>الصفوف والسناتر والمجموعات والحصص</h1>
    <p>رتّب أماكن التدريس مرة واحدة. في قاعة السنتر تختار الحصة ثم اسم الامتحان المناسب لها.</p></div></div>
    <div className="catalog-grid">{stages.map(stage=>{const parent=stage.parent&&selected[stage.parent];
      const items=(catalog?.[stage.kind]||[]).filter(item=>!stage.parent||item[stage.key]===parent);
      const enabled=!stage.parent||Boolean(parent);
      return <section className="panel catalog-column" key={stage.kind}><div className="catalog-heading"><h2>{stage.title}</h2><span className="badge quiet">{number(items.length)}</span></div>
        {enabled?<><div className="catalog-items">{items.length?items.map(item=><div className={`catalog-item ${selected[stage.kind]===item.id?'selected':''}`} key={item.id}>
          <button type="button" onClick={()=>update(stage.kind,item.id)}>{item.name}</button>
          <button className="catalog-rename" type="button" aria-label={`تعديل ${item.name}`} onClick={()=>rename(stage,item)}>تعديل</button></div>):
          <p className="help catalog-empty">أضف أول {stage.singular} هنا.</p>}</div>
          <form className="catalog-add" onSubmit={event=>add(stage,event)}><label>إضافة {stage.singular}<input required maxLength="150" value={names[stage.kind]||''}
            onChange={event=>setNames(current=>({...current,[stage.kind]:event.target.value}))} placeholder={`اسم ${stage.singular}`}/></label>
            <button className="button secondary small" disabled={busy}>＋ إضافة</button></form></>:
          <p className="help catalog-empty">اختر {stages.find(s=>s.kind===stage.parent)?.singular} أولًا.</p>}</section>})}</div></>;
}
