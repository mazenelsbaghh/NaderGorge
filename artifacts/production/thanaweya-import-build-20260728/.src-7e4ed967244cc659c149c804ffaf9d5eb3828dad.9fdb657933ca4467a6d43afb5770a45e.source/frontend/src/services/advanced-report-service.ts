import apiClient from './api-client';

export type ReportAudience = 'admin' | 'teacher';
export type ReportLogicalOperator = 'and' | 'or';
export type AdvancedReportFilterValue = string | number | boolean;
export type ReportFilterOperator =
  | 'eq' | 'neq' | 'contains' | 'in' | 'gt' | 'gte' | 'lt' | 'lte'
  | 'before' | 'after' | 'between' | 'is-empty' | 'not-empty';

export interface AdvancedReportFilter {
  id: string;
  field: string;
  operator: ReportFilterOperator;
  values: AdvancedReportFilterValue[];
}

export interface AdvancedReportFilterGroup {
  id: string;
  logic: ReportLogicalOperator;
  filters: AdvancedReportFilter[];
  groups: AdvancedReportFilterGroup[];
}

export interface AdvancedReportQuery {
  domain: string;
  filterGroup: AdvancedReportFilterGroup;
  columns: string[];
  sort?: { field: string; direction: 'asc' | 'desc' };
  page: number;
  pageSize: number;
}

export interface AdvancedReportColumn {
  key: string;
  label: string;
  type?: 'text' | 'number' | 'currency' | 'percentage' | 'date' | 'datetime' | 'status';
}

export interface AdvancedReportKpi {
  key: string;
  label: string;
  value: string | number;
  change?: number;
  format?: 'number' | 'currency' | 'percentage' | 'duration';
}

export interface AdvancedReportChartPoint {
  label: string;
  value: number;
  secondaryValue?: number;
}

export interface AdvancedReportResult {
  summary: AdvancedReportKpi[];
  chart: { type?: string; label: string; title?: string; valueLabel?: string; points: AdvancedReportChartPoint[] };
  columns: AdvancedReportColumn[];
  rows: Array<Record<string, unknown>>;
  page: number;
  pageSize: number;
  totalCount: number;
  generatedAtCairo?: string;
  appliedFilters?: string[];
  isTruncated?: boolean;
  notice?: string | null;
}

export interface SavedAdvancedReport {
  id: string;
  name: string;
  description?: string;
  domain: string;
  configuration: AdvancedReportQuery;
  schemaVersion?: number;
  version?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface AdvancedReportCatalogField {
  key: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'boolean';
  operators: ReportFilterOperator[];
  isSensitive?: boolean;
}

export interface AdvancedReportCatalogDomain {
  key: string;
  label: string;
  description: string;
  isAvailable: boolean;
  fields: AdvancedReportCatalogField[];
  defaultColumns: string[];
}

export interface AdvancedReportCatalog {
  domains: AdvancedReportCatalogDomain[];
  timeZone: string;
}

export interface AdvancedReportFilterOption {
  field: string;
  value: string;
  label: string;
}

export interface AdvancedReportFilterOptions {
  options: AdvancedReportFilterOption[];
}

type ApiEnvelope<T> = { data?: T; success?: boolean; message?: string };
const endpoint = (audience: ReportAudience) => `/${audience}/reports`;
const unwrap = <T>(payload: ApiEnvelope<T> | T): T =>
  payload && typeof payload === 'object' && 'data' in payload
    ? (payload as ApiEnvelope<T>).data as T
    : payload as T;

type SerializedFilterGroup = {
  logic: ReportLogicalOperator;
  filters: Array<{ field: string; operator: ReportFilterOperator; values: AdvancedReportFilterValue[] }>;
  groups: SerializedFilterGroup[];
};

const expandSelectedValues = (values: AdvancedReportFilterValue[]) => values.reduce<AdvancedReportFilterValue[]>((expanded, selectedValue) => {
  if (typeof selectedValue !== 'string') return [...expanded, selectedValue];
  return [...expanded, ...selectedValue.split(/[,،]/u).map((part) => part.trim()).filter(Boolean)];
}, []);

const serializeGroup = (group: AdvancedReportFilterGroup): SerializedFilterGroup => ({
  logic: group.logic,
  filters: group.filters.map((filter) => ({
    field: filter.field,
    operator: filter.operator,
    values: filter.operator === 'in'
      ? expandSelectedValues(filter.values)
      : filter.values,
  })),
  groups: group.groups.map(serializeGroup),
});

const serializeQuery = (query: AdvancedReportQuery) => ({
  ...query,
  filterGroup: serializeGroup(query.filterGroup),
});

export const advancedReportService = {
  async getCatalog(audience: ReportAudience) {
    const response = await apiClient.get<ApiEnvelope<AdvancedReportCatalog>>(`${endpoint(audience)}/catalog`);
    return unwrap(response.data);
  },
  async getFilterOptions(audience: ReportAudience) {
    const response = await apiClient.get<ApiEnvelope<AdvancedReportFilterOptions>>(`${endpoint(audience)}/filter-options`);
    return unwrap(response.data);
  },
  async run(audience: ReportAudience, query: AdvancedReportQuery) {
    const response = await apiClient.post<ApiEnvelope<AdvancedReportResult>>(
      `${endpoint(audience)}/execute`, serializeQuery(query),
    );
    return unwrap(response.data);
  },
  async listSaved(audience: ReportAudience) {
    const response = await apiClient.get<ApiEnvelope<SavedAdvancedReport[]>>(
      `${endpoint(audience)}/definitions`,
    );
    return unwrap(response.data) ?? [];
  },
  async save(audience: ReportAudience, payload: { name: string; configuration: AdvancedReportQuery }) {
    const response = await apiClient.post<ApiEnvelope<SavedAdvancedReport>>(
      `${endpoint(audience)}/definitions`, { ...payload, configuration: serializeQuery(payload.configuration) },
    );
    return unwrap(response.data);
  },
  async update(audience: ReportAudience, id: string, payload: { name: string; configuration: AdvancedReportQuery; version?: number }) {
    const response = await apiClient.put<ApiEnvelope<SavedAdvancedReport>>(
      `${endpoint(audience)}/definitions/${id}`, { ...payload, configuration: serializeQuery(payload.configuration) },
    );
    return unwrap(response.data);
  },
  async remove(audience: ReportAudience, id: string) {
    await apiClient.delete(`${endpoint(audience)}/definitions/${id}`);
  },
  async exportFile(audience: ReportAudience, format: 'xlsx' | 'pdf', query: AdvancedReportQuery) {
    const response = await apiClient.post<Blob>(
      `${endpoint(audience)}/export/${format}`, serializeQuery(query), { responseType: 'blob' },
    );
    return response.data;
  },
  async exportStudentLedger(audience: ReportAudience, teacherId?: string) {
    const response = await apiClient.get<Blob>(`${endpoint(audience)}/student-ledger/export`, {
      params: teacherId ? { teacherId } : undefined,
      responseType: 'blob',
    });
    return response.data;
  },
};
