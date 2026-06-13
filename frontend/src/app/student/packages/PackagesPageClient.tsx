"use client";

import { devConsole } from '@/utils/dev-console';
import { useEffect, useState } from "react";

import {
  PackagesGrid,
  PackagesOverview,
} from "@/components/student-pages/PackagesOverview";
import { type PackageDto, contentService } from "@/services/content-service";

export default function PackagesPageClient() {
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    contentService
      .getPackages()
      .then((res) => setPackages(res.data?.data || []))
      .catch((err) => devConsole.error(err))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-72 rounded-2xl bg-[var(--admin-card-strong)]" />
        <div className="grid gap-6 xl:grid-cols-2">
          {[1, 2].map((i) => (
            <div key={i} className="h-80 rounded-[30px] bg-[var(--admin-card-strong)]" />
          ))}
        </div>
      </div>
    );
  }

  const enrolledPackages = packages.filter((pkg) => pkg.isEnrolled);
  const lockedPackages = packages.filter((pkg) => !pkg.isEnrolled);

  return (
    <div className="space-y-12 pb-10">
      <PackagesOverview packages={packages} />

      <PackagesGrid
        title="الباقات المفعّلة"
        description="هذه هي الباقات التي يمكنك دخولها الآن مباشرة."
        packages={enrolledPackages}
        actionLabel="دخول الباقة"
        emptyTitle="لا توجد باقات مفعّلة بعد"
        emptyDescription="بمجرد تفعيل كود صالح ستظهر الباقات النشطة هنا بشكل مرتب."
        getHref={(packageId) => `/student/packages/${packageId}`}
      />

      <PackagesGrid
        title="باقات تحتاج تفعيل"
        description="هذه الباقات متاحة على المنصة لكنها تحتاج كود أو شراء للوصول إليها."
        packages={lockedPackages}
        actionLabel="استعرض الباقة"
        emptyTitle="كل الباقات الحالية مفعّلة"
        emptyDescription="ممتاز. لا توجد باقات إضافية مقفولة في هذه اللحظة."
        getHref={(packageId) => `/student/packages/${packageId}`}
      />
    </div>
  );
}
