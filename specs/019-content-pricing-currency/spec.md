# Feature Specification: Content Pricing and Currency Update

**Feature Branch**: `019-content-pricing-currency`  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "عايز اضيف اني اضيف السعر فوانا بضيف كل حاجه وعايز كمان تغير العمله تبقي جنيها"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Granular Content Pricing (Priority: P1)

As an administrator, I want to be able to set a specific price for Terms, Sections, and Lessons when creating or updating them, so that I have the flexibility to sell individual sub-sections of a package rather than just the package as a whole.

**Why this priority**: Granular pricing directly impacts the business model, allowing micro-transactions and targeted sales of specific academic content (e.g., selling a single revision lesson instead of an entire term).

**Independent Test**: Can be tested by navigating to any nested content level (Term, Section, or Lesson), filling out the creation form including a price, and verifying that the item is saved and displayed with the correct price.

**Acceptance Scenarios**:

1. **Given** an administrator is on the "Add Term" modal/form, **When** they enter a valid numeric price and submit, **Then** the term should be created with that price.
2. **Given** an administrator views the list of Sections, **When** the sections are displayed, **Then** the associated price for each section should be clearly visible.
3. **Given** an administrator is creating a Lesson, **When** they intentionally leave the price field empty, **Then** the system should gracefully default the price to 0 (free).

---

### User Story 2 - Localized Currency Display (Priority: P2)

As a system user (student or admin), I want to see all monetary values represented in Egyptian Pounds ("جنيها") rather than Kuwaiti Dinars ("دك"), so that the pricing reflects the localized target market correctly.

**Why this priority**: Correct currency representation prevents customer confusion and ensures trust in the platform's billing and wallet systems.

**Independent Test**: Can be verified by auditing the wallet balance display, package details, purchase modals, and admin listings to ensure "جنيها" is the unified currency string.

**Acceptance Scenarios**:

1. **Given** a student views their wallet balance, **When** the balance is rendered, **Then** it should end with "جنيها".
2. **Given** an administrator views the Package list, **When** the package price is displayed, **Then** it should read "X جنيها" instead of "X دك".

### Edge Cases

- What happens when an administrator inputs a negative price? (System should prevent negative inputs via form validation).
- How does the system handle pricing if a parent (Term) is purchased but the child (Lesson) has its own price? (This spec only covers *setting* the price, not the complex access resolution model which may already rely on the wallet).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST persist a numerical `Price` attribute for Terms, Content Sections, and Lessons.
- **FR-002**: The system MUST expose input fields for `Price` on the creation and update forms for Terms, Sections, and Lessons in the administrative dashboard.
- **FR-003**: The system MUST default the price to `0` (Free) if an administrator does not specify a price.
- **FR-004**: The system MUST prevent entering negative price values.
- **FR-005**: The system MUST globally replace all hardcoded instances of the Kuwaiti Dinar abbreviation ("دك") with the Egyptian Pound abbreviation ("جنيها") across both the student and admin modules.

### Key Entities

- **Term**: Represents an academic term. Requires a new `Price` attribute.
- **ContentSection**: Represents a unit/chapter within a term. Requires a new `Price` attribute.
- **Lesson**: Represents an individual lesson within a section. Requires a new `Price` attribute.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Administrators can fully configure the price of any nested content item in under 30 seconds.
- **SC-002**: 100% of all user-facing and admin-facing monetary displays utilize "جنيها" instead of any legacy currency symbols.
- **SC-003**: The database correctly persists and retrieves decimal prices for all 3 levels of the content hierarchy without data truncation.
