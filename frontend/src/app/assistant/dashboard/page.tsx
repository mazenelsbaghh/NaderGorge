import { AssistantTaskBoard } from '@/components/assistant/AssistantTaskBoard';

export const metadata = {
  title: 'Assistant Dashboard | Massar Platform',
  description: 'Manage and resolve student issues and academic tasks.',
};

export default function AssistantDashboardPage() {
  return (
    <div className="min-h-screen bg-gray-50 dark:bg-black py-12 px-4 sm:px-6 lg:px-8">
      <AssistantTaskBoard />
    </div>
  );
}
