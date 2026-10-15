import {test} from 'node:test';
import assert from 'node:assert/strict';
import {templateFieldList,renderTemplateMessage,initialTemplateMappings,savedTemplateBinding,whatsappViewHref} from './whatsappTemplateFields.js';
test('header and body variable one stay independent when mapping and previewing',()=>{
 const template={name:'parent_follow_center',components:[{type:'HEADER',format:'TEXT',text:'ولي أمر {{1}}'},{type:'BODY',text:'{{1}} حصل على {{6}} من {{7}}'}]};
 const fields=templateFieldList(template);
 assert.deepEqual(fields.map(f=>[f.key,f.suggestedSource]),[['header:1','name'],['body:1','name'],['body:6','score'],['body:7','maximum']]);
 assert.equal(renderTemplateMessage(template,{parameters:['طالب العنوان','طالب المتن','8','12']}),'ولي أمر طالب العنوان\nطالب المتن حصل على 8 من 12');
});
test('named fields and URL buttons retain their component identities',()=>{
 const template={components:[{type:'BODY',text:'{{student_name}} {{score}}'},{type:'BUTTONS',buttons:[{type:'URL',url:'https://example.test/{{1}}'}]}]};
 assert.deepEqual(templateFieldList(template).map(f=>f.key),['body:student_name','body:score','button:0:1']);
});
test('saved bindings fill a new batch and never carry into another template revision or recipient',()=>{
 const template={id:'followup',bindingSchema:'revision-one',components:[{type:'BODY',text:'{{student_name}} {{group_name}} {{attendance_status}} {{score}} من {{max_score}} {{homework_status}}'}],savedBindings:[{bindingSchema:'revision-one',recipientKind:'guardian',reportKind:'followup',mappings:[{source:'name'},{source:'group'},{source:'attendance'},{source:'score'},{source:'maximum'},{source:'literal',text:'يرجى المتابعة'}]}]};
 const initial=initialTemplateMappings(template,'guardian','followup');
 assert.equal(initial.at(-1).text,'يرجى المتابعة');
 initial.at(-1).text='تعديل غير محفوظ';
 assert.equal(initialTemplateMappings(template,'guardian','followup').at(-1).text,'يرجى المتابعة');
 for(const changed of [{...template,bindingSchema:'revision-two'},{...template,savedBindings:[{...template.savedBindings[0],recipientKind:'student'}]},{...template,savedBindings:[{...template.savedBindings[0],reportKind:'attendance'}]}]){
  assert.equal(savedTemplateBinding(changed,'guardian','followup'),undefined);
  assert.deepEqual(initialTemplateMappings(changed,'guardian','followup').map(field=>field.source),['name','group','attendance','score','maximum','homework']);
 }
});
test('settings and return links preserve the embedded center theme and batch identity',()=>{
 const href=whatsappViewHref('whatsapp','batch with spaces','?view=whatsapp-center&embedded=center&theme=light&batch=old');
 const query=new URLSearchParams(href.slice(1));
 assert.equal(query.get('view'),'whatsapp');
 assert.equal(query.get('batch'),'batch with spaces');
 assert.equal(query.get('embedded'),'center');
 assert.equal(query.get('theme'),'light');
});
