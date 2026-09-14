# Shadow posting

`LiveFinancialProjectionCoordinator` and the source adapters support shadow-safe coordination through `PlatformFinanceOptions`. Shadow mode must be enabled against a production read replica first; each mismatch is recorded by source type and checkpoint before mutation mode is enabled.
