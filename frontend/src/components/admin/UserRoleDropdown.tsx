'use client';

import { useState } from 'react';
import { adminService, AdminUserListDto } from '@/services/admin-service';
import toast from 'react-hot-toast';
import { Checkbox, Label } from '@/components/ui/checkbox';

interface UserRoleDropdownProps {
  user: AdminUserListDto;
  onUpdate: (userId: string, roles: string[]) => void;
}

const AVAILABLE_ROLES = ['Admin', 'Teacher', 'Assistant', 'Student', 'AssistantReviewer', 'AssistantAcademic'];

export function UserRoleDropdown({ user, onUpdate }: UserRoleDropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [currentRoles, setCurrentRoles] = useState(user.roles || []);

  const toggleRole = async (roleName: string) => {
    setLoading(true);
    try {
      let newRoles = [...currentRoles];
      if (newRoles.includes(roleName)) {
        newRoles = newRoles.filter(r => r !== roleName);
      } else {
        newRoles.push(roleName);
      }
      
      await adminService.updateUserRoles(user.id, newRoles);
      setCurrentRoles(newRoles);
      onUpdate(user.id, newRoles);
    } catch {
      toast.error('فشل في تحديث الصلاحية');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="relative inline-block text-left">
      <div>
        <button
          type="button"
          onClick={() => setIsOpen(!isOpen)}
          className="inline-flex justify-center w-full rounded-2xl border border-[var(--admin-border)] shadow-sm px-4 py-2 bg-[var(--admin-card)] text-sm font-medium text-[var(--admin-text)] hover:bg-[var(--admin-card-soft)] focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)] transition"
        >
          {currentRoles.length > 0 ? currentRoles.join(', ') : 'No Roles'}
          <svg className="-mr-1 ml-2 h-5 w-5" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
            <path fillRule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
        </button>
      </div>

      {isOpen && (
        <div className="origin-top-right absolute right-0 mt-2 w-56 rounded-2xl shadow-[0_12px_32px_var(--admin-shadow)] bg-[var(--admin-card)] border border-[var(--admin-border)] z-10">
          <div className="py-1" role="menu" aria-orientation="vertical">
            {AVAILABLE_ROLES.map(role => (
              <Checkbox
                key={role}
                id={`role-${role}`}
                isDisabled={loading}
                isSelected={currentRoles.includes(role)}
                onChange={() => toggleRole(role)}
                className="w-full justify-start px-4 py-2 hover:bg-[var(--admin-card-soft)] transition"
              >
                <Checkbox.Control>
                  <Checkbox.Indicator />
                </Checkbox.Control>
                <Checkbox.Content>
                  <Label className="text-sm font-medium text-[var(--admin-text)] cursor-pointer">{role}</Label>
                </Checkbox.Content>
              </Checkbox>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
