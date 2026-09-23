# Massar Parent Flutter design system

The owner approved the sixteen individual mobile mockups beginning with the white/mint splash. The native Flutter screens use real controls and live API data; the artwork's example names, scores, dates, status bars and inferred messages are not application data.

`lib/ui/design_system.dart` is the public barrel. Screen code imports this barrel. Change shared appearance in `lib/ui/design_system/`, not in copied screen-specific widgets.

| File | Responsibility |
| --- | --- |
| `tokens.dart` | Navy/teal/mint palette, typography, control dimensions, light/dark themes |
| `brand.dart` | Original SVG logo, with inline SVG fills supported by Flutter |
| `surfaces.dart` | Soft panels, full-screen wave background, scrolling page spacing |
| `actions.dart` | Full-width primary pill button and busy state |
| `typography.dart` | Page titles and section headings |
| `data_display.dart` | Data rows, status pills, empty and retry panels |
| `progress.dart` | Accessible academic progress arc with text scaling |
| `navigation.dart` | Four shared bottom destinations and navy selected pill |

Use `LessonTile` and `AssessmentTile` from `academic_screens.dart` for academic summaries in both overview and lists. Titles and row content wrap. RTL is set at the application boundary. The operating system owns the status bar. Screens scroll rather than scaling down body text; accessibility text scale is preserved.

Academic status has meaning: starting a video is distinct from completing it; unstarted or unreconciled exams have no displayed numeric result; submitted homework awaiting grading does not display a zero grade. Empty lists are described explicitly. Warnings display the backend reason rather than generating a conclusion about the student.

The canonical component screenshot check is in `test/widget_test.dart`; it also covers 360px layouts with 1.6 text scaling. Screenshots are emitted locally to ignored `test-output/` for inspection.

## Reference alignment and motion

All routes share `MassarBackdrop`: a mint radial wash, translucent vector waves and stable fine grain. `ContextBanner` clips the same background into the outlined academic header. The progress arc and rings use a sweep gradient along the completed segment, with an endpoint marker on the arc. `patterns.dart` owns the student banner, icon badges, pill tabs, paired metrics, question-answer panels and branded detail scaffold. `motion.dart` centralizes staggered entrances, navigation fades/slides and reduced-motion durations; primary actions provide press feedback, link steps and main tabs transition, and progress animates to new values. No delay is added to network completion.

The onboarding artwork is a transparent ImageGen derivative of the approved books/lesson-panel reference. All titles, metrics and actions remain Flutter widgets. Font scaling and OS safe areas remain native; example values in the mockups are never copied into live responses.

Academic views require an explicit teacher selection. Teacher options come only from the active student's API `courses` (the backend derives these from active access grants), including enrolled teachers with no rows in the selected assessment type. There is no global teacher catalogue or “all teachers” bypass in these views. Rows are matched to the selected teacher through the enrolled course ID. Changing teacher resets dependent course/term filters; switching exam/homework preserves the selected teacher.

`test/screen_gallery_test.dart` produces sixteen separate screenshots of the actual widgets using labelled fixture data. The gallery supplements the migration, identity-switching, academic-state, enrollment-filter and reduced-motion tests. It is not a pixel-diff assertion against the generated artwork. Hardware frame pacing, physical push delivery and signed native-upgrade compatibility still require devices.
