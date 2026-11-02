# Fixed written-answer grading, 2026-11-02

## Behavior

The worker generates the rubric from the question and teacher reference before presenting any student answer. The first complete rubric published for that exact source and policy version is immutable. Production workers share it under `SHARED_STORAGE_ROOT/private/grading-rubrics-v1`; local development uses `worker/.tmp/grading-rubrics-v1` when launched from `worker/`. These private files contain derived marking criteria, not student answers or grades. PostgreSQL remains authoritative for saved grades.

Evaluation returns one judgment per fixed criterion, with a verbatim student-answer excerpt for every matched criterion. The worker rejects missing, duplicate or unknown criteria and invented evidence. It computes the weighted fraction itself; a model-supplied numeric score cannot override the calculation. Two matching equally weighted criteria out of three produce 0.67 consistently. The existing backend policy that awards full credit from 70 percent is unchanged.

Criteria and weights remain fixed across concurrent publications and worker restarts. Updating the question/reference or policy version selects a new rubric. Invalid saved files and storage errors fail grading rather than silently replacing the rubric. Semantic matching still depends on the AI; fixed arithmetic does not guarantee that every semantic judgment or initially generated rubric is correct.

Existing saved grades and existing queue evaluation checkpoints are preserved. This change does not regrade historical attempts or publish a production release. Essay checkpoints retain the rubric fingerprint and judgments; homework checkpoints retain their calculated score and feedback. Both callbacks retain their existing score/feedback contract.

## Verification

Build scope: awaiting the owner's explicit choice of `worker` or `all`, as required by the production skill. No build or test run for this change has been performed yet.

Prepared checks cover the 67/75 percent regression, fixed explicit weights, malformed judgments and evidence, rounding near zero/full credit, concurrent rubric publication, cache integrity, and callback retries. The opt-in real-provider probe `node dist/scripts/accept-written-rubric.js` uses synthetic answers only and never changes student records. Automated provider stubs establish protocol behavior, not semantic grading accuracy.
