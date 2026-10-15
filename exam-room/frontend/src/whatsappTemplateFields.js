const numbered = /\{\{\s*([0-9]+|[a-z][a-z0-9_]*)\s*\}\}/g;
const parentSources={1:'name',2:'lesson',3:'month',4:'center',5:'time',6:'score',7:'maximum',8:'homework'};
const namedSources={student_name:'name',name:'name',student_code:'code',code:'code',score:'score',max_score:'maximum',maximum:'maximum',result:'result',exam_name:'exam',lesson:'lesson',month:'month',center:'center',group:'group',group_name:'group',time:'time',homework:'homework',homework_status:'homework',attendance:'attendance',attendance_status:'attendance'};
export const templateSources={name:'اسم الطالب',code:'كود الطالب',result:'جاب كام من كام',score:'الدرجة',maximum:'الدرجة النهائية',exam:'اسم الامتحان',lesson:'الحصة',month:'الشهر',center:'السنتر',group:'المجموعة',time:'موعد الحصة',homework:'حالة الواجب',attendance:'حالة الحضور',literal:'نص ثابت'};
export function templateFieldList(template){
 if(Array.isArray(template?.reportFields))return template.reportFields;
 const fields=[];
 const add=(scope,text,index)=>{
  const keys=[...new Set([...String(text||'').matchAll(numbered)].map(m=>m[1]))];
  if(keys.every(key=>/^\d+$/.test(key)))keys.sort((a,b)=>Number(a)-Number(b));
  for(const position of keys)fields.push({scope,position,buttonIndex:index,key:scope==='button'?`button:${index}:${position}`:`${scope}:${position}`,suggestedSource:namedSources[position]||(['parent_follow_center','parent__follow_center'].includes(template?.name)?(scope==='header'?'name':scope==='body'?parentSources[position]:''):'')});
 };
 for(const component of template?.components||[]){
  if(component.type==='HEADER'&&component.format==='TEXT')add('header',component.text);
  if(component.type==='BODY')add('body',component.text);
  if(component.type==='BUTTONS')component.buttons?.forEach((b,i)=>{if(b.type==='URL')add('button',b.url,i);});
 }
 return fields;
}
export function savedTemplateBinding(template,recipientKind,reportKind){
 return template?.savedBindings?.find(binding=>binding.bindingSchema===template.bindingSchema&&binding.recipientKind===recipientKind&&binding.reportKind===reportKind&&validTemplateMappings(template,binding.mappings));
}
export function validTemplateMappings(template,mappings){
 return Array.isArray(mappings)&&mappings.length===templateFieldList(template).length&&mappings.every(mapping=>Object.hasOwn(templateSources,mapping?.source)&&(mapping.source!=='literal'||String(mapping.text||'').trim().length>0));
}
export function initialTemplateMappings(template,recipientKind,reportKind){
 const binding=savedTemplateBinding(template,recipientKind,reportKind);
 return binding?binding.mappings.map(mapping=>({...mapping})):templateFieldList(template).map(field=>({source:field.suggestedSource||''}));
}
export function whatsappViewHref(view,batchId,search=window.location.search){
 const query=new URLSearchParams(search);query.set('view',view);query.set('batch',batchId);
 return `?${query.toString()}`;
}
export function renderTemplateMessage(template,item){
 const fields=templateFieldList(template);
 const fill=(scope,text,index)=>String(text||'').replace(numbered,(_,position)=>{
  const found=fields.findIndex(f=>f.scope===scope&&String(f.position)===position&&(scope!=='button'||f.buttonIndex===index));
  return found<0?'':item.parameters?.[found]||'';
 });
 return (template?.components||[]).flatMap(c=>c.type==='HEADER'&&c.format==='TEXT'?[fill('header',c.text)]:c.type==='BODY'?[fill('body',c.text)]:c.type==='FOOTER'?[c.text]:[]).join('\n');
}
