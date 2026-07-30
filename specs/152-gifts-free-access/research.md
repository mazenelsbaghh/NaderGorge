# Research: Gifts and Free Access

## Decision 1: Use a Dedicated Gift Aggregate

**Decision**: Model `GiftIssuance` and `GiftRecipient` separately from `CodeGroup` and `AccessCode`.

**Rationale**: Gifts are selected Admin actions with bulk recipient outcomes, revocation reasons, and no redemption secret. Reusing codes would create fake activation semantics and couple this phase to future coupon/print-code work.

**Alternatives rejected**:
- Extend `AccessCode`: requires meaningless code strings and activation state.
- Create grants directly without a header: cannot provide request idempotency, bulk outcomes, or a coherent ledger.

## Decision 2: Keep Promotional Value Outside Paid Balance

**Decision**: Store each gift as a conserved `PromotionalBalanceAllocation` with append-only `PromotionalBalanceUsage` rows.

**Rationale**: The product must explain source, teacher restriction, expiration, use cap, revocation, and revenue exclusion. A single paid balance number cannot preserve those rules or evidence.

**Alternatives rejected**:
- Credit `StudentBalance`: loses expiry/restriction provenance and risks teacher revenue.
- Store only a computed total: cannot safely consume or audit concurrent purchases.

## Decision 3: Purchase Owns One Serializable Transaction

**Decision**: `PurchaseContentCommand` opens one serializable transaction covering promotional allocations, paid balance, entitlement, usage rows, and event creation.

**Rationale**: Mixed funding is one business operation. Partial commits could consume a gift without granting content or deduct paid value twice. Conditional allocation updates plus transaction rollback preserve conservation under concurrency.

**Alternatives rejected**:
- Separate transactions per funding source: permits partial financial state.
- Process-local locks: do not protect multiple API instances.
- Redis lock as authority: PostgreSQL already owns all durable value and must remain authoritative.

## Decision 4: Resolve Teacher Eligibility from Authoritative Content

**Decision**: Resolve the content teacher from the existing package/content hierarchy at purchase time, never from a client-provided teacher id.

**Rationale**: A stale or forged client value could spend restricted credit on the wrong teacher. Purchase-time resolution also handles legitimate content ownership changes consistently.

## Decision 5: Target-Aware Use Counting

**Decision**: Count successful new video sessions, fresh exam attempts, and funded purchases. Package and lesson gifts use expiration only.

**Rationale**: This matches the approved clarification and avoids charging failed playback starts or reopening an existing exam attempt.

**Alternatives rejected**:
- Count page opens: unreliable and easy to consume accidentally.
- Count completed videos/exams only: allows unlimited starts despite an issuance cap.

## Decision 6: Add Video-Specific Access

**Decision**: Add `HasAccessToVideoAsync` and a partial lesson projection that exposes only directly granted videos when lesson access is absent.

**Rationale**: Existing session creation checks lesson access, while `StudentAccessGrant` already supports `LessonVideoId`. Without this change a video gift is recorded but unusable; granting the lesson would violate sibling isolation.

## Decision 7: Lazy Expiration is the Correctness Mechanism

**Decision**: Expire promotional allocations atomically when balance, purchase, ledger detail, or revocation paths touch them. Access grants rely on `ExpiresAt` checks.

**Rationale**: Correct behavior does not depend on a background scheduler. A later maintenance job may improve reporting freshness but is not required for this phase.

## Decision 8: Gift-Specific Lookup APIs

**Decision**: Put student, teacher, and target search under `/api/admin/gifts/lookups/**` and protect them with `gifts.manage`.

**Rationale**: Delegated gift staff must complete the workflow without receiving broad `users.manage` or `content.manage` permissions. Responses expose only fields needed for selection.

## Decision 9: Preserve Evidence During Revocation

**Decision**: Revocation disables future access and moves only available promotional value to revoked value. It never deletes sessions, attempts, usages, or prior entitlement evidence.

**Rationale**: This satisfies “remove unused remainder” while keeping academic and financial history explainable.

## Decision 10: Keep Revenue Exclusion Explicit

**Decision**: Carry `PromotionalAmount` and `PaidAmount` through purchase result/event data and ensure only paid value can reach existing revenue logic.

**Rationale**: Inferring gift funding later from grants is fragile. Explicit split values make tests, audit, and future accounting integration safe.

## Decision 11: No Worker or External Messaging

**Decision**: Implement synchronously in API/Application/PostgreSQL and use only existing in-app behavior where already present.

**Rationale**: Issuance is capped at 100 recipients and the approved scope says gift state cannot depend on SMS/WhatsApp/push delivery.
