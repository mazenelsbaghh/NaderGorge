import apiClient from './api-client';

export type RepairStatus = 'queued' | 'diagnosing' | 'repairing' | 'testing' | 'ready' | 'deploying' | 'monitoring' | 'completed' | 'awaiting_approval' | 'failed' | 'rolled_back' | 'duplicate' | 'dismissed' | 'needs_evidence' | 'collecting_evidence';
export interface RepairIncident {
  id: string; source: string; category: string; level: string; status: RepairStatus;
  occurrences: number; attempts: number; firstSeen: string; lastSeen: string; summary: string; releaseId: string;
}
export interface RepairControl { paused: boolean; autoDeploy: boolean; heartbeat: string | null; runner: string }
export interface RepairSynchronization {
  checkedAt: string;
  snapshot: {
    state: 'ready' | 'pending_release' | 'dependencies_changed' | 'unavailable' | 'storage_low' | 'release_failed';
    sharedCommit: string;
    nodes: { nodeId: string; releaseId: string }[];
  };
}
export interface RepairOverview {
  synchronization: RepairSynchronization | null;
  lastSynchronized: RepairSynchronization | null;
  control: RepairControl; incidents: RepairIncident[]; total: number;
  counts: { status: RepairStatus; count: number }[];
}
export interface RepairDetail {
  id: string; status: RepairStatus; evidence: string; summary: string; proposalHash: string; approvedHash: string; releaseId: string;
  additionalEvidence?: string[];
  events: { id: number; timestamp: string; status: RepairStatus | 'evidence' | 'collection_closed'; detail: string; actor: string }[];
}
export async function getRepairs(status: string, page: number) {
  return (await apiClient.get<{ data: RepairOverview }>('/admin/auto-repair', { params: { status, page } })).data.data;
}
export async function getRepair(id: string) {
  return (await apiClient.get<{ data: RepairDetail }>(`/admin/auto-repair/${id}`)).data.data;
}
export async function setRepairControl(control: Pick<RepairControl, 'paused' | 'autoDeploy'>) {
  await apiClient.put('/admin/auto-repair/control', control);
}
export async function decideRepair(id: string, decision: { action: 'approve' | 'retry' | 'dismiss' | 'supply_evidence'; evidence?: string; reason?: string; proposalHash?: string; confirmation?: string }) {
  await apiClient.post(`/admin/auto-repair/${id}/decision`, decision);
}
