import Link from 'next/link';
import { RegistrationForm } from '@/components/forms/RegistrationForm';

export default function RegisterPage() {
  return (
    <div className="flex min-h-[calc(100vh-4rem)] items-center justify-center px-4 py-12">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold bg-gradient-to-r from-blue-600 to-indigo-600 bg-clip-text text-transparent">
            Create Account
          </h1>
          <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
            Join Nader George Educational Platform
          </p>
        </div>

        <div className="rounded-2xl border border-gray-200 bg-white/80 p-6 shadow-xl backdrop-blur-sm dark:border-gray-700 dark:bg-gray-900/80">
          <RegistrationForm />
        </div>

        <p className="mt-4 text-center text-sm text-gray-500 dark:text-gray-400">
          Already have an account?{' '}
          <Link href="/login" className="font-medium text-blue-600 hover:text-blue-700 dark:text-blue-400">
            Sign In
          </Link>
        </p>
      </div>
    </div>
  );
}
