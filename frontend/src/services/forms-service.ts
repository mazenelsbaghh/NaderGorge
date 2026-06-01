import apiClient from './api-client';

export type FormFieldType = 'text' | 'longtext' | 'number' | 'email' | 'phone' | 'select' | 'checkbox';

export interface FormFieldConfig {
  id: string;
  type: FormFieldType;
  label: string;
  placeholder?: string;
  isRequired: boolean;
  options: string[]; // For 'select' dropdown options
}

export interface CustomFormDto {
  id: string;
  title: string;
  description: string;
  slug: string;
  isActive: boolean;
  submissionCount: number;
  createdAt: string;
}

export interface CustomFormDetailDto {
  id: string;
  title: string;
  description: string;
  slug: string;
  isActive: boolean;
  fieldsJson: string; // JSON array of FormFieldConfig
  createdAt: string;
}

export type FormSubmissionStatus = 'Pending' | 'Reviewed' | 'Accepted' | 'Rejected';

export interface FormSubmissionDto {
  id: string;
  customFormId: string;
  submittedDataJson: string; // JSON mapping field IDs to values
  status: FormSubmissionStatus;
  adminNotes?: string;
  submittedAt: string;
}

export interface PublicFormDto {
  id: string;
  title: string;
  description: string;
  fieldsJson: string; // JSON array of FormFieldConfig
}

/**
 * Admin: Get all custom forms
 */
export async function getAdminForms(): Promise<CustomFormDto[]> {
  const { data } = await apiClient.get<{ data: CustomFormDto[] }>('/admin/forms');
  return data.data;
}

/**
 * Admin: Get details of a single custom form
 */
export async function getAdminFormDetails(id: string): Promise<CustomFormDetailDto> {
  const { data } = await apiClient.get<{ data: CustomFormDetailDto }>(`/admin/forms/${id}`);
  return data.data;
}

/**
 * Admin: Create a new custom form
 */
export async function createAdminForm(form: {
  title: string;
  description: string;
  slug: string;
  isActive: boolean;
  fieldsJson: string;
}): Promise<string> {
  const { data } = await apiClient.post<{ data: string }>('/admin/forms', form);
  return data.data;
}

/**
 * Admin: Update an existing custom form
 */
export async function updateAdminForm(
  id: string,
  form: {
    title: string;
    description: string;
    slug: string;
    isActive: boolean;
    fieldsJson: string;
  }
): Promise<void> {
  await apiClient.put(`/admin/forms/${id}`, form);
}

/**
 * Admin: Delete a custom form
 */
export async function deleteAdminForm(id: string): Promise<void> {
  await apiClient.delete(`/admin/forms/${id}`);
}

/**
 * Admin: Get submissions for a form
 */
export async function getFormSubmissions(id: string): Promise<FormSubmissionDto[]> {
  const { data } = await apiClient.get<{ data: FormSubmissionDto[] }>(`/admin/forms/${id}/submissions`);
  return data.data;
}

/**
 * Admin: Update submission status and review notes
 */
export async function updateSubmissionStatus(
  submissionId: string,
  status: FormSubmissionStatus,
  adminNotes?: string
): Promise<void> {
  await apiClient.put(`/admin/forms/submissions/${submissionId}/status`, {
    status,
    adminNotes,
  });
}

/**
 * Public: Get active form by slug
 */
export async function getPublicForm(slug: string): Promise<PublicFormDto> {
  const { data } = await apiClient.get<{ data: PublicFormDto }>(`/public/forms/${slug}`);
  return data.data;
}

/**
 * Public: Submit form responses
 */
export async function submitPublicForm(slug: string, answers: Record<string, string>): Promise<void> {
  await apiClient.post(`/public/forms/${slug}/submit`, answers);
}
