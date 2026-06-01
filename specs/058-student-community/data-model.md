# Data Model: Student Community

## Entities

### `CommunityPost`
Represents a student-authored post submitted to the community feed and subject to moderation before public visibility.

**Fields**:
- `Id` (Guid, PK)
- `AuthorUserId` (Guid, FK to `User`)
- `Body` (string, required, bounded length)
- `Status` (Enum/Int): `Pending = 0`, `Approved = 1`, `Rejected = 2`
- `CreatedAt` (DateTime)
- `UpdatedAt` (DateTime, nullable if edits are not supported in v1)
- `ReviewedAt` (DateTime, nullable)
- `ReviewedByUserId` (Guid, FK to `User`, nullable)

**Relationships**:
- Belongs to one author `User`
- Optionally references one reviewer `User`
- Has many `CommunityPostComment`
- Has many `CommunityPostLike`

### `CommunityPostComment`
Represents a student's flat discussion reply on an approved community post.

**Fields**:
- `Id` (Guid, PK)
- `PostId` (Guid, FK to `CommunityPost`)
- `AuthorUserId` (Guid, FK to `User`)
- `Body` (string, required, bounded length)
- `CreatedAt` (DateTime)

**Relationships**:
- Belongs to one `CommunityPost`
- Belongs to one author `User`

### `CommunityPostLike`
Represents a unique like from a student to an approved community post.

**Fields**:
- `Id` (Guid, PK) or composite uniqueness across `PostId` + `UserId`
- `PostId` (Guid, FK to `CommunityPost`)
- `UserId` (Guid, FK to `User`)
- `CreatedAt` (DateTime)

**Relationships**:
- Belongs to one `CommunityPost`
- Belongs to one `User`

### `CommunityPostModerationView`
Read-model concept used by admins to review pending and resolved posts with enough context to make moderation decisions.

**Fields**:
- `PostId`
- `StudentId`
- `StudentName`
- `Body`
- `Status`
- `CreatedAt`
- `ReviewedAt`
- `ReviewedByName`
- `CommentCount`
- `LikeCount`

**Relationships**:
- Derived from `CommunityPost`, `User`, `CommunityPostComment`, and `CommunityPostLike`

## State Transitions

- **Pending -> Approved**: Post becomes visible in the public community feed and becomes eligible for likes/comments.
- **Pending -> Rejected**: Post remains hidden from the public feed and cannot receive engagement.
- **Approved -> Approved**: Idempotent repeat approval should not create duplicate side effects.
- **Rejected -> Rejected**: Idempotent repeat rejection should not create duplicate side effects.
- **Approved -> Removed from feed later**: Not part of v1 workflow, but the model should preserve enough metadata to support future administrative removal rules.

## Validation Rules

- A post cannot be created unless the user is authenticated and allowed to access the student community.
- `CommunityPost.Body` must contain non-whitespace text and respect a bounded maximum length suitable for feed posts.
- Only `Pending` posts can be newly approved or rejected in v1.
- Public feed queries must return only `Approved` posts.
- Likes can be created only for `Approved` posts.
- The same user cannot hold more than one active like on the same post at the same time.
- Comments can be created only for `Approved` posts.
- `CommunityPostComment.Body` must contain non-whitespace text and respect a bounded maximum length suitable for discussion replies.
- Feed ordering should prioritize newest approved posts first; comment ordering should be chronological within a post.

## DTOs

### `CommunityPostFeedDto`
- `Id`
- `AuthorName`
- `Body`
- `CreatedAt`
- `LikeCount`
- `CommentCount`
- `IsLikedByCurrentUser`

Used for the student-facing feed of approved posts.

### `CreateCommunityPostRequest`
- `Body`

### `CreateCommunityPostResponse`
- `Id`
- `Status`
- `CreatedAt`
- `Message`

### `CommunityPostCommentDto`
- `Id`
- `PostId`
- `AuthorName`
- `Body`
- `CreatedAt`
- `IsOwnComment`

### `CreateCommunityCommentRequest`
- `Body`

### `ToggleCommunityPostLikeResponse`
- `PostId`
- `IsLikedByCurrentUser`
- `LikeCount`

### `ModerationCommunityPostDto`
- `Id`
- `StudentId`
- `StudentName`
- `Body`
- `Status`
- `CreatedAt`
- `ReviewedAt`
- `ReviewedByName`
- `CommentCount`
- `LikeCount`

### `ModerateCommunityPostResponse`
- `Id`
- `Status`
- `ReviewedAt`
- `ReviewedByUserId`
