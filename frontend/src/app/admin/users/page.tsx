'use client';

import { useState, useEffect } from 'react';
import { adminService, AdminUserListDto, DeviceDto } from '@/services/admin-service';
import { motion, AnimatePresence } from 'framer-motion';

export default function AdminUsersPage() {
  const [users, setUsers] = useState<AdminUserListDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const [selectedUser, setSelectedUser] = useState<AdminUserListDto | null>(null);
  const [devices, setDevices] = useState<DeviceDto[]>([]);
  
  useEffect(() => {
    loadUsers();
  }, [page, search]);

  async function loadUsers() {
    try {
      setLoading(true);
      const data = await adminService.listUsers(page, 20, search);
      setUsers(data.items);
      setTotalPages(Math.ceil(data.totalCount / data.pageSize));
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }

  async function handleToggleStatus(user: AdminUserListDto) {
    const newStatus = user.status === 'Active' ? 'Disabled' : 'Active';
    try {
      await adminService.updateUserStatus(user.id, newStatus);
      setUsers(users.map(u => u.id === user.id ? { ...u, status: newStatus } : u));
    } catch (e) {
      alert('Failed to update status');
    }
  }

  async function handleViewDevices(user: AdminUserListDto) {
    setSelectedUser(user);
    try {
      const data = await adminService.getUserDevices(user.id);
      setDevices(data);
    } catch (e) {
      alert('Failed to load devices');
    }
  }

  async function handleRemoveDevice(deviceId: string) {
    if (!confirm('Are you sure you want to remove this device?')) return;
    try {
      await adminService.removeDevice(deviceId);
      setDevices(devices.filter(d => d.id !== deviceId));
    } catch (e) {
      alert('Failed to remove device');
    }
  }

  return (
    <div className="container mx-auto p-6 max-w-6xl space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-extrabold text-gray-900 dark:text-white">User Management</h1>
        <div className="relative w-64">
          <input 
            type="text" 
            placeholder="Search phone or name..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }}
            className="w-full rounded-full border border-gray-300 dark:border-gray-700 bg-white dark:bg-gray-800 px-4 py-2 text-sm shadow-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none"
          />
        </div>
      </div>

      <div className="overflow-hidden rounded-2xl border border-gray-200 dark:border-gray-800 bg-white/50 dark:bg-gray-900/50 backdrop-blur-md shadow-lg">
        {loading && <div className="p-10 text-center animate-pulse">Loading users...</div>}
        
        {!loading && (
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-800">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Name / Phone</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Grade / Track</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Status</th>
                <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 dark:divide-gray-800 bg-white dark:bg-gray-900">
              {users.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
                  <td className="whitespace-nowrap px-6 py-4">
                    <div className="text-sm font-bold text-gray-900 dark:text-white">{u.fullName}</div>
                    <div className="text-sm text-gray-500">{u.phoneNumber}</div>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-500">
                    {u.grade} - {u.track}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4">
                    <span className={`inline-flex rounded-full px-2 text-xs font-semibold leading-5 ${u.status === 'Active' ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400' : 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'}`}>
                      {u.status}
                    </span>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-right text-sm font-medium space-x-3">
                    <button onClick={() => handleToggleStatus(u)} className="text-indigo-600 hover:text-indigo-900 dark:text-indigo-400 dark:hover:text-indigo-300">
                      {u.status === 'Active' ? 'Disable' : 'Enable'}
                    </button>
                    <button onClick={() => handleViewDevices(u)} className="text-blue-600 hover:text-blue-900 dark:text-blue-400 dark:hover:text-blue-300">
                      Devices
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <div className="flex justify-between items-center p-4 bg-gray-50 dark:bg-gray-800">
          <button 
            disabled={page === 1}
            onClick={() => setPage(p => p - 1)}
            className="rounded-lg bg-white dark:bg-gray-700 px-4 py-2 text-sm font-semibold border disabled:opacity-50"
          >
            Previous
          </button>
          <span className="text-sm">Page {page} of {totalPages}</span>
          <button 
            disabled={page >= totalPages}
            onClick={() => setPage(p => p + 1)}
            className="rounded-lg bg-white dark:bg-gray-700 px-4 py-2 text-sm font-semibold border disabled:opacity-50"
          >
            Next
          </button>
        </div>
      </div>

      <AnimatePresence>
        {selectedUser && (
          <motion.div 
            initial={{ opacity: 0 }} 
            animate={{ opacity: 1 }} 
            exit={{ opacity: 0 }} 
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
          >
            <motion.div 
              initial={{ scale: 0.95 }} 
              animate={{ scale: 1 }} 
              exit={{ scale: 0.95 }} 
              className="w-full max-w-2xl rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-2xl"
            >
              <div className="flex justify-between items-center mb-6">
                <h3 className="text-xl font-bold">Devices for {selectedUser.fullName}</h3>
                <button onClick={() => setSelectedUser(null)} className="text-gray-500 hover:text-gray-900 w-8 h-8 rounded-full hover:bg-gray-100">&times;</button>
              </div>

              {devices.length === 0 ? (
                <p className="text-center text-gray-500 my-8">No devices registered</p>
              ) : (
                <div className="space-y-4">
                  {devices.map(d => (
                    <div key={d.id} className="flex justify-between items-center rounded-xl border p-4 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/30">
                      <div>
                        <div className="font-semibold">{d.fingerprint}</div>
                        <div className="text-sm text-gray-500">{d.browser} • {d.os}</div>
                        <div className="text-xs text-gray-400 mt-1">Last seen: {new Date(d.lastUsedAt).toLocaleString()}</div>
                      </div>
                      <button 
                         onClick={() => handleRemoveDevice(d.id)}
                         className="rounded-lg bg-red-100 dark:bg-red-900/40 text-red-600 dark:text-red-400 px-3 py-1.5 text-sm font-semibold hover:bg-red-200 dark:hover:bg-red-900/80 transition"
                      >
                         Revoke
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
