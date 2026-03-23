'use client';

import { useState } from 'react';
import { CodeActivationForm } from '@/components/forms/CodeActivationForm';
import { ProfileCompletionModal } from '@/components/forms/ProfileCompletionModal';

export default function CodeRedemptionPage() {
  const [showProfileModal, setShowProfileModal] = useState(false);
  const [recentGrants, setRecentGrants] = useState<string[]>([]);

  return (
    <div className="mx-auto max-w-2xl">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
          Redeem Access Code
        </h1>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Enter your access code to unlock packages and lessons.
        </p>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white/80 p-6 shadow-lg backdrop-blur-sm dark:border-gray-700 dark:bg-gray-900/80">
        <CodeActivationForm
          onRequiresProfile={() => setShowProfileModal(true)}
          onSuccess={() => setRecentGrants([...recentGrants, 'New access granted!'])}
        />
      </div>

      {recentGrants.length > 0 && (
        <div className="mt-4 space-y-2">
          {recentGrants.map((msg, i) => (
            <div key={i} className="rounded-xl bg-green-50 border border-green-200 p-3 text-sm text-green-700 dark:bg-green-900/20 dark:border-green-800 dark:text-green-400">
              ✅ {msg}
            </div>
          ))}
        </div>
      )}

      <ProfileCompletionModal
        isOpen={showProfileModal}
        onClose={() => setShowProfileModal(false)}
        onComplete={() => {
          setShowProfileModal(false);
          setRecentGrants([...recentGrants, 'Profile completed! Code activated.']);
        }}
      />
    </div>
  );
}
