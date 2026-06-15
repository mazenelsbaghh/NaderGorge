"use client";

import { devConsole } from "@/utils/dev-console";
import { useEffect, useMemo, useRef, useState } from "react";
import { isAxiosError } from "axios";
import { Eye, KeyRound, Sparkles, Search } from "lucide-react";
import Link from "next/link";

import {
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
} from "@/components/admin";
import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";
import { formatCompactNumber, formatDate } from "@/components/admin/admin-utils";
import { adminService, CodeGroupDto } from "@/services/admin-service";
import { PackageDto, contentService } from "@/services/content-service";
import NeumorphButton from "@/components/ui/neumorph-button";

export default function TeacherCodesPageClient() {
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");

  const [packages, setPackages] = useState<PackageDto[]>([]);
  const loadDataInFlightRef = useRef<Promise<void> | null>(null);

  useEffect(() => {
    void loadData();
  }, []);

  async function loadData(options?: { force?: boolean }) {
    if (loadDataInFlightRef.current && !options?.force) {
      return loadDataInFlightRef.current;
    }

    const request = (async () => {
      try {
        setLoading(true);
        const [groupsData, packagesResponse] = await Promise.all([
          adminService.listCodeGroups({ force: options?.force }),
          contentService.getPackages({ force: options?.force }),
        ]);
        setGroups(groupsData || []);
        setPackages((packagesResponse.data?.data || []) as PackageDto[]);
      } catch (error) {
        if (!isAxiosError(error) || error.response?.status !== 429) {
          devConsole.error(error);
        }
      } finally {
        setLoading(false);
      }
    })();

    loadDataInFlightRef.current = request;

    try {
      await request;
    } finally {
      if (loadDataInFlightRef.current === request) {
        loadDataInFlightRef.current = null;
      }
    }
  }



  const packageNameMap = useMemo(() => {
    return Object.fromEntries(packages.map((pkg) => [pkg.id, pkg.name]));
  }, [packages]);

  const filteredGroups = useMemo(() => {
    if (!searchQuery.trim()) return groups;
    const q = searchQuery.toLowerCase().trim();
    return groups.filter((g) => 
      g.name.toLowerCase().includes(q) || 
      g.id.toLowerCase().includes(q) ||
      (g.packageId && (packageNameMap[g.packageId] || g.packageId).toLowerCase().includes(q)) ||
      (g.lessonId && g.lessonId.toLowerCase().includes(q))
    );
  }, [groups, searchQuery, packageNameMap]);

  const totalCodes = groups.reduce((sum, group) => sum + group.codeCount, 0);
  const usedCodes = groups.reduce((sum, group) => sum + group.usedCount, 0);

  const groupColumns: AdminColumn<CodeGroupDto>[] = [
    {
      key: "name",
      label: "المجموعة",
      render: (g) => (
        <div>
          <div className="font-bold text-[var(--admin-text-strong)]">{g.name || "دفعة بدون اسم"}</div>
          <div className="text-xs font-mono text-[var(--admin-muted)] mt-0.5">{g.id}</div>
        </div>
      ),
    },
    {
      key: "createdAt",
      label: "تاريخ الإنشاء",
      render: (g) => <span className="text-[var(--admin-muted)]">{formatDate(g.createdAt)}</span>,
    },
    {
      key: "linking",
      label: "الربط",
      render: (g: CodeGroupDto) => (
        <div className="font-semibold text-[var(--admin-text)]">
          <div>{g.packageId ? "Package" : g.lessonId ? "Lesson" : "عام"}</div>
          {g.packageId ? (
            <div className="mt-1 text-xs text-[var(--admin-muted)] font-normal">{packageNameMap[g.packageId] || g.packageId}</div>
          ) : null}
        </div>
      ),
    },
    {
      key: "usage",
      label: "الاستخدام",
      render: (g) => (
        <div>
          <div className="text-sm font-bold text-[var(--admin-text)]">
            {formatCompactNumber(g.usedCount)} / {formatCompactNumber(g.codeCount)}
          </div>
          <div className="mt-2 h-1.5 rounded-full bg-[var(--admin-card-strong)] overflow-hidden border border-[var(--admin-border)]">
            <div
              className="h-full rounded-full bg-[var(--admin-primary-strong)]"
              style={{ width: `${g.codeCount === 0 ? 0 : (g.usedCount / g.codeCount) * 100}%` }}
            />
          </div>
        </div>
      ),
    },
    {
      key: "actions",
      label: "الإجراءات",
      align: "left",
      render: (g) => (
        <div className="flex items-center justify-end gap-2">
          <Link href={`/teacher/codes/${g.id}`} passHref legacyBehavior>
            <NeumorphButton
              type="button"
              intent="icon"
              size="icon"
              title="عرض التفاصيل والطباعة"
            >
              <Eye className="h-5 w-5" />
            </NeumorphButton>
          </Link>
        </div>
      ),
    },
  ];

  return (
    <TeacherShellChrome
      activePath="/teacher/codes"
      sectionLabel="إدارة الأكواد"
      pageTitle="مجموعات أكواد الوصول"
      subtitle="عرض وتفاصيل مجموعات أكواد الوصول والطباعة."
    >
      {/* Stats */}
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={KeyRound}
          label="إجمالي الأكواد"
          value={totalCodes}
        />
        <AdminStatCard
          variant="accent"
          icon={Sparkles}
          label="المستخدمة"
          value={usedCodes}
        />
        <AdminStatCard
          variant="muted"
          icon={Sparkles}
          label="المجموعات"
          value={groups.length}
        />
      </section>

      {/* Search and Filters */}
      <div className="mb-6 flex items-center bg-[var(--admin-card)] rounded-2xl border border-[var(--admin-border)] px-4 py-3 shadow-sm max-w-md mr-auto">
        <Search className="text-[var(--admin-muted)] w-5 h-5 ml-2.5" />
        <input
          type="text"
          placeholder="ابحث عن اسم دفعة، ID، أو باقة مربوطة..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="bg-transparent border-none outline-none text-sm text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] w-full text-right"
          dir="rtl"
        />
      </div>

      {/* Code Groups Table */}
      <AdminDataTable
        data={filteredGroups}
        columns={groupColumns}
        loading={loading}
        rowKey={(g) => g.id}
        emptyMessage="لا توجد مجموعات أكواد بعد."
      />
    </TeacherShellChrome>
  );
}
