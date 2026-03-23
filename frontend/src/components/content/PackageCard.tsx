'use client';

import { PackageDto } from '@/services/content-service';
import { motion } from 'framer-motion';

export function PackageCard({ pkg, onClick }: { pkg: PackageDto; onClick: () => void }) {
  return (
    <motion.div
      whileHover={{ scale: 1.02, y: -4 }}
      transition={{ type: 'spring', stiffness: 300 }}
      className={`relative overflow-hidden cursor-pointer rounded-2xl border px-6 py-8 shadow-xl transition-all ${
        pkg.isEnrolled
          ? 'border-green-200 bg-gradient-to-br from-green-50 to-emerald-50 dark:border-green-900/50 dark:from-green-900/20 dark:to-emerald-900/10'
          : 'border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900'
      }`}
      onClick={onClick}
    >
      <div className="mb-4 flex items-center justify-between">
        <span className={`rounded-full px-3 py-1 text-xs font-bold uppercase tracking-wider ${pkg.isEnrolled ? 'bg-green-200/50 text-green-800 dark:bg-green-800/40 dark:text-green-300' : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400'}`}>
          {pkg.isEnrolled ? 'Enrolled' : 'Available'}
        </span>
        <span className="text-lg font-black text-gray-900 dark:text-white">
          ${pkg.price.toFixed(2)}
        </span>
      </div>

      <h3 className="mb-2 text-xl font-bold text-gray-900 dark:text-white">{pkg.name}</h3>
      <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400 line-clamp-3">
        {pkg.description}
      </p>

      {!pkg.isEnrolled && (
        <div className="mt-6 flex justify-end">
          <p className="text-xs font-medium text-blue-600 dark:text-blue-400">Requires Activation Code &rarr;</p>
        </div>
      )}
    </motion.div>
  );
}
