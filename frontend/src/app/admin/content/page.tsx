'use client';

import { useState, useEffect } from 'react';
import { adminService } from '@/services/admin-service';
import { contentService, PackageDto, ContentSectionDto, LessonSummaryDto } from '@/services/content-service';
import { motion, AnimatePresence } from 'framer-motion';

type ActiveTab = 'packages' | 'sections' | 'lessons' | 'videos';

export default function AdminContentPage() {
  const [tab, setTab] = useState<ActiveTab>('packages');

  // Packages
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [pkgLoading, setPkgLoading] = useState(true);

  // Sections (selected package)
  const [selectedPkgId, setSelectedPkgId] = useState<string | null>(null);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [secLoading, setSecLoading] = useState(false);

  // Lessons (selected section)
  const [selectedSecId, setSelectedSecId] = useState<string | null>(null);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [lesLoading, setLesLoading] = useState(false);

  // Create Modals
  const [showCreatePkg, setShowCreatePkg] = useState(false);
  const [showCreateSec, setShowCreateSec] = useState(false);
  const [showCreateLes, setShowCreateLes] = useState(false);
  const [showCreateVid, setShowCreateVid] = useState(false);

  // Form states
  const [formName, setFormName] = useState('');
  const [formDesc, setFormDesc] = useState('');
  const [formPrice, setFormPrice] = useState(0);
  const [formProgramId, setFormProgramId] = useState('');
  const [formTitle, setFormTitle] = useState('');
  const [formOrder, setFormOrder] = useState(1);
  const [formSummary, setFormSummary] = useState('');
  const [formProvider, setFormProvider] = useState('YouTube');
  const [formVideoId, setFormVideoId] = useState('');
  const [formLimit, setFormLimit] = useState(3);
  const [formLessonId, setFormLessonId] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    loadPackages();
  }, []);

  async function loadPackages() {
    setPkgLoading(true);
    try {
      const res = await contentService.getPackages();
      setPackages(res.data?.data || []);
    } catch { /* ignore */ } finally {
      setPkgLoading(false);
    }
  }

  async function loadSections(pkgId: string) {
    setSelectedPkgId(pkgId);
    setTab('sections');
    setSecLoading(true);
    try {
      const res = await contentService.getSections(pkgId);
      setSections(res.data?.data || []);
    } catch { /* ignore */ } finally {
      setSecLoading(false);
    }
  }

  async function loadLessons(secId: string) {
    setSelectedSecId(secId);
    setTab('lessons');
    setLesLoading(true);
    try {
      const res = await contentService.getLessons(secId);
      setLessons(res.data?.data || []);
    } catch { /* ignore */ } finally {
      setLesLoading(false);
    }
  }

  function resetForm() {
    setFormName(''); setFormDesc(''); setFormPrice(0); setFormProgramId('');
    setFormTitle(''); setFormOrder(1); setFormSummary('');
    setFormProvider('YouTube'); setFormVideoId(''); setFormLimit(3); setFormLessonId('');
  }

  async function handleCreatePackage(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await adminService.createPackage({ name: formName, description: formDesc, price: formPrice, programId: formProgramId });
      setShowCreatePkg(false);
      resetForm();
      loadPackages();
    } catch { alert('Failed to create package'); } finally { setSaving(false); }
  }

  async function handleCreateSection(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await adminService.createSection({ title: formTitle, order: formOrder, packageId: selectedPkgId });
      setShowCreateSec(false);
      resetForm();
      if (selectedPkgId) loadSections(selectedPkgId);
    } catch { alert('Failed to create section'); } finally { setSaving(false); }
  }

  async function handleCreateLesson(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await adminService.createLesson({ title: formTitle, summary: formSummary, order: formOrder, sectionId: selectedSecId });
      setShowCreateLes(false);
      resetForm();
      if (selectedSecId) loadLessons(selectedSecId);
    } catch { alert('Failed to create lesson'); } finally { setSaving(false); }
  }

  async function handleCreateVideo(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await adminService.createVideo({ title: formTitle, provider: formProvider, urlOrEmbedCode: formVideoId, order: formOrder, limit: formLimit, lessonId: formLessonId });
      setShowCreateVid(false);
      resetForm();
    } catch { alert('Failed to create video'); } finally { setSaving(false); }
  }

  const tabs: { key: ActiveTab; label: string }[] = [
    { key: 'packages', label: 'Packages' },
    { key: 'sections', label: 'Sections' },
    { key: 'lessons', label: 'Lessons' },
    { key: 'videos', label: 'Videos' }
  ];

  return (
    <div className="container mx-auto p-6 max-w-6xl space-y-6">
      <h1 className="text-3xl font-extrabold text-gray-900 dark:text-white">Content Management</h1>

      {/* Breadcrumb & Tabs */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-2">
        <button onClick={() => { setTab('packages'); setSelectedPkgId(null); setSelectedSecId(null); }} className="hover:text-indigo-600 font-semibold">All Packages</button>
        {selectedPkgId && <><span>/</span><button onClick={() => loadSections(selectedPkgId)} className="hover:text-indigo-600 font-semibold">Sections</button></>}
        {selectedSecId && <><span>/</span><span className="font-semibold text-gray-900 dark:text-white">Lessons</span></>}
      </div>

      <div className="flex gap-2 border-b border-gray-200 dark:border-gray-700 pb-1 mb-6">
        {tabs.map(t => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-4 py-2 text-sm font-semibold rounded-t-lg transition-colors ${tab === t.key ? 'bg-indigo-600 text-white' : 'text-gray-500 hover:text-gray-900 dark:hover:text-white'}`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Content Area */}
      {tab === 'packages' && (
        <div className="space-y-4">
          <div className="flex justify-end">
            <button onClick={() => setShowCreatePkg(true)} className="rounded-full bg-indigo-600 px-5 py-2 text-sm font-bold text-white hover:bg-indigo-700 shadow-lg transition hover:scale-105">+ New Package</button>
          </div>
          {pkgLoading ? <div className="p-10 text-center animate-pulse">Loading...</div> : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {packages.map(p => (
                <motion.div key={p.id} whileHover={{ scale: 1.02 }} className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 shadow-sm cursor-pointer hover:shadow-xl transition-shadow" onClick={() => loadSections(p.id)}>
                  <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-2">{p.name}</h3>
                  <p className="text-sm text-gray-500 line-clamp-2 mb-4">{p.description}</p>
                  <div className="flex justify-between items-center">
                    <span className="font-bold text-indigo-600">{p.price} EGP</span>
                    <span className="text-xs text-gray-400">Click to manage →</span>
                  </div>
                </motion.div>
              ))}
              {packages.length === 0 && <div className="col-span-full text-center py-12 border-2 border-dashed rounded-2xl text-gray-400">No packages yet. Create your first!</div>}
            </div>
          )}
        </div>
      )}

      {tab === 'sections' && selectedPkgId && (
        <div className="space-y-4">
          <div className="flex justify-end">
            <button onClick={() => { resetForm(); setShowCreateSec(true); }} className="rounded-full bg-purple-600 px-5 py-2 text-sm font-bold text-white hover:bg-purple-700 shadow-lg transition hover:scale-105">+ New Section</button>
          </div>
          {secLoading ? <div className="p-10 text-center animate-pulse">Loading...</div> : (
            <div className="space-y-3">
              {sections.sort((a, b) => a.order - b.order).map((s, i) => (
                <motion.div key={s.id} initial={{ opacity: 0, x: -20 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: i * 0.05 }}
                  className="flex items-center justify-between rounded-xl border p-4 bg-white dark:bg-gray-900 dark:border-gray-800 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/50 transition"
                  onClick={() => loadLessons(s.id)}
                >
                  <div className="flex items-center gap-4">
                    <span className="flex h-8 w-8 items-center justify-center rounded-full bg-purple-100 text-purple-800 text-sm font-bold">{s.order}</span>
                    <span className="font-semibold">{s.title}</span>
                  </div>
                  <span className="text-xs text-gray-400">View Lessons →</span>
                </motion.div>
              ))}
              {sections.length === 0 && <div className="text-center py-12 border-2 border-dashed rounded-2xl text-gray-400">No sections. Add one!</div>}
            </div>
          )}
        </div>
      )}

      {tab === 'lessons' && selectedSecId && (
        <div className="space-y-4">
          <div className="flex justify-end gap-3">
            <button onClick={() => { resetForm(); setShowCreateVid(true); }} className="rounded-full bg-pink-600 px-5 py-2 text-sm font-bold text-white hover:bg-pink-700 shadow-lg transition hover:scale-105">+ Add Video</button>
            <button onClick={() => { resetForm(); setShowCreateLes(true); }} className="rounded-full bg-teal-600 px-5 py-2 text-sm font-bold text-white hover:bg-teal-700 shadow-lg transition hover:scale-105">+ New Lesson</button>
          </div>
          {lesLoading ? <div className="p-10 text-center animate-pulse">Loading...</div> : (
            <div className="space-y-3">
              {lessons.sort((a, b) => a.order - b.order).map((l, i) => (
                <motion.div key={l.id} initial={{ opacity: 0, x: -20 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: i * 0.05 }}
                  className="flex items-center justify-between rounded-xl border p-4 bg-white dark:bg-gray-900 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition"
                >
                  <div className="flex items-center gap-4">
                    <span className="flex h-8 w-8 items-center justify-center rounded-full bg-teal-100 text-teal-800 text-sm font-bold">{l.order}</span>
                    <div>
                      <span className="font-semibold block">{l.title}</span>
                      <span className="text-xs text-gray-400">{l.summary?.substring(0, 80)}</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    {l.isCompleted && <span className="text-xs text-green-600 font-semibold">✓ Completed</span>}
                    <span className="text-xs text-gray-400 font-mono">{l.id.substring(0, 8)}</span>
                  </div>
                </motion.div>
              ))}
              {lessons.length === 0 && <div className="text-center py-12 border-2 border-dashed rounded-2xl text-gray-400">No lessons yet. Create one!</div>}
            </div>
          )}
        </div>
      )}

      {tab === 'videos' && (
        <div className="space-y-4">
          <div className="flex justify-end">
            <button onClick={() => { resetForm(); setShowCreateVid(true); }} className="rounded-full bg-pink-600 px-5 py-2 text-sm font-bold text-white hover:bg-pink-700 shadow-lg transition hover:scale-105">+ Add Video</button>
          </div>
          <div className="text-center py-12 border-2 border-dashed rounded-2xl text-gray-400">
            Use the content hierarchy (Packages → Sections → Lessons) to manage videos within lessons. Or use the quick-add button above.
          </div>
        </div>
      )}

      {/* Create Package Modal */}
      <AnimatePresence>
        {showCreatePkg && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
            <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="w-full max-w-lg rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-2xl">
              <h3 className="text-xl font-bold mb-4">Create Package</h3>
              <form onSubmit={handleCreatePackage} className="space-y-4">
                <input type="text" placeholder="Package Name" value={formName} onChange={e => setFormName(e.target.value)} required className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <textarea placeholder="Description" value={formDesc} onChange={e => setFormDesc(e.target.value)} rows={3} className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <div className="flex gap-4">
                  <input type="number" placeholder="Price (EGP)" value={formPrice} onChange={e => setFormPrice(Number(e.target.value))} min={0} className="flex-1 rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                  <input type="text" placeholder="Program ID (GUID)" value={formProgramId} onChange={e => setFormProgramId(e.target.value)} required className="flex-1 rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                </div>
                <div className="flex justify-end gap-3 pt-2">
                  <button type="button" onClick={() => setShowCreatePkg(false)} className="px-4 py-2 font-semibold">Cancel</button>
                  <button type="submit" disabled={saving} className="rounded-xl bg-indigo-600 px-6 py-2 font-bold text-white disabled:opacity-50">{saving ? 'Saving...' : 'Create'}</button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Create Section Modal */}
      <AnimatePresence>
        {showCreateSec && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
            <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="w-full max-w-lg rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-2xl">
              <h3 className="text-xl font-bold mb-4">Create Section</h3>
              <form onSubmit={handleCreateSection} className="space-y-4">
                <input type="text" placeholder="Section Title" value={formTitle} onChange={e => setFormTitle(e.target.value)} required className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <input type="number" placeholder="Order" value={formOrder} onChange={e => setFormOrder(Number(e.target.value))} min={1} className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <div className="flex justify-end gap-3 pt-2">
                  <button type="button" onClick={() => setShowCreateSec(false)} className="px-4 py-2 font-semibold">Cancel</button>
                  <button type="submit" disabled={saving} className="rounded-xl bg-purple-600 px-6 py-2 font-bold text-white disabled:opacity-50">{saving ? 'Saving...' : 'Create'}</button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Create Lesson Modal */}
      <AnimatePresence>
        {showCreateLes && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
            <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="w-full max-w-lg rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-2xl">
              <h3 className="text-xl font-bold mb-4">Create Lesson</h3>
              <form onSubmit={handleCreateLesson} className="space-y-4">
                <input type="text" placeholder="Lesson Title" value={formTitle} onChange={e => setFormTitle(e.target.value)} required className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <textarea placeholder="Summary" value={formSummary} onChange={e => setFormSummary(e.target.value)} rows={3} className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <input type="number" placeholder="Order" value={formOrder} onChange={e => setFormOrder(Number(e.target.value))} min={1} className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <div className="flex justify-end gap-3 pt-2">
                  <button type="button" onClick={() => setShowCreateLes(false)} className="px-4 py-2 font-semibold">Cancel</button>
                  <button type="submit" disabled={saving} className="rounded-xl bg-teal-600 px-6 py-2 font-bold text-white disabled:opacity-50">{saving ? 'Saving...' : 'Create'}</button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Create Video Modal */}
      <AnimatePresence>
        {showCreateVid && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
            <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="w-full max-w-lg rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-2xl">
              <h3 className="text-xl font-bold mb-4">Add Video to Lesson</h3>
              <form onSubmit={handleCreateVideo} className="space-y-4">
                <input type="text" placeholder="Video Title" value={formTitle} onChange={e => setFormTitle(e.target.value)} required className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <input type="text" placeholder="Lesson ID (GUID)" value={formLessonId} onChange={e => setFormLessonId(e.target.value)} required className="w-full rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                <div className="flex gap-4">
                  <select value={formProvider} onChange={e => setFormProvider(e.target.value)} className="flex-1 rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700">
                    <option value="YouTube">YouTube</option>
                    <option value="Vimeo">Vimeo</option>
                    <option value="Custom">Custom</option>
                  </select>
                  <input type="text" placeholder="Video ID / URL" value={formVideoId} onChange={e => setFormVideoId(e.target.value)} required className="flex-1 rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                </div>
                <div className="flex gap-4">
                  <input type="number" placeholder="Order" value={formOrder} onChange={e => setFormOrder(Number(e.target.value))} min={1} className="flex-1 rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                  <input type="number" placeholder="Watch Limit" value={formLimit} onChange={e => setFormLimit(Number(e.target.value))} min={1} className="flex-1 rounded-xl border p-3 dark:bg-gray-800 dark:border-gray-700" />
                </div>
                <div className="flex justify-end gap-3 pt-2">
                  <button type="button" onClick={() => setShowCreateVid(false)} className="px-4 py-2 font-semibold">Cancel</button>
                  <button type="submit" disabled={saving} className="rounded-xl bg-pink-600 px-6 py-2 font-bold text-white disabled:opacity-50">{saving ? 'Saving...' : 'Add Video'}</button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
