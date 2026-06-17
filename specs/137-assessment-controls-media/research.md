# Research: Assessment Controls And Question Media

## Decision: Reuse existing mandatory fields

**Rationale**: `Homework.IsMandatory` and `Exam.IsMandatory` already exist and are already checked by progression logic. The feature should expose and preserve these settings rather than introducing new flags.

**Alternatives considered**: A separate progression policy table was rejected because the current requirement is binary mandatory/optional per assessment and the domain already supports it.

## Decision: Store question image URL on question entities

**Rationale**: Exam questions use `QuestionBankItem`; homework uses `HomeworkQuestion`. A nullable `ImageUrl` on each entity is the simplest durable model and mirrors existing `AudioUrl`, `HintText`, and image URL conventions.

**Alternatives considered**: Embedding images inside rich text was rejected because it complicates sanitization and makes `<p>`/HTML display problems worse.

## Decision: Use the existing assets-domain image pipeline

**Rationale**: Existing content image uploads use `IContentImageStorage`, convert images to WebP, store under `/uploads/content/...`, and the frontend resolves relative upload URLs to `https://assets.massar-academy.net` in production. Question image upload should use the same path and return relative URLs such as `/uploads/content/questions/{file}.webp`.

**Alternatives considered**: Client-side base64 storage was rejected due to database bloat and performance concerns.

## Decision: Clean rich text through shared display utilities

**Rationale**: Student views need safe, consistent display. A shared frontend helper can sanitize HTML for rich display and provide plain-text conversion where needed.

**Alternatives considered**: Stripping all HTML before saving was rejected because existing questions may rely on basic formatting from the editor.
