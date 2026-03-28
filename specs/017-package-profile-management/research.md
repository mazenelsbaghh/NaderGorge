# Research & Decisions: Package Profile Management

## Overview
This document outlines the technical decisions and design choices for creating the Package Profile and Term Management feature, resolving any ambiguities from the initial specification.

## Component Strategy: Unified Settings via Shared Components
**Decision**: Use existing `AdminStatCard`, `AdminShellChrome`, and standard Tailwind Form components augmented with `backdrop-blur` and glassmorphism.
**Rationale**: The user explicitly requested "واعملهم من شاريد كومبنت" (make them from shared components). The Constitution (Section VIII) mandates the "Editorial Scholar" design system. Reusing these components ensures compliance with the No-Line rule and Glass & Gradient aesthetics.
**Alternatives considered**: Building custom layout specific to the package profile. Rejected due to maintainability issues and user requests.

## Page Layout & Navigation
**Decision**: Implement the package profile at `/admin/content/packages/[id]`. The page will feature tabs or sections for: Overview (Stats), Settings (Edit details), and Content (Terms hierarchy).
**Rationale**: A single unified page matches the user's intent to "دخلني علي صفحتها علشان اضيف الترم جواه" (enter its page to add terms inside it).
**Alternatives considered**: A sliding panel or modal over the main content page. Rejected because a dedicated route allows deep linking and more screen real estate for managing complex hierarchies.

## Term Management Interface
**Decision**: When adding a Term, present an interactive section/form directly on the `Content` tab of the Package Profile rather than redirecting to a separate page.
**Rationale**: Ensures the admin retains context of the package they are editing, satisfying SC-001 (under 3 clicks to start adding a term).

## Backend Integration
**Decision**: The frontend will interact with the existing `PackagesController` and `TermsController` endpoints, specifically utilizing paths structured around the Package-Term relationship.
**Rationale**: Adheres to the exact Academic Content Integrity hierarchy defined in the constitution (Package → Term → Section → Lesson).
