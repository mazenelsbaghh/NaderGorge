'use client';

import { useState, useEffect, use } from 'react';
import { useRouter } from 'next/navigation';
import { AdminShellChrome, AdminTabBar, AdminTab, AdminStatCard, AdminModal, AdminDataTable } from '@/components/admin';
import { adminService, type StudentProfileExtendedDto } from '@/services/admin-service';
import { Users, FileText, MonitorPlay, MonitorUp, Power, Video, Clock3 } from 'lucide-react';
import toast from 'react-hot-toast';

export default function AdminStudentProfile({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<'overview' | 'academic' | 'devices' | 'financials' | 'overrides' | 'audit'>('overview');
  const [loading, setLoading] = useState(true);
  const [studentData, setStudentData] = useState<StudentProfileExtendedDto | null>(null);
  const [modalOpen, setModalOpen] = useState<'none'|'override'|'disconnect'|'gamification'|'status'>('none');
  const [overrideInput, setOverrideInput] = useState({ videoId: '', addedViews: 1, reason: '' });
  const [gamificationInput, setGamificationInput] = useState({ points: 10, reason: '' });

  const formatDuration = (seconds: number) => {
    if (!seconds) return '0 دقيقة';
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;

    if (hours > 0) {
      return `${hours}س ${minutes}د`;
    }

    if (minutes > 0) {
      return `${minutes}د ${remainingSeconds}ث`;
    }

    return `${remainingSeconds}ث`;
  };

  const TABS: AdminTab<'overview' | 'academic' | 'devices' | 'financials' | 'overrides' | 'audit'>[] = [
     { key: 'overview', label: 'نظرة عامة' },
     { key: 'academic', label: 'الأكاديمية' },
     { key: 'devices', label: 'الأجهزة والجلسات' },
     { key: 'overrides', label: 'التجاوزات' },
     { key: 'financials', label: 'الماليات' },
     { key: 'audit', label: 'سجل النشاط' }
  ];

  const fetchStudent = () => {
      setLoading(true);
      adminService.getStudentProfile(id).then(res => {
          setStudentData(res);
          setLoading(false);
      }).catch(err => {
          console.error("Failed to load student", err);
          setLoading(false);
      });
  };

  useEffect(() => {
    fetchStudent();
  }, [id]);

  const handleOverrideSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
        await adminService.overrideVideoLimit(id, overrideInput.videoId, overrideInput.addedViews, overrideInput.reason);
        setModalOpen('none');
        toast.success('تمت الإضافة بنجاح');
        fetchStudent();
    } catch(err) {
        toast.error('فشل التجاوز');
    }
  };

  const handleDisconnectAll = async () => {
    try {
        await adminService.disconnectAllDevices(id);
        toast.success('تم الفصل بنجاح');
        fetchStudent();
    } catch(err) {
        toast.error('فشل الفصل');
    }
  };

  const handleGamificationSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
        await adminService.adjustGamification(id, gamificationInput.points, gamificationInput.reason);
        setModalOpen('none');
        toast.success('تم التعديل بنجاح');
        fetchStudent();
    } catch(err) {
        toast.error('فشل تعديل النقاط');
    }
  };

  const toggleStatus = async () => {
    try {
        await adminService.toggleStudentStatus(id, !(studentData?.isActive), "Admin Override Action");
        fetchStudent();
    } catch(err) {
        toast.error('فشل تغيير الحالة');
    }
  };

  return (
    <AdminShellChrome
       activePath="/admin/users"
       sectionLabel="إدارة المستخدمين"
       pageTitle="ملف الطالب الشامل"
       subtitle="تفاصيل شاملة للمنهج، الأجهزة، والماليات"
       action={
          <div className="flex gap-4">
            <button 
              onClick={toggleStatus}
              className={`flex items-center gap-2 rounded-2xl px-4 py-2 font-medium transition-colors 
                 ${studentData?.isActive ? 'bg-red-500/10 text-red-500 hover:bg-red-500/20' : 'bg-green-500/10 text-green-500 hover:bg-green-500/20'}`}
            >
               <Power size={20} />
               {studentData?.isActive ? 'إيقاف الحساب' : 'تفعيل الحساب'}
            </button>
            <button 
               onClick={() => router.push('/admin/users')}
               className="flex items-center gap-2 rounded-2xl bg-[var(--admin-surface-low)] px-4 py-2 text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-border)]"
            >
               <Users size={20} />
               العودة للقائمة
            </button>
          </div>
       }
    >
       <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />
       
       <div className="mt-8 animate-in fade-in slide-in-from-bottom-2 duration-300">
         {activeTab === 'overview' && (
            <div className="flex flex-col gap-8">
               <div className="flex justify-between items-center mb-[-0.5rem]">
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">ملخص الإحصاءات</h3>
                  <button onClick={() => setModalOpen('gamification')} className="text-sm font-bold text-[var(--admin-primary)] bg-[var(--admin-card-strong)] px-4 py-2 flex items-center gap-2 rounded-xl hover:bg-[var(--admin-hover)] transition-colors">
                     تعديل نقاط الطالب
                  </button>
               </div>
               <div className="grid grid-cols-1 gap-6 md:grid-cols-4">
                  <AdminStatCard variant="accent" icon={Users} label="إجمالي النقاط" value={studentData?.gamification?.totalPoints || 0} />
                  <AdminStatCard variant="light" icon={MonitorUp} label="أجهزة مسجلة" value={studentData?.devices?.length || 0} />
                  <AdminStatCard variant="muted" icon={FileText} label="باقات نشطة" value={studentData?.packages?.length || 0} />
                  <AdminStatCard variant="accent" icon={MonitorPlay} label="تجاوزات نشطة" value={studentData?.overrides?.length || 0} />
               </div>
               
               <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
                   <h3 className="mb-4 text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                      البيانات الشخصية
                   </h3>
                   {loading ? (
                       <div className="text-[var(--admin-muted)]">جاري التحميل...</div>
                   ) : (
                       <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">الاسم بالكامل</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.fullName || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">البريد الإلكتروني</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.email || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">رقم الهاتف</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.phone || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">ولي الأمر</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.parentPhone || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">الصف الدراسي</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.grade || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">تاريخ الإنضمام</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.createdAt ? new Date(studentData.createdAt).toLocaleDateString() : 'غير متوفر'}</p>
                           </div>
                       </div>
                   )}
               </div>
            </div>
         )}
         
         {activeTab === 'overrides' && (
             <div className="flex flex-col gap-6">
                 <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                    <div>
                        <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">تجاوزات الفيديوهات</h3>
                        <p className="text-[var(--admin-muted)]">إضافة مشاهدات أو رفع الحظر مؤقتاً للطالب</p>
                    </div>
                    <button onClick={() => setModalOpen('override')} className="bg-[var(--admin-accent)] text-white px-6 py-3 rounded-xl flex items-center gap-2 font-bold hover:brightness-110">
                        <Video size={18} />
                        إضافة تجاوز جديد
                    </button>
                 </div>
                 
                 <AdminDataTable<any> 
                    columns={[
                        {key: 'videoId', label:'رقم الفيديو (ID)', render: (row: any) => row.videoId}, 
                        {key: 'addedViews', label:'المشاهدات المضافة', render: (row: any) => row.addedViews}, 
                        {key: 'reason', label:'السبب', render: (row: any) => row.reason}
                    ]}
                    data={studentData?.overrides || []}
                    rowKey={(row: any) => row.id || Math.random().toString()}
                    emptyMessage="لا توجد تجاوزات لهذا الطالب"
                 />
             </div>
         )}

         {activeTab === 'devices' && (
             <div className="flex flex-col gap-6">
                 <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                    <div>
                        <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">الأجهزة المُسجلة</h3>
                        <p className="text-[var(--admin-muted)]">قائمة بالأجهزة التي قام الطالب بالدخول من خلالها</p>
                    </div>
                    <button onClick={handleDisconnectAll} className="bg-red-500/10 text-red-500 px-6 py-3 rounded-xl font-bold hover:bg-red-500/20">
                        فصل جميع الأجهزة
                    </button>
                 </div>

                 <AdminDataTable<any> 
                    columns={[
                        {key: 'id', label:'معرف الجهاز', render: (row: any) => row.id}, 
                        {key: 'deviceName', label:'اسم الجهاز', render: (row: any) => row.deviceName}, 
                        {key: 'lastActiveAt', label:'آخر نشاط', render: (row: any) => row.lastActiveAt ? new Date(row.lastActiveAt).toLocaleString() : ''}
                    ]}
                    data={studentData?.devices || []}
                    rowKey={(row: any) => row.id}
                    emptyMessage="لا توجد أجهزة مسجلة"
                 />
             </div>
         )}

         {activeTab === 'audit' && (
             <div className="flex flex-col gap-6">
                 <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                    <div>
                        <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل النشاط (Audit Log)</h3>
                        <p className="text-[var(--admin-muted)]">كافة الإجراءات والعمليات التي تمت على حساب هذا الطالب</p>
                    </div>
                 </div>

                 <AdminDataTable<any> 
                    columns={[
                        {key: 'date', label:'الوقت والتاريخ', render: (row: any) => new Date(row.date).toLocaleString()},
                        {key: 'action', label:'الإجراء', render: (row: any) => row.action},
                        {key: 'adminName', label:'المسؤول', render: (row: any) => row.adminName},
                        {key: 'details', label:'التفاصيل', render: (row: any) => <span className="text-xs truncate max-w-xs inline-block">{row.details}</span>}
                    ]}
                    data={studentData?.auditTrail || []}
                    rowKey={(row: any) => row.id}
                    emptyMessage="لا يوجد سجل نشاط"
                 />
             </div>
         )}

         {activeTab === 'academic' && (
             <div className="flex flex-col gap-6">
                 <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
                    <AdminStatCard
                      variant="accent"
                      icon={Video}
                      label="فيديوهات تم تتبعها"
                      value={studentData?.watchTracking?.watchedVideosCount || 0}
                    />
                    <AdminStatCard
                      variant="light"
                      icon={Clock3}
                      label="إجمالي زمن المشاهدة"
                      value={formatDuration(studentData?.watchTracking?.totalWatchedSeconds || 0)}
                    />
                    <AdminStatCard
                      variant="muted"
                      icon={MonitorPlay}
                      label="جلسات محتسبة"
                      value={studentData?.watchTracking?.activities?.reduce((sum, activity) => sum + activity.watchCount, 0) || 0}
                    />
                 </div>

                 <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                    <div className="mb-5">
                      <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل مشاهدة الفيديوهات</h3>
                      <p className="text-[var(--admin-muted)]">آخر الفيديوهات التي شاهدها الطالب مع الزمن التراكمي الفعلي وآخر نشاط.</p>
                    </div>

                    <AdminDataTable<StudentProfileExtendedDto['watchTracking']['activities'][number]>
                      columns={[
                        {
                          key: 'videoTitle',
                          label: 'الفيديو',
                          render: (row) => (
                            <div className="flex flex-col">
                              <span className="font-bold text-[var(--admin-text)]">{row.videoTitle}</span>
                              <span className="text-xs text-[var(--admin-muted)]">{row.lessonTitle}{row.packageName ? ` • ${row.packageName}` : ''}</span>
                            </div>
                          )
                        },
                        {
                          key: 'watchedSeconds',
                          label: 'المدة المشاهدة',
                          render: (row) => formatDuration(row.watchedSeconds)
                        },
                        {
                          key: 'watchCount',
                          label: 'المشاهدات',
                          render: (row) => `${row.watchCount} / ${row.maxWatchCount === 0 ? '∞' : row.maxWatchCount}`
                        },
                        {
                          key: 'isLocked',
                          label: 'الحالة',
                          render: (row) => row.isLocked ? 'مقفول' : 'نشط'
                        },
                        {
                          key: 'lastWatchedAt',
                          label: 'آخر مشاهدة',
                          render: (row) => row.lastWatchedAt ? new Date(row.lastWatchedAt).toLocaleString() : 'غير متوفر'
                        }
                      ]}
                      data={studentData?.watchTracking?.activities || []}
                      rowKey={(row) => row.lessonVideoId}
                      emptyMessage="لا توجد بيانات مشاهدة لهذا الطالب بعد"
                    />
                 </div>
             </div>
         )}
         
         <AdminModal open={modalOpen === 'gamification'} onClose={() => setModalOpen('none')} title="تعديل القاء نقاط الطالب">
             <form onSubmit={handleGamificationSubmit} className="flex flex-col gap-4">
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">إضافة / خصم نقاط (يمكن استخدام قيم سالبة)</label>
                     <input required type="number" className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-accent)] outline-none" 
                            value={gamificationInput.points} onChange={e => setGamificationInput({...gamificationInput, points: parseInt(e.target.value)})} />
                 </div>
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">السبب</label>
                     <input required type="text" className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-accent)] outline-none" 
                            value={gamificationInput.reason} onChange={e => setGamificationInput({...gamificationInput, reason: e.target.value})} />
                 </div>
                 <div className="flex gap-4 mt-4">
                     <button type="button" onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-border)] hover:brightness-110">إلغاء</button>
                     <button type="submit" className="flex-1 px-4 py-3 rounded-xl font-bold text-white bg-[var(--admin-accent)] hover:brightness-110">حفظ وحفظ</button>
                 </div>
             </form>
         </AdminModal>

         <AdminModal open={modalOpen === 'override'} onClose={() => setModalOpen('none')} title="إضافة مشاهدات فيديو">
             <form onSubmit={handleOverrideSubmit} className="flex flex-col gap-4">
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">رقم الفيديو المتجاوز (UUID)</label>
                     <input required type="text" className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-accent)] outline-none" 
                            value={overrideInput.videoId} onChange={e => setOverrideInput({...overrideInput, videoId: e.target.value})} />
                 </div>
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">عدد المشاهدات الإضافية</label>
                     <input required type="number" min="1" className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-accent)] outline-none" 
                            value={overrideInput.addedViews} onChange={e => setOverrideInput({...overrideInput, addedViews: parseInt(e.target.value)})} />
                 </div>
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">السبب (يظهر للآدمنز)</label>
                     <input required type="text" className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-accent)] outline-none" 
                            value={overrideInput.reason} onChange={e => setOverrideInput({...overrideInput, reason: e.target.value})} />
                 </div>
                 <div className="flex gap-4 mt-4">
                     <button type="button" onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-border)] hover:brightness-110">إلغاء</button>
                     <button type="submit" className="flex-1 px-4 py-3 rounded-xl font-bold text-white bg-[var(--admin-accent)] hover:brightness-110">حفظ التجاوز</button>
                 </div>
             </form>
         </AdminModal>
       </div>
    </AdminShellChrome>
  );
}
