import 'models.dart';

/// Reception records homework exceptions; attendance supplies the default.
HomeworkStatus effectiveHomeworkStatus(
  HomeworkStatus? recordedStatus, {
  required bool attended,
}) {
  if (recordedStatus != null && recordedStatus != HomeworkStatus.notReviewed) {
    return recordedStatus;
  }
  return attended ? HomeworkStatus.complete : HomeworkStatus.notReviewed;
}
