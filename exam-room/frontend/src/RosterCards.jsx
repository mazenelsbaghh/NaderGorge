import React from 'react';
import {number} from '../../web/assets/common.js';
import {rosterFilters,screenFilters} from './rosterStatus.js';

export default function RosterCards({counts,filter,onChange,searching,showScreenCards}) {
 return <section className="roster-card-area" aria-label="حالات الطلاب">
  <div className="roster-card-heading"><h2>حالات الطلاب</h2><p className="help">{searching?'الأعداد حسب البحث الحالي. ':''}اضغط على الكارت لعرض طلابه.</p></div>
  {[{label:'حالات الطلاب',filters:rosterFilters},...(showScreenCards?[{label:'مراقبة الشاشة',filters:screenFilters}]:[])].map(group=><React.Fragment key={group.label}>
   {group.label==='مراقبة الشاشة'&&<h3 className="screen-card-heading">آخر حالة للشاشة · المحاولات غير المسلّمة</h3>}
   <div className="roster-status-cards" role="group" aria-label={group.label==='حالات الطلاب'?'تصفية الطلاب':'تصفية مراقبة الشاشة'}>
    {group.filters.map(([value,label])=><button type="button" key={value}
     aria-label={`${label}: ${number(counts[value])} طالب`} aria-pressed={filter===value}
     className={`roster-status-card ${filter===value?'selected':''}`} onClick={()=>onChange(value)}>
     <span className="roster-card-label">{label}</span>
     <span className="roster-card-bottom"><strong>{number(counts[value])}</strong><span aria-hidden="true">{filter===value?'✓':'←'}</span></span>
    </button>)}
   </div>
  </React.Fragment>)}
  <p className="roster-card-selection" role="status">المعروض: {[...rosterFilters,...screenFilters].find(([key])=>key===filter)?.[1]} · {number(counts[filter])} طالب</p>
 </section>;
}
