# Requirements Checklist: Package Partial Enrollment Display

- [ ] A package shows up in "الباقات المفعّلة" if direct Package access is granted.
- [ ] A package shows up in "الباقات المفعّلة" if any child Term access is granted.
- [ ] A package shows up in "الباقات المفعّلة" if any child Section (Month) access is granted.
- [ ] A package shows up in "الباقات المفعّلة" if any child Lesson access is granted.
- [ ] Backend endpoint `/api/content/packages` returns `isEnrolled: true` in all these cases.
- [ ] Direct database queries inside `GetPackagesQueryHandler` are optimized (no N+1 queries).
