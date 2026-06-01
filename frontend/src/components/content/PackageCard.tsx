'use client';

import { PackageDto } from '@/services/content-service';
import { motion } from 'framer-motion';
import Image from 'next/image';

export function PackageCard({ pkg, onClick }: { pkg: PackageDto; onClick: () => void }) {
  return (
    <motion.button
      type="button"
      whileHover={{ y: -4 }}
      className="group relative flex flex-col overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] text-right shadow-sm transition-all hover:shadow-xl cursor-pointer focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
      onClick={onClick}
    >
      {/* Image Header */}
      <div className="relative aspect-[16/9] w-full overflow-hidden bg-[var(--admin-card-strong)]">
        <Image 
          src={pkg.imageUrl || '/images/default-package.png'} 
          alt={pkg.name} 
          fill
          className="object-cover transition-transform duration-500 group-hover:scale-105"
          sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
        />
        {/* Status Badge */}
        <div className="absolute top-4 right-4">
           <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-bold shadow-sm backdrop-blur-md ${
             pkg.isEnrolled 
               ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)] border border-[var(--admin-success-20)]' 
               : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)] border border-[var(--admin-danger-20)]'
           }`}>
             {pkg.isEnrolled ? 'مفعلة' : 'تحتاج كود'}
           </span>
        </div>
      </div>

      {/* Body */}
      <div className="flex flex-col flex-grow p-6">
        <div className="mb-3 flex items-start justify-between gap-4">
          <h3 className="text-xl font-bold text-[var(--admin-text)] leading-tight line-clamp-2">
            {pkg.name}
          </h3>
          <span className="shrink-0 text-lg font-black text-[var(--admin-primary)]">
            {pkg.price.toFixed(0)} ج.م
          </span>
        </div>
        
        <p className="line-clamp-2 text-sm text-[var(--admin-muted)] leading-relaxed mb-6">
          {pkg.description || 'باقة تعليمية متكاملة لضمان التفوق الأكاديمي. تتضمن الشرح والتدريبات اللازمة لاجتياز الاختبارات بامتياز.'}
        </p>

        {/* Footer / CTA */}
        <div className="mt-auto pt-4 border-t border-[var(--admin-border)]">
          <button className={`w-full rounded-xl px-4 py-3 text-sm font-bold transition-colors ${
            pkg.isEnrolled
              ? 'bg-[var(--admin-card-strong)] text-[var(--admin-primary)] group-hover:bg-[var(--admin-primary)] group-hover:text-[var(--admin-primary-contrast)]'
              : 'bg-[var(--admin-card-strong)] text-[var(--admin-text)] group-hover:bg-[var(--admin-card-strong)]'
          }`}>
            {pkg.isEnrolled ? 'دخول الباقة' : 'تفعيل بالكود'}
          </button>
        </div>
      </div>
    </motion.button>
  );
}
