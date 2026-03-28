# Research & Discovered Best Practices

## Findings

### Feature: Pricing for Nested Content Hierarchy
- **Decision**: Introduce a `public decimal Price { get; set; }` nullable or required property on `Term`, `ContentSection`, and `Lesson` entities.
- **Rationale**: Direct column mapping provides O(1) reads for nested entity pricing directly from their own tables without requiring polymorphic configurations or external joins. Using decimal strictly avoids precision-loss errors associated with float/double for financial amounts.
- **Alternatives considered**: Having a centralized `ContentPricing` table mapped by an EntityType enum and EntityId. Rejected because it introduces unnecessary join complexity for a simple price display use case.

### Feature: Global Currency Update (EGP)
- **Decision**: Update all frontend hard-coded values from 'دك' (Kuwaiti Dinar abbreviation) to 'جنيها' (Egyptian Pound).
- **Rationale**: The project has historically launched with Kuwaiti Dinar support but is localizing rendering for the target Egyptian audience. Hardcoding it directly in the React views is the fastest path since full multi-currency support is not within the current spec bounds.
- **Alternatives considered**: Introducing a localization (i18n) framework. Rejected due to over-engineering for a single-language platform MVP.

## Next Steps

- Proceed with Entity additions.
- Adjust Command validators and constructors.
- Map DTOs.
- React components update.
