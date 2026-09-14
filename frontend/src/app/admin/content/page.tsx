import { Suspense } from 'react';
import AdminContentPageClient from "./AdminContentPageClient";

export default function AdminContentPage() {
  return (
    <Suspense fallback={null}>
      <AdminContentPageClient />
    </Suspense>
  );
}
