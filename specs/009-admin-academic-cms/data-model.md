# Data Model & Storage: Phase 2.5: Admin CMS for Homework and Assistants

## Overview
This specification adds relatively few new domains or properties since Phase 2 implemented the core models `Homework`, `HomeworkQuestion`, and `HomeworkSubmission`. Phase 2.5 integrates this entirely over the Management REST API via simple UI interaction and expands the Admin's scope over Identity. Nothing new needs migrating on the database side except making sure the Application uses existing columns.

## Existing Schema Utilization
No new Entity Framework models are required. 

- `ApplicationUser`: The existing Identity user table with its associated `IdentityUserRole<Guid>` relationships.
- `Homework`: Contains lesson bindings, instructions, and list of `HomeworkQuestion`s.

No additional migrations are required for this phase. 
