# Database Query Plan Performance Audit (EXPLAIN ANALYZE)

This document records a synthetic `EXPLAIN ANALYZE` smoke check for selected read-query shapes.

> **Validation limit:** the captured plans use an all-zero UUID and return `rows=0`. They verify that PostgreSQL can select the named indexes on the current schema, but they do not establish production latency, selectivity, sort cost, or behavior with realistic data volume. A populated staging/production-like run is still required.

---

## 1. Lesson Details Query
* **Use Case**: Loading a single lesson's core metadata, title, and videos in `GetLessonDetailQuery`.
* **SQL Query**:
  ```sql
  EXPLAIN ANALYZE SELECT * FROM lessons WHERE "Id" = '00000000-0000-0000-0000-000000000000';
  ```
* **Execution Plan**:
  ```text
                                                         QUERY PLAN                                                       
  ------------------------------------------------------------------------------------------------------------------------
   Index Scan using "PK_lessons" on lessons  (cost=0.14..8.16 rows=1 width=550) (actual time=0.012..0.012 rows=0 loops=1)
     Index Cond: ("Id" = '00000000-0000-0000-0000-000000000000'::uuid)
   Planning Time: 0.566 ms
   Execution Time: 0.051 ms
  ```
* **Analysis**: The empty-result lookup uses the primary key index (`PK_lessons`). The recorded **0.051 ms** applies only to this zero-row smoke query.

---

## 2. Lesson Comments Query
* **Use Case**: Pagination of lesson comments on component mount/scroll.
* **SQL Query**:
  ```sql
  EXPLAIN ANALYZE SELECT * FROM lesson_comments 
  WHERE "LessonId" = '00000000-0000-0000-0000-000000000000' 
  ORDER BY "CreatedAt" DESC LIMIT 10;
  ```
* **Execution Plan**:
  ```text
                                                                           QUERY PLAN                                                                          
  -------------------------------------------------------------------------------------------------------------------------------------------------------------
   Limit  (cost=8.17..8.18 rows=1 width=608) (actual time=0.017..0.017 rows=0 loops=1)
     ->  Sort  (cost=8.17..8.18 rows=1 width=608) (actual time=0.016..0.017 rows=0 loops=1)
           Sort Key: "CreatedAt" DESC
           Sort Method: quicksort  Memory: 25kB
           ->  Index Scan using "IX_lesson_comments_LessonId" on lesson_comments  (cost=0.14..8.16 rows=1 width=608) (actual time=0.003..0.003 rows=0 loops=1)
                 Index Cond: ("LessonId" = '00000000-0000-0000-0000-000000000000'::uuid)
   Planning Time: 0.545 ms
   Execution Time: 0.032 ms
  ```
* **Analysis**: PostgreSQL selects `IX_lesson_comments_LessonId`, then sorts an empty result. This does not measure the cost of sorting a populated lesson discussion.

---

## 3. Community Feed Query
* **Use Case**: Fetching the latest approved community posts for the student feed.
* **SQL Query**:
  ```sql
  EXPLAIN ANALYZE SELECT * FROM community_posts 
  WHERE "Status" = 1 
  ORDER BY "CreatedAt" DESC LIMIT 10;
  ```
* **Execution Plan**:
  ```text
                                                                          QUERY PLAN                                                                         
  -----------------------------------------------------------------------------------------------------------------------------------------------------------
   Limit  (cost=8.17..8.18 rows=1 width=593) (actual time=0.014..0.014 rows=0 loops=1)
     ->  Sort  (cost=8.17..8.18 rows=1 width=593) (actual time=0.013..0.013 rows=0 loops=1)
           Sort Key: "CreatedAt" DESC
           Sort Method: quicksort  Memory: 25kB
           ->  Index Scan using "IX_community_posts_Status" on community_posts  (cost=0.14..8.16 rows=1 width=593) (actual time=0.002..0.002 rows=0 loops=1)
                 Index Cond: ("Status" = 1)
   Planning Time: 0.447 ms
   Execution Time: 0.028 ms
  ```
* **Analysis**: PostgreSQL selects `IX_community_posts_Status`, then sorts an empty result. A populated feed is needed to evaluate whether a composite `Status, CreatedAt` index is useful.

---

## 4. Audit Log Query
* **Use Case**: Retrieving recent actions performed by an administrative user or staff.
* **SQL Query**:
  ```sql
  EXPLAIN ANALYZE SELECT * FROM audit_logs 
  WHERE "PerformedByUserId" = '00000000-0000-0000-0000-000000000000' 
  ORDER BY "CreatedAt" DESC LIMIT 10;
  ```
* **Execution Plan**:
  ```text
                                                                           QUERY PLAN                                                                         
  ------------------------------------------------------------------------------------------------------------------------------------------------------------
   Limit  (cost=8.17..8.18 rows=1 width=818) (actual time=0.027..0.027 rows=0 loops=1)
     ->  Sort  (cost=8.17..8.18 rows=1 width=818) (actual time=0.026..0.026 rows=0 loops=1)
           Sort Key: "CreatedAt" DESC
           Sort Method: quicksort  Memory: 25kB
           ->  Index Scan using "IX_audit_logs_PerformedByUserId" on audit_logs  (cost=0.14..8.16 rows=1 width=818) (actual time=0.004..0.004 rows=0 loops=1)
                 Index Cond: ("PerformedByUserId" = '00000000-0000-0000-0000-000000000000'::uuid)
   Planning Time: 0.994 ms
   Execution Time: 0.052 ms
  ```
* **Analysis**: The empty-result query selects `IX_audit_logs_PerformedByUserId`. It does not measure a user with a large audit history.

---

## 5. Other Relational Indexes Summary
The schema contains the following relevant B-Tree indexes. Their effectiveness still needs validation with representative rows:
* **Packages**: Scanned via `IX_packages_TeacherId` when loaded by teacher scope, or `IX_packages_SubjectId`.
* **Notifications**: Badge updates query the `notification_events` table using `IX_notification_events_UserId`.
* **Attendance Logs**: Queried via composite index scanning on `IX_attendance_logs_EmployeeId` and `IX_attendance_logs_Date`.

---

## Conclusion
The smoke queries confirm that the listed indexes exist and are selectable. They do **not** prove that all major relational queries are optimal or that no additional indexes are required. Completion requires rerunning the actual generated SQL against representative data and recording row counts, buffers, execution times, and any sequential scans or expensive sorts.
