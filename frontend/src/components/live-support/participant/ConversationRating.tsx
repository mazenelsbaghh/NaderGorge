'use client';
import { useState } from 'react';
import { Star } from 'lucide-react';
import { liveSupportService } from '@/services/live-support-service';
export function ConversationRating({ conversationId, onRated }: { conversationId: string; onRated?: () => void }) { const [busy, setBusy] = useState(false); return <fieldset disabled={busy} className="text-center"><legend className="mb-2 text-sm text-slate-600">قيّم تجربتك</legend><div className="flex justify-center gap-1">{[1,2,3,4,5].map((stars) => <button key={stars} type="button" aria-label={`${stars} نجوم`} onClick={() => { setBusy(true); void liveSupportService.submitRating(conversationId, { stars }).then(onRated).finally(() => setBusy(false)); }} className="grid size-11 place-items-center text-amber-600 focus-visible:outline-2"><Star size={22}/></button>)}</div></fieldset>; }
