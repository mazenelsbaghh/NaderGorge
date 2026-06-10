'use client';

import React from 'react';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { Bell, Info, CheckCircle, Clock } from 'lucide-react';

export default function AssistantNotificationsPageClient() {
  const mockNotifications = [
    {
      id: 1,
      title: 'مهمة جديدة مسندة إليك',
      description: 'لقد قام المدير بإسناد مهمة "مراجعة واجبات الفصل الثالث للطلاب المتعثرين" إليك.',
      time: 'منذ ساعتين',
      type: 'task',
      icon: Bell,
      iconColor: 'text-amber-500 bg-amber-50 dark:bg-amber-950/20',
    },
    {
      id: 2,
      title: 'تم قبول طلب الإجازة',
      description: 'تمت الموافقة على طلب إجازتك السنوية للفترة من 20 يونيو إلى 25 يونيو.',
      time: 'منذ يومين',
      type: 'hr',
      icon: CheckCircle,
      iconColor: 'text-emerald-500 bg-emerald-50 dark:bg-emerald-950/20',
    },
    {
      id: 3,
      title: 'تحديث في نظام التعليم المطور',
      description: 'تمت إضافة ميزة بنك الأسئلة الموحد. يرجى الاطلاع على دليل العمل لمعرفة التغييرات الجديدة.',
      time: 'منذ 3 أيام',
      type: 'system',
      icon: Info,
      iconColor: 'text-blue-500 bg-blue-50 dark:bg-blue-950/20',
    },
  ];

  return (
    <AssistantShellChrome
      activePath="/assistant/notifications"
      sectionLabel="التنبيهات"
      pageTitle="مركز الإشعارات والتنبيهات"
      subtitle="تابع آخر التحديثات الخاصة بالمهام والإشعارات الإدارية وقرارات الموارد البشرية."
    >
      <div className="mx-auto max-w-4xl space-y-6 text-right" dir="rtl">
        {mockNotifications.map((notif) => {
          const Icon = notif.icon;
          return (
            <div
              key={notif.id}
              className="flex gap-4 items-start p-5 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] hover:shadow-md transition duration-200"
            >
              <div className={`p-3 rounded-2xl shrink-0 ${notif.iconColor}`}>
                <Icon className="h-5 w-5" />
              </div>
              <div className="flex-1 space-y-1">
                <div className="flex justify-between items-center">
                  <h3 className="text-sm font-black text-[var(--admin-text)]">{notif.title}</h3>
                  <span className="text-[10px] text-[var(--admin-muted)] font-bold font-mono flex items-center gap-1">
                    <Clock className="h-3 w-3" />
                    {notif.time}
                  </span>
                </div>
                <p className="text-xs text-[var(--admin-muted)] leading-relaxed">{notif.description}</p>
              </div>
            </div>
          );
        })}
      </div>
    </AssistantShellChrome>
  );
}
