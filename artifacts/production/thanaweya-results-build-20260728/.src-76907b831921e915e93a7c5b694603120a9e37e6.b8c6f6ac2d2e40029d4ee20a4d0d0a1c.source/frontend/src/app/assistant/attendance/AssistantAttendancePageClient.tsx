'use client';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import { AttendanceWorkspace } from '@/features/hr/attendance';
export default function AssistantAttendancePageClient() { return <NavRouteGuard routePath="/assistant/attendance"><AssistantShellChrome activePath="/assistant/attendance" sectionLabel="الموارد البشرية" pageTitle="حضوري" subtitle="سجل وردياتك والاستراحات اليومية."><AttendanceWorkspace /></AssistantShellChrome></NavRouteGuard>; }
