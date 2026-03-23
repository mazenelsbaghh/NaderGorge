'use client';

import { useState, useEffect } from 'react';
import { adminService, QuestionBankItemDto, QuestionOptionDto } from '@/services/admin-service';
import { motion, AnimatePresence } from 'framer-motion';

export default function AdminQuestionsPage() {
  const [questions, setQuestions] = useState<QuestionBankItemDto[]>([]);
  const [loading, setLoading] = useState(true);

  // Form
  const [showModal, setShowModal] = useState(false);
  const [qText, setQText] = useState('');
  const [qPoints, setQPoints] = useState(1);
  const [qTags, setQTags] = useState('');
  const [options, setOptions] = useState<{ text: string; isCorrect: boolean }[]>([
    { text: '', isCorrect: true },
    { text: '', isCorrect: false },
    { text: '', isCorrect: false },
    { text: '', isCorrect: false }
  ]);

  useEffect(() => {
    loadQuestions();
  }, []);

  async function loadQuestions() {
    setLoading(true);
    try {
      const data = await adminService.listQuestions(1, 100, '');
      setQuestions(data.items);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    if (options.filter(o => o.text.trim().length > 0).length < 2) {
      return alert('Provide at least 2 filled options.');
    }
    
    try {
      await adminService.createQuestion({
        text: qText,
        defaultPoints: qPoints,
        tags: qTags,
        options: options.filter(o => o.text.trim().length > 0)
      });
      setShowModal(false);
      
      // Reset
      setQText('');
      setQTags('');
      setQPoints(1);
      setOptions([
        { text: '', isCorrect: true },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false }
      ]);
      loadQuestions();
    } catch (e) {
      alert('Failed to save question');
    }
  }

  function updateOption(idx: number, prop: keyof QuestionOptionDto, val: any) {
    const list = [...options];
    list[idx] = { ...list[idx], [prop]: val };
    
    // Toggle others logic for MCQ if single select wanted (usually one correct)
    if (prop === 'isCorrect' && val === true) {
      list.forEach((o, i) => { if (i !== idx) o.isCorrect = false; });
    }
    setOptions(list);
  }

  return (
    <div className="container mx-auto p-6 max-w-6xl space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-3xl font-extrabold text-gray-900 dark:text-white">Question Bank</h1>
          <p className="text-gray-500">Create global multiple-choice questions to attach dynamically to exams.</p>
        </div>
        <button 
          onClick={() => setShowModal(true)}
          className="rounded-full bg-blue-600 px-6 py-2.5 font-bold text-white hover:bg-blue-700 shadow-xl transition-all hover:scale-105"
        >
          + Add Question
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {loading && <div className="col-span-1 md:col-span-2 text-center p-10">Loading bank...</div>}
        
        {!loading && questions.map(q => (
          <div key={q.id} className="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm hover:shadow-lg transition">
            <div className="flex justify-between items-start mb-4">
              <span className="inline-block rounded-md bg-purple-100 text-purple-800 px-2 py-1 text-xs font-bold">{q.tags || 'General'}</span>
              <span className="text-sm font-semibold text-gray-500">{q.defaultPoints} pts</span>
            </div>
            <h3 className="text-lg font-bold text-gray-900 mb-4">{q.text}</h3>
            <div className="space-y-2">
              {q.options.map((o, idx) => (
                <div key={idx} className={`flex items-center p-3 rounded-lg border ${o.isCorrect ? 'border-green-400 bg-green-50' : 'border-gray-100 bg-gray-50'}`}>
                  <div className={`w-4 h-4 rounded-full flex-shrink-0 mr-3 ${o.isCorrect ? 'bg-green-500' : 'bg-gray-300'}`}></div>
                  <span className="text-sm font-medium">{o.text}</span>
                </div>
              ))}
            </div>
          </div>
        ))}
        {!loading && questions.length === 0 && (
          <div className="col-span-1 md:col-span-2 text-center py-12 border-2 border-dashed rounded-2xl">
            Empty question bank. Add your first item!
          </div>
        )}
      </div>

      <AnimatePresence>
        {showModal && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm overflow-y-auto">
            <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="w-full max-w-2xl rounded-2xl bg-white dark:bg-gray-900 p-8 shadow-2xl my-8">
              <h2 className="text-2xl font-bold mb-6">Create MCQ Item</h2>
              
              <form onSubmit={handleSave} className="space-y-6">
                <div>
                  <label className="block text-sm font-bold mb-2">Question Text</label>
                  <textarea 
                    value={qText} onChange={e => setQText(e.target.value)} required rows={3}
                    className="w-full rounded-xl border-gray-300 border p-4 shadow-inner" placeholder="E.g., What is the capital of France?"
                  />
                </div>
                
                <div className="flex gap-4">
                  <div className="flex-1">
                    <label className="block text-sm font-bold mb-2">Tags</label>
                    <input type="text" value={qTags} onChange={e => setQTags(e.target.value)} placeholder="E.g., Geography, Unit1" className="w-full rounded-xl border p-3" />
                  </div>
                  <div className="w-32">
                    <label className="block text-sm font-bold mb-2">Points</label>
                    <input type="number" min="0" step="0.5" value={qPoints} onChange={e => setQPoints(parseFloat(e.target.value))} required className="w-full rounded-xl border p-3" />
                  </div>
                </div>

                <div className="pt-4 border-t">
                  <h3 className="font-bold mb-4 text-gray-700">Answer Options</h3>
                  <div className="space-y-3">
                    {options.map((o, i) => (
                      <div key={i} className="flex items-center gap-3">
                        <input 
                          type="radio" 
                          name="correctOption" 
                          checked={o.isCorrect} 
                          onChange={(e) => updateOption(i, 'isCorrect', e.target.checked)}
                          className="w-5 h-5 text-green-600 focus:ring-green-500 cursor-pointer" 
                        />
                        <input 
                          type="text" 
                          value={o.text} 
                          onChange={(e) => updateOption(i, 'text', e.target.value)} 
                          placeholder={`Option ${i+1}`}
                          required={i < 2} // first two are required minimum
                          className={`flex-1 rounded-xl border p-3 shadow-sm ${o.isCorrect ? 'border-green-400 focus:border-green-500' : 'border-gray-200'}`} 
                        />
                      </div>
                    ))}
                  </div>
                </div>

                <div className="flex justify-end gap-3 pt-6">
                  <button type="button" onClick={() => setShowModal(false)} className="px-6 py-3 font-semibold text-gray-600 rounded-xl hover:bg-gray-100">Cancel</button>
                  <button type="submit" className="rounded-xl bg-blue-600 px-8 py-3 font-bold text-white shadow-lg hover:bg-blue-700 hover:scale-105 transition-transform">
                    Save to Bank
                  </button>
                </div>
              </form>

            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

    </div>
  );
}
