# Research & Decision Log: Media Production and Social Planner

## Summary of Decisions

1. **Pipeline Stages & State Machine**:
   - Stages will be mapped as a C# enum: `MediaStage` (`Preparation`, `Filming`, `Editing`, `Uploading`, `Review`, `Approved`, `Published`).
   - The transitions will follow a strict forward flow, except for rejection from `Review` which moves it back to `Editing`.
   - Transition to `Published` will be restricted: the item *must* be in `Approved` stage first.

2. **Integration with Approval Pipeline**:
   - Instead of creating a duplicate approval system, we will reuse the `TaskItem` and task approval mechanics from Phase 3.
   - We will add a nullable foreign key `MediaPipelineId` (Guid?) to the `TaskItem` entity.
   - When a media item transitions to `Review`, the system will automatically create a `TaskItem` with:
     - Title: `مراجعة محتوى: [Media Title]`
     - Status: `TaskStatus.Review`
     - AssigneeId: The supervisor selected during the transition.
     - MediaPipelineId: The ID of the media pipeline item.
   - When a manager approves the task via `AdminResolveApprovalCommand`, the handler will check if `MediaPipelineId` is set and automatically update the linked media item's stage to `Approved`.
   - If rejected, the handler will update the media item's stage back to `Editing` and append a comment explaining the rejection.

3. **Social Media Planner Integration**:
   - The `SocialMediaPlan` entity will contain details about posts (title, copy, platform, scheduled date).
   - A nullable foreign key `MediaProductionPipelineId` (Guid?) will link it to a pipeline item.
   - This allows the social planner dashboard to show whether the media asset for a scheduled post has been filmed, edited, approved, or published.

## Rationale for Decisions

- **Reusing TaskItem**: Reusing the existing task infrastructure avoids creating redundant approval tables, endpoint duplications, and UI comment feeds. The existing `TaskItem` already has assignee mappings, comments, attachments, and manager-approval logic. Adding `MediaPipelineId` allows the media approvals to blend seamlessly into the operations dashboard.
- **Strict Transition Rules**: Preventing direct transitions to `Published` guarantees that no raw, unedited, or unapproved videos are posted on the student platform, securing content quality.
- **Stateless Social Planner**: Direct integrations with social APIs (Meta Graph API, YouTube upload API) require tokens, complex scopes, and app approvals. Keeping the planner as an internal operational scheduler avoids these external dependencies while providing 100% of the required coordination value.

## Alternatives Considered

- **Alternative 1: Separate MediaApproval Table**: Rejected because it requires duplicating the comment threads, attachments, and roles checks already implemented in the operations tasks module.
- **Alternative 2: Automatic API Publishing**: Rejected because the cost of maintaining Facebook/YouTube API integrations (OAuth tokens expiring, API version changes, upload limits) outweighs the manual coordination benefits for the current team size.
