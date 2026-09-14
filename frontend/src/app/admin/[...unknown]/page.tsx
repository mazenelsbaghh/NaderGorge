import { redirect } from 'next/navigation';

export default function UnknownAdminPage() {
  redirect('/admin/unauthorized');
}
