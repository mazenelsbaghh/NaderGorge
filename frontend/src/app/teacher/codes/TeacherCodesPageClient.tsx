"use client";

import { devConsole } from "@/utils/dev-console";
import { useEffect, useMemo, useRef, useState } from "react";
import { isAxiosError } from "axios";
import { Eye, KeyRound, Sparkles } from "lucide-react";
import Link from "next/link";

import {
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminSearchToolbar,
} from "@/components/admin";
import { TeacherPage } from "@/components/teacher/TeacherShellChrome";
import { formatCompactNumber, formatDate } from "@/components/admin/admin-utils";
import { adminService, CodeGroupDto } from "@/services/admin-service";
import { PackageDto, contentService } from "@/services/content-service";
import NeumorphButton from "@/components/ui/neumorph-button";

export default function TeacherCodesPageClient() {
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");
  const [codeSearch, setCodeSearch] = useState<{
    query: string;
    status: "loading" | "success" | "error";
    groups: CodeGroupDto[];
  } | null>(null);
  const [searchRetry, setSearchRetry] = useState(0);
  const searchTerm = searchQuery.trim();

  useEffect(() => {
    if (!searchTerm) return;
    let active = true;
    const timer = window.setTimeout(() => {
      setCodeSearch({ query: searchTerm, status: "loading", groups: [] });
      void adminService.listCodeGroups({ search: searchTerm }).then((matches) => {
        if (active) setCodeSearch({ query: searchTerm, status: "success", groups: matches ?? [] });
      }).catch((error: unknown) => {
        if (!active) return;
        devConsole.error(error);
        setCodeSearch({ query: searchTerm, status: "error", groups: [] });
      });
    }, 350);
    return () => { active = false; window.clearTimeout(timer); };
  }, [searchTerm, searchRetry]);

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
          adminService.listCodeGroups(),
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
    if (!searchTerm) return groups;
    const q = searchTerm.toLowerCase();
    const matches = codeSearch?.query === searchTerm && codeSearch.status === "success" ? codeSearch.groups : [];
    const matchedIds = new Set(matches.map(group => group.id));
    const candidates = new Map([...groups, ...matches].map(group => [group.id, group]));
    return [...candidates.values()].filter((g) =>
      matchedIds.has(g.id) ||
      g.name.toLowerCase().includes(q) || 
      g.id.toLowerCase().includes(q) ||
      (g.packageId && (packageNameMap[g.packageId] || g.packageId).toLowerCase().includes(q)) ||
      (g.lessonId && g.lessonId.toLowerCase().includes(q))
    );
  }, [groups, searchTerm, codeSearch, packageNameMap]);

  const searchPending = Boolean(searchTerm && (codeSearch?.query !== searchTerm || codeSearch.status === "loading"));
  const searchFailed = codeSearch?.query === searchTerm && codeSearch.status === "error";

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
          <div>{g.packageId ? "باقة" : g.lessonId ? "حصة" : "خاص بالمدرس"}</div>
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
          <NeumorphButton
            asChild
            intent="icon"
            size="icon"
            title="عرض التفاصيل والطباعة"
          >
            <Link href={`/teacher/codes/${g.id}`} aria-label={`فتح مجموعة ${g.name || "بدون اسم"}`}>
              <Eye className="h-5 w-5" />
            </Link>
          </NeumorphButton>
        </div>
      ),
    },
  ];

  return (
    <TeacherPage
      activePath="/teacher/codes"
      sectionLabel="إدارة الأكواد"
      pageTitle="مجموعات أكواد المدرس"
      subtitle="عرض دفعات الأكواد الخاصة بك، استخدام الطلاب، وتفاصيل الطباعة."
    >
      {/* Stats */}
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={KeyRound}
          label="إجمالي أكواد المدرس"
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

      <AdminSearchToolbar
        value={searchQuery}
        onChange={setSearchQuery}
        placeholder="ابحث بالكود أو السيريال أو اسم المجموعة أو الباقة..."
      />

      {searchPending && <p role="status" className="mb-4 text-sm text-[var(--admin-muted)]">جارٍ البحث في أكوادك…</p>}
      {searchFailed && (
        <div role="alert" className="mb-4 flex flex-wrap items-center gap-3 text-sm text-[var(--admin-text)]">
          <span>تعذر البحث بالكود أو السيريال. أعد المحاولة.</span>
          <NeumorphButton type="button" intent="ghost" size="sm" onClick={() => setSearchRetry(retry => retry + 1)}>
            إعادة المحاولة
          </NeumorphButton>
        </div>
      )}

      {/* Code Groups Table */}
      <AdminDataTable
        data={filteredGroups}
        columns={groupColumns}
        loading={loading || (searchPending && filteredGroups.length === 0)}
        rowKey={(g) => g.id}
        emptyMessage={searchFailed ? "نتائج البحث بالكود أو السيريال غير متاحة الآن."
          : searchTerm ? "لا توجد مجموعات تطابق البحث. أدخل الكود أو السيريال بالكامل."
            : "لا توجد مجموعات أكواد للمدرس بعد."}
      />
    </TeacherPage>
  );
}
