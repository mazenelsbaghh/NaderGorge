'use client';

import { useState, useEffect } from 'react';
import { adminService, CodeGroupDto, CodeDetailDto } from '@/services/admin-service';
import { motion, AnimatePresence } from 'framer-motion';

export default function AdminCodesPage() {
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [loading, setLoading] = useState(true);

  // Generation Form State
  const [showGenModal, setShowGenModal] = useState(false);
  const [genPackageId, setGenPackageId] = useState('');
  const [genLessonId, setGenLessonId] = useState('');
  const [genCount, setGenCount] = useState(10);
  const [genLoading, setGenLoading] = useState(false);

  // Detail Modal State
  const [selectedGroup, setSelectedGroup] = useState<CodeGroupDto | null>(null);
  const [codes, setCodes] = useState<CodeDetailDto[]>([]);
  const [codesLoading, setCodesLoading] = useState(false);

  useEffect(() => {
    loadGroups();
  }, []);

  async function loadGroups() {
    try {
      setLoading(true);
      const data = await adminService.listCodeGroups();
      setGroups(data);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }

  async function handleGenerate(e: React.FormEvent) {
    e.preventDefault();
    if (!genPackageId) return alert('Package ID mapping is highly recommended.');
    
    try {
      setGenLoading(true);
      await adminService.bulkGenerateCodes({
        count: genCount,
        codeLength: 12,
        packageId: genPackageId,
        lessonId: genLessonId || undefined
      });
      setShowGenModal(false);
      alert('Job pushed to code generation queue successfully! Wait a few seconds then refresh.');
      loadGroups();
    } catch (err) {
      alert('Failed to push job.');
    } finally {
      setGenLoading(false);
    }
  }

  async function openGroupDetails(group: CodeGroupDto) {
    setSelectedGroup(group);
    try {
      setCodesLoading(true);
      const data = await adminService.getCodeGroupDetails(group.id);
      setCodes(data);
    } catch (e) {
      alert('Failed to load group keys');
    } finally {
      setCodesLoading(false);
    }
  }

  function handleExportCsv() {
    if (codes.length === 0) return;
    const header = "Code,IsUsed,UsedAt,UsedByUserId\n";
    const rows = codes.map(c => `${c.code},${c.isUsed},${c.usedAt || ''},${c.usedByUserId || ''}`).join('\n');
    const blob = new Blob([header + rows], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Group_${selectedGroup?.id}_Codes.csv`;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  return (
    <div className="container mx-auto p-6 max-w-6xl space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-extrabold text-gray-900 dark:text-white">Access Codes</h1>
        <button 
          onClick={() => setShowGenModal(true)}
          className="rounded-full bg-indigo-600 px-6 py-2.5 font-bold text-white hover:bg-indigo-700 shadow-xl transition-all hover:scale-105"
        >
          + BullMQ Bulk Generate
        </button>
      </div>

      <div className="overflow-hidden rounded-2xl border border-gray-200 dark:border-gray-800 bg-white/50 dark:bg-gray-900/50 backdrop-blur-md shadow-lg">
        {loading && <div className="p-10 text-center animate-pulse">Loading queue...</div>}
        
        {!loading && (
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-800">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase text-gray-500">Gen Date</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase text-gray-500">Mappings</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase text-gray-500">Usage</th>
                <th className="px-6 py-3 text-right text-xs font-medium uppercase text-gray-500">Exports</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 dark:divide-gray-800 bg-white dark:bg-gray-900">
              {groups.map((g) => (
                <tr key={g.id} className="hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
                  <td className="whitespace-nowrap px-6 py-4 text-sm font-medium">{new Date(g.createdAt).toLocaleString()}</td>
                  <td className="whitespace-nowrap px-6 py-4 text-sm">
                    {g.packageId ? <span className="text-indigo-600 block">Package: {g.packageId.substring(0,8)}</span> : 'All Packages'}
                    {g.lessonId && <span className="text-pink-600 block">Lesson: {g.lessonId.substring(0,8)}</span>}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4">
                    <div className="text-sm font-bold">{g.usedCount} / {g.codeCount}</div>
                    <div className="w-full bg-gray-200 rounded-full h-1.5 mt-1">
                      <div className="bg-indigo-600 h-1.5 rounded-full" style={{ width: `${(g.usedCount / g.codeCount)*100}%` }}></div>
                    </div>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-right">
                    <button onClick={() => openGroupDetails(g)} className="text-indigo-600 hover:underline">
                      View / Export CSV
                    </button>
                  </td>
                </tr>
              ))}
              {groups.length === 0 && (
                <tr>
                  <td colSpan={4} className="p-6 text-center text-gray-500">No code groups generated yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>

      {/* Generation Modal */}
      <AnimatePresence>
        {showGenModal && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
            <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="w-full max-w-md rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-2xl">
              <h3 className="text-xl font-bold mb-4">Batch Generate Codes</h3>
              <form onSubmit={handleGenerate} className="space-y-4">
                <div>
                  <label className="block text-sm font-semibold mb-1">Count of Codes</label>
                  <input type="number" min="1" max="100000" value={genCount} onChange={e => setGenCount(Number(e.target.value))} required className="w-full rounded-xl border p-2 dark:bg-gray-800" />
                  <p className="text-xs text-gray-500 mt-1">Safely queued via generic Redis worker implementation.</p>
                </div>
                <div>
                  <label className="block text-sm font-semibold mb-1">Target Package ID (Guid)</label>
                  <input type="text" value={genPackageId} onChange={e => setGenPackageId(e.target.value)} required placeholder="Required mapping" className="w-full rounded-xl border p-2 dark:bg-gray-800" />
                </div>
                <div>
                  <label className="block text-sm font-semibold mb-1">Target Lesson ID (Guid) [Optional]</label>
                  <input type="text" value={genLessonId} onChange={e => setGenLessonId(e.target.value)} placeholder="Leave blank for entire package" className="w-full rounded-xl border p-2 dark:bg-gray-800" />
                </div>
                
                <div className="flex justify-end pt-4 space-x-3">
                  <button type="button" onClick={() => setShowGenModal(false)} className="px-4 py-2 font-semibold">Cancel</button>
                  <button type="submit" disabled={genLoading} className="rounded-xl bg-indigo-600 px-6 py-2 font-bold text-white hover:bg-indigo-700 disabled:opacity-50">
                    {genLoading ? 'Pushing Job...' : 'Generate Batch'}
                  </button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Detail Modal */}
      <AnimatePresence>
        {selectedGroup && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
            <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="flex flex-col w-full max-w-4xl max-h-[80vh] rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-2xl">
              <div className="flex justify-between items-center mb-6">
                <div>
                  <h3 className="text-xl font-bold">Code Group {selectedGroup.id.substring(0,8)}</h3>
                  <p className="text-sm text-gray-500">{codes.length} total codes loaded.</p>
                </div>
                <div className="flex space-x-3">
                  <button onClick={handleExportCsv} className="rounded-lg bg-green-600 px-4 py-2 text-sm font-bold text-white hover:bg-green-700">Export CSV</button>
                  <button onClick={() => setSelectedGroup(null)} className="w-10 h-10 rounded-full hover:bg-gray-100 flex items-center justify-center text-xl">&times;</button>
                </div>
              </div>

              <div className="overflow-y-auto flex-1 border border-gray-200 rounded-xl dark:border-gray-800">
                {codesLoading ? (
                  <div className="animate-pulse p-10 text-center">Loading codes...</div>
                ) : (
                  <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50 sticky top-0">
                      <tr>
                        <th className="px-4 py-2 text-left text-xs font-semibold text-gray-500">Key</th>
                        <th className="px-4 py-2 text-left text-xs font-semibold text-gray-500">State</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
                      {codes.map(c => (
                        <tr key={c.code} className="hover:bg-gray-50">
                          <td className="px-4 py-2 font-mono text-sm">{c.code}</td>
                          <td className="px-4 py-2 text-sm">
                            {c.isUsed ? <span className="text-red-600">Consumed: {new Date(c.usedAt!).toLocaleDateString()}</span> : <span className="text-green-600 font-semibold">Available</span>}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

    </div>
  );
}
