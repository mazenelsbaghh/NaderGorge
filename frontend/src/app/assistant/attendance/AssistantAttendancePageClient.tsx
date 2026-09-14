'use client';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import { AttendanceWorkspace } from '@/features/hr/attendance';
export default function AssistantAttendancePageClient() { return <NavRouteGuard routePath="/assistant/attendance"><AssistantPage activePath="/assistant/attendance" sectionLabel="الموارد البشرية" pageTitle="حضوري" subtitle="سجل وردياتك والاستراحات اليومية."><AttendanceWorkspace /></AssistantPage></NavRouteGuard>; }
