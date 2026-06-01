# Data Model: Video Carousel Navigation

## Entities

### `Video` (Existing Domain Entity - Frontend DTO)

The frontend currently receives a list of videos within a lesson. The `FeatureCarousel` will need to map these fields to its expected props.

**Fields (Frontend perspective):**
- `id` (String): Unique identifier. Used to sync the player with the selected carousel step.
- `title` (String): Displayed as the feature title in the carousel step.
- `description` (String, Optional): Descriptive text for the carousel step payload.
- `videoUrl` (String): The URL to the video stream (handled externally by the player).
- `thumbnailUrl` (String, Optional): Mapped to the `{image: src}` prop of the feature carousel step. If null, a default image must be supplied.
- `order` (Integer): The sorting index for the video list. Determines the order of steps in the carousel.

## Relationships

- A **Lesson** `hasMany` **Videos**.
- The `FeatureCarousel` maps a `Lesson.videos` array to an array of Step/Feature configurations where:
    - Step index maps to Video `order`.
    - Carousel `currentStep` maps to `activeVideoIndex`.
