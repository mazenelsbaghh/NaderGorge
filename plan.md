1) System Overview

1.1 Product Summary

The platform is a web-first educational system for Nader George focused on:
	•	First Secondary
	•	Second Secondary
	•	First Baccalaureate
	•	Second Baccalaureate

It is not only a course-selling website.
It is a full academic control platform that combines:
	•	recorded lessons
	•	structured lesson bundles
	•	exams
	•	homework
	•	student progress tracking
	•	gamification
	•	parent reporting
	•	assistant workflows
	•	code-based content access
	•	future AI-assisted academic workflows

The platform should be built as a brand-specific system for Nader George, while keeping the architecture expandable and modular.

⸻

1.2 Core Product Vision

The platform should help students with:
	•	understanding history more easily
	•	memorizing faster
	•	solving more exam questions
	•	staying committed
	•	preparing for exams with better structure
	•	improving grades through follow-up and controlled progression

The platform should feel:
	•	simple
	•	organized
	•	motivating
	•	clear in its study path

⸻

1.3 Academic Flow

The platform’s learning flow is based on:
	•	Exam
	•	Lesson
	•	Homework

A lesson may contain:
	•	multiple videos
	•	written summary
	•	short questions
	•	homework
	•	short quiz
	•	downloadable file
	•	mind map
	•	revision content

The content structure should support:
	•	package
	•	internal bundle sections called “months” as content groups, not calendar months
	•	lessons
	•	video segments
	•	exams
	•	homework

⸻

2) Technical Architecture Overview

2.1 Frontend Stack

The frontend should be built using:
	•	Next.js as the React framework
	•	TypeScript
	•	Tailwind CSS
	•	React Query / TanStack Query
	•	Zustand or equivalent for local app state where needed
	•	Shadcn/UI or a clean custom component system
	•	Framer Motion for lightweight animations where useful

Why this stack

Because it gives:
	•	fast web delivery
	•	strong dashboard experience
	•	reusable component architecture
	•	excellent developer speed
	•	good SSR/CSR balance
	•	easy future migration into app-like UX

⸻

2.2 Backend Stack

The backend should be built using:
	•	.NET Web API
	•	C#
	•	Clean Architecture
	•	CQRS where useful, not blindly everywhere
	•	Entity Framework Core
	•	PostgreSQL

Backend Layers

Recommended backend structure:
	•	API Layer
	•	Application Layer
	•	Domain Layer
	•	Infrastructure Layer

Core backend modules

The backend should be modularized around:
	•	Authentication
	•	Users
	•	Student Profiles
	•	Parent Data
	•	Packages and Content
	•	Lessons and Videos
	•	Exams
	•	Homework
	•	Codes and Code Groups
	•	Tracking and Analytics
	•	Notifications
	•	Gamification
	•	AI Workflows
	•	Audit Logs
	•	Assistant Operations

⸻

2.3 Database

Primary database:
	•	PostgreSQL

Why PostgreSQL

Because it supports:
	•	strong relational modeling
	•	reporting queries
	•	scalable indexing
	•	reliable transactional flows
	•	structured and semi-structured data

Main data domains

The schema should cover:
	•	users
	•	roles
	•	students
	•	parent contacts
	•	academic programs
	•	packages
	•	lessons
	•	lesson videos
	•	exams
	•	questions
	•	answers
	•	homework
	•	code groups
	•	activation logs
	•	watch tracking
	•	notifications
	•	leaderboard data
	•	audit logs
	•	AI analysis outputs

⸻

2.4 Cache and Real-Time Speed Layer

Use:
	•	Redis

Redis should be used for:
	•	caching repeated reads
	•	rate limiting
	•	session/device activity support
	•	OTP temporary storage
	•	leaderboard fast reads
	•	notification queue buffering
	•	watch-session temporary state
	•	anti-abuse logic

⸻

2.5 Background Jobs / Queues

Use:
	•	BullMQ with Redis

Important note:
BullMQ is a Node-based queue system, so it does not run inside .NET directly.
That means the system should use:
	•	.NET as the main backend
	•	a small Node worker service for BullMQ jobs

Why use BullMQ here

Because the platform will need background processing for:
	•	sending SMS
	•	sending notifications
	•	generating reports
	•	recalculating leaderboard
	•	evaluating delayed academic events
	•	post-processing watch logs
	•	AI job orchestration
	•	scheduled activation tasks
	•	package expiration checks
	•	reminder generation

Recommended architecture for jobs

Use:
	•	.NET API writes jobs/events
	•	Redis acts as shared broker/store
	•	Node Worker using BullMQ processes background jobs

This is a clean and practical hybrid model.

⸻

2.6 File and Media Strategy

Initial video strategy

Use:
	•	YouTube embedded videos in Phase 1

Future video strategy

Prepare abstraction for migration to:
	•	Bunny.net Stream
	•	Vimeo
	•	private media layer later

Important

Do not hard-code the system around YouTube URLs only.
Create a Video Provider Abstraction Layer from the beginning.

Each lesson video record should support:
	•	provider type
	•	external video id
	•	title
	•	duration
	•	order
	•	watch limits
	•	replay limits
	•	provider metadata

⸻

2.7 Notifications and Communication

The system should support:
	•	in-platform notifications
	•	SMS for selected critical events
	•	future WhatsApp / Zoho integration

Notification examples
	•	code activated
	•	package expiring
	•	inactivity warning
	•	missed homework
	•	weak exam result
	•	parent alert
	•	assistant follow-up task

⸻

2.8 Security and Protection

The platform should include:
	•	JWT-based auth
	•	refresh token flow
	•	role-based authorization
	•	audit logs
	•	IP tracking
	•	device tracking
	•	session control
	•	optional forced logout
	•	watch restrictions
	•	dynamic watermark overlay on videos later
	•	rate limiting on code redemption and auth flows

⸻

3) Core System Modules

3.1 Public Website

Used for:
	•	branding
	•	teacher identity
	•	landing pages
	•	package explanation
	•	FAQ
	•	code instructions
	•	login/registration entry
	•	conversion pages

⸻

3.2 Student Portal

Used for:
	•	dashboard
	•	lesson access
	•	exam solving
	•	homework
	•	progress tracking
	•	ranking
	•	notifications
	•	plan visibility
	•	AI study assistance later

⸻

3.3 Parent Layer

Used for:
	•	student progress visibility
	•	warning summaries
	•	grade summaries
	•	homework and commitment tracking

This can start as reports and later become a dedicated dashboard.

⸻

3.4 Teacher Panel

Used by Nader George for:
	•	content oversight
	•	academic monitoring
	•	student analytics
	•	performance review
	•	package planning
	•	exam visibility

⸻

3.5 Assistant Panel

Used by assistants with role-based access for:
	•	homework review
	•	student follow-up
	•	support actions
	•	assigned task handling
	•	performance tracking

⸻

3.6 Admin Panel

Used for:
	•	managing users
	•	managing packages
	•	managing lessons
	•	managing code groups and codes
	•	tracking activations
	•	managing questions and exams
	•	monitoring analytics
	•	controlling settings
	•	reviewing logs
	•	controlling assistant permissions

⸻

3.7 AI Services Layer

Used later for:
	•	question generation
	•	simple performance analysis
	•	essay correction assistance
	•	weakness detection
	•	study recommendation
	•	controlled lesson assistant

⸻

4) Business Rules Summary

4.1 Access Model

Access is primarily granted through codes.

Code types
	•	year code
	•	term code
	•	month code
	•	lesson code
	•	video code (one or more specific videos)
	•	exam code
	•	balance/credit code
	•	promotional code later
	•	referral code later

Code behavior
	•	single-use codes
	•	code groups/batches
	•	QR code purchase support
	•	direct purchase support
	•	content-based and/or duration-based logic
	•	activation confirmation required
	•	some codes can start on a selected date
	•	some codes can expire before usage
	•	stacking rules depend on code type
	•	discount management
	•	admin can modify any code at any time

⸻

4.2 Package Logic

A package follows a hierarchical content structure.

Hierarchy:
	•	Package
	•	Year (a package contains a year)
	•	Term (a year contains any number of terms)
	•	Content Section / Month (a term contains content groups — these are content bundles, not calendar months)
	•	Lesson (each section contains lessons)
	•	Lesson contains: videos, quiz, homework, and resources

⸻

4.3 Watch Control

Each video can have:
	•	max allowed watch minutes
	•	max replay count
	•	allowed playback speeds
	•	partial skip policy
	•	completion threshold

Admin or authorized assistant can increase limits if needed.

⸻

4.4 Exam Logic

Exams are essential, not optional.

The system should support:
	•	MCQ
	•	Essay
	•	mixed exams
	•	question bank
	•	AI-assisted classification later
	•	exam availability rules
	•	pass threshold rules
	•	instant grading where possible
	•	essay comparison against a model answer

⸻

4.5 Homework Logic

Homework can be:
	•	MCQ
	•	essay
	•	mixed

Homework may be:
	•	required
	•	due-date based
	•	assistant-reviewed
	•	AI-assisted later

⸻

4.6 Student Behavior Logic

The system should track:
	•	lesson completion
	•	watch time
	•	missed homework
	•	exam performance
	•	inactivity
	•	commitment level

Student classification can include:
	•	committed
	•	average
	•	at risk

⸻

4.7 Gamification Logic

The platform should support:
	•	points
	•	badges
	•	levels
	•	ranking
	•	challenges

This should be motivating, not toxic.

⸻

5) Full Phased Plan

⸻

Phase 0 — Discovery, Planning, and Product Blueprint

Goal

Define the entire product clearly before building.

Objectives
	•	lock the business model
	•	lock the academic structure
	•	lock the access/code system
	•	lock the user roles
	•	lock the initial UX philosophy
	•	lock the technical architecture

Scope

0.1 Product Definition

Define the platform as a full academic system, not just a content site.

0.2 Target Audience Definition

Confirm all user segments:
	•	First Secondary
	•	Second Secondary
	•	First Baccalaureate
	•	Second Baccalaureate
	•	parents
	•	assistants
	•	admin

0.3 Teacher Brand Definition

Document the teacher style:
	•	youthful and simple
	•	energetic
	•	story-based explanation
	•	maps and tables
	•	motivating teaching style

0.4 Content Blueprint

Define the full content structure:
	•	package
	•	year (contains any number of terms)
	•	term
	•	internal content sections (months)
	•	lessons
	•	multiple videos
	•	summary
	•	short questions
	•	homework
	•	short quiz
	•	downloadable files
	•	mind maps
	•	revision assets

0.5 Access Blueprint

Define:
	•	code group model
	•	single-use logic
	•	activation confirmation
	•	type-based stacking
	•	time-based activation
	•	expiration rules
	•	access scope: year, term, month, lesson, specific video(s), specific exam
	•	balance/credit code model
	•	QR code purchase support
	•	direct purchase support
	•	discount management
	•	admin override and modification of any code at any time

0.6 Data Blueprint

Define all required student data:

Personal information:
	•	full name (four-part name)
	•	student phone number (Dostab)
	•	student code (Dostab)
	•	date of birth
	•	gender
	•	governorate
	•	address
	•	parent phone number
	•	parent status (father alive/not, mother alive/not)

Academic information:
	•	education stage: Secondary or Baccalaureate
	•	grade: depends on stage
		◦	Secondary: First Secondary, Second Secondary
		◦	Baccalaureate: First Baccalaureate, Second Baccalaureate
	•	track/branch: only applies to certain grades
		◦	Second Secondary: Arts or Science
		◦	Second Baccalaureate: Medicine and Life Sciences, Engineering and Computer Science, Business, Arts and Humanities

Tracking data:
	•	engagement data
	•	package history
	•	code history

0.7 UX Direction

Define platform experience as:
	•	simple
	•	organized
	•	motivating
	•	clear in path
	•	controlled but not oppressive

0.8 AI Scope Definition

Limit the first AI wave to:
	•	question generation
	•	basic weak-point analysis
	•	essay support
	•	controlled lesson assistant

0.9 Tech Blueprint

Finalize architecture:
	•	Next.js frontend
	•	.NET backend
	•	PostgreSQL
	•	Redis
	•	BullMQ worker
	•	YouTube initially
	•	provider abstraction
	•	SMS integration model
	•	future Zoho/WhatsApp hooks

Deliverables
	•	Product Requirements Document
	•	System Blueprint
	•	User Roles Matrix
	•	Sitemap
	•	Data Model Draft
	•	Business Rules Document
	•	Initial UI Wireframe Direction
	•	Technical Architecture Document

⸻

Phase 1 — Foundation and MVP Launch

Goal

Launch a working web platform that students can actually use.

Objectives
	•	register and log in
	•	activate codes
	•	access packages and lessons
	•	watch videos
	•	solve basic exams
	•	see progress
	•	allow admin control of content and codes

Scope

1.1 Authentication

Implement phone-based auth.

Recommended:
	•	phone registration
	•	JWT auth
	•	refresh tokens
	•	secure session handling

1.2 Student Registration

All required data is collected during registration in a single flow.

Personal data:
	•	full name (four-part name)
	•	student phone number (Dostab)
	•	student code (Dostab)
	•	date of birth
	•	gender
	•	governorate
	•	address
	•	parent phone number
	•	parent status:
		◦	is father alive (yes/no)
		◦	is mother alive (yes/no)

Academic data with conditional logic:
	•	education stage: Secondary or Baccalaureate
	•	grade (depends on selected stage):
		◦	if Secondary: First Secondary or Second Secondary
		◦	if Baccalaureate: First Baccalaureate or Second Baccalaureate
	•	track/branch (only shown for specific grades):
		◦	Second Secondary: Arts or Science
		◦	Second Baccalaureate: Medicine and Life Sciences, Engineering and Computer Science, Business, Arts and Humanities
		◦	First Secondary and First Baccalaureate: no track selection needed

1.3 Roles and Permissions

Implement core roles:
	•	Student
	•	Admin
	•	Teacher
	•	Assistant

Initial assistant permissions can remain basic in Phase 1.

1.4 Public Website

Build:
	•	landing page
	•	about teacher
	•	packages overview
	•	FAQ
	•	login/register
	•	activate code entry point

1.5 Content Hierarchy

Build the full content model:
	•	Package
	•	Year (a package contains a year, which holds any number of terms)
	•	Term
	•	Content Section / Month (content groups within a term)
	•	Lesson
	•	Video Item (each video can have a type/tag set by admin)
	•	Exam
	•	Homework placeholder
	•	Downloadable file placeholder

1.6 Lesson Pages

Each lesson page should support:
	•	embedded video player
	•	written summary
	•	short post-lesson questions
	•	downloadable resources
	•	quick quiz entry
	•	locked/unlocked state

1.7 Video Tracking (MVP)

Track:
	•	lesson opened
	•	video started
	•	video completed
	•	current watch duration
	•	replay count
	•	playback speed
	•	watch events stored in logs

Do not build full smart minute-control engine yet, but build the event structure now.

1.8 Basic Watch Rules

Support in MVP:
	•	allowed speed values only
	•	partial skip rules if simple enough
	•	ability to mark completion
	•	prepare video limit fields in schema

1.9 Code Engine (MVP)

Code types:
	•	year code (access to a full year)
	•	term code (access to a specific term)
	•	month code (access to a specific content section/month)
	•	lesson code (access to a specific lesson)
	•	video code (access to one or more specific videos — admin selects which videos when creating the code type)
	•	exam code (access to a specific exam)
	•	balance/credit code (adds credit to student account for flexible access)

Code purchase and distribution:
	•	QR code — scannable for instant redemption
	•	direct purchase — student buys directly

Code management:
	•	code groups with bulk generation
	•	single-use codes
	•	confirmation before redemption
	•	activation logs
	•	selected activation date support
	•	expiration and validity period
	•	discount code support
	•	admin can modify, extend, revoke, or adjust any code at any time
	•	access assignment after redemption

1.10 Student Dashboard (MVP)

Dashboard should include:
	•	available packages
	•	latest lesson
	•	upcoming/pending exams
	•	progress percentage
	•	used codes
	•	basic notifications
	•	quick access to resume study

1.11 Basic Exam System

Build first exam module with:
	•	MCQ questions
	•	lesson-level tests
	•	instant grading
	•	exam result page
	•	pass threshold support
	•	attempt tracking

1.12 Admin Panel (MVP)

Admin should manage:
	•	students
	•	content structure
	•	packages
	•	lessons
	•	videos
	•	code groups
	•	codes
	•	question bank basics
	•	activation logs

1.13 Logging and Audit

Track:
	•	code generation
	•	code redemption
	•	content edits
	•	permission changes
	•	student state updates

Deliverables
	•	production-ready MVP website
	•	auth system
	•	content system
	•	code engine v1
	•	exam system v1
	•	admin panel v1
	•	student dashboard v1

⸻

Phase 2 — Structured Learning and Academic Operations

Goal

Turn the MVP into a stronger learning operations platform.

Objectives
	•	add homework
	•	add parent visibility
	•	add student behavior follow-up
	•	add assistant workflow
	•	improve academic control

Scope

2.1 Homework System

Add homework with:
	•	MCQ homework
	•	essay homework
	•	mixed homework
	•	due dates
	•	submission state
	•	review state

2.2 Essay Submission Flow

Allow students to submit essay answers.

Support:
	•	text answer
	•	model answer comparison setup
	•	review pipeline

2.3 Parent Layer

Start parent visibility with:
	•	student progress
	•	homework completion
	•	exam summaries
	•	warning indicators

This can begin as shared reports before becoming a full parent dashboard.

2.4 Notification System

Add notification engine for:
	•	missed homework
	•	weak exam result
	•	package warnings
	•	inactivity
	•	code-related events

Channels:
	•	in-platform
	•	SMS for critical events only

2.5 Commitment Engine

Classify student behavior based on:
	•	time spent
	•	lessons completed
	•	exams solved
	•	homework completion
	•	inactivity duration

Statuses:
	•	committed
	•	average
	•	at risk

2.6 Warning System

Build warning logic for:
	•	repeated missed tasks
	•	repeated weak results
	•	inactivity

Warnings can notify:
	•	student
	•	parent
	•	assistant

2.7 Gamification System

Add:
	•	points
	•	badges
	•	levels
	•	leaderboard
	•	challenge logic
	•	progress streaks

2.8 Assistant Roles

Split assistant roles by function:
	•	academic assistant
	•	homework reviewer
	•	follow-up assistant
	•	support assistant

2.9 Assistant Dashboard

Build assistant tools for:
	•	assigned students
	•	pending homework
	•	at-risk students
	•	alert queue
	•	task completion
	•	assistant performance metrics

2.10 Admin Reports

Add reports such as:
	•	hardest lessons
	•	average grade by class
	•	most difficult questions
	•	students likely to decline
	•	code usage by package/group

Deliverables
	•	homework system
	•	parent reporting v1
	•	notifications v1
	•	gamification v1
	•	assistant operations panel
	•	student status engine

⸻

Phase 2.5 — Video Security and Content Protection

Goal

Prevent students from easily extracting YouTube video URLs from the browser, protecting content from unauthorized redistribution.

Objectives
	•	hide YouTube video URLs from DevTools Elements panel
	•	hide YouTube video URLs from DevTools Network source inspection
	•	prevent casual right-click and copy of video sources
	•	maintain full playback functionality and custom player controls
	•	maintain watch count tracking and progress reporting

Scope

2.5.1 Legacy Player Cleanup

Remove the old insecure VideoPlayer component that directly exposed iframe src attributes.
	•	deleted VideoPlayer.tsx
	•	verified no remaining imports reference the legacy component
	•	all video playback now routes through SecureVideoPlayer

2.5.2 Server-Side Video Embed Route

Build a Next.js API route that decrypts the video session token server-side and returns a self-contained HTML page with YouTube embedded.

The flow:
	•	frontend creates encrypted video session via backend
	•	frontend passes encrypted token + key to /api/video/embed as query params
	•	API route decrypts using Node.js crypto (AES-256-GCM) — same algorithm as backend
	•	API route returns HTML page with YouTube IFrame API embedded
	•	the YouTube video ID never reaches the frontend JavaScript context

Security headers on the embed response:
	•	Cache-Control: no-store, no-cache
	•	X-Frame-Options: SAMEORIGIN
	•	Content-Security-Policy: frame-ancestors 'self'
	•	X-Content-Type-Options: nosniff

2.5.3 Closed Shadow DOM Inside Embed Page

The embed HTML page creates the YouTube iframe inside a closed Shadow DOM.

Even if someone drills into the embed iframe in DevTools, they see:
	•	<div id="shell"> with #shadow-root (closed)
	•	the YouTube iframe is not visible in the DOM tree

Additional protections:
	•	shadowRoot getter is overridden to return null
	•	context menu is disabled
	•	text selection and drag are disabled

2.5.4 PostMessage Player Control Protocol

Custom controls communicate with the embedded YouTube player via postMessage.

Commands (parent → embed):
	•	play, pause, seekTo, setVolume, mute, unmute
	•	setPlaybackRate, setQuality, getState

Events (embed → parent):
	•	ready, stateChange, timeUpdate, error, stateResponse

This maintains the full custom PlayerControls UI while keeping YouTube isolated.

2.5.5 DOM Shield Guards

The container element is protected by DOM mutation observers:
	•	detects removal or tampering of the player container
	•	triggers error state with arabic error message if tampering is detected
	•	guardShadowHost() traps programmatic shadowRoot access with a console.warn

2.5.6 Watch Tracking Integration

Watch tracking continues to work through the postMessage bridge:
	•	timeUpdate events every 500ms report currentTime and duration
	•	parent component tracks actualWatchedSeconds
	•	progress is synced to backend every 10 seconds
	•	view is registered after 60 seconds of watching

Deliverables
	•	/api/video/embed API route with server-side decryption
	•	SecureVideoPlayer rewritten with iframe + postMessage architecture
	•	dom-shield.ts enhanced with guardShadowHost
	•	legacy VideoPlayer.tsx deleted
	•	full spec documentation at specs/013-video-url-protection/

⸻

Phase 3 — Registration, Code System & Content Hierarchy Overhaul

Goal

Rebuild the registration flow, expand the code system, and restructure the content hierarchy to match real business requirements.

Objectives
	•	collect all required student data in a single registration flow
	•	support conditional academic fields (stage, grade, track)
	•	expand code types to cover year, term, month, lesson, video, exam, and balance
	•	add QR and direct purchase support for codes
	•	restructure content hierarchy to include Year and Term levels

Scope

3.1 Registration Flow Overhaul

Replace the two-step registration with a single-flow registration collecting all data.

Personal data:
	•	full name (four-part name)
	•	student phone number (Dostab)
	•	student code (Dostab)
	•	date of birth
	•	gender
	•	governorate
	•	address
	•	parent phone number
	•	parent status (father alive/not, mother alive/not)

Academic data with conditional logic:
	•	education stage: Secondary or Baccalaureate
	•	grade (depends on stage):
		◦	Secondary → First Secondary or Second Secondary
		◦	Baccalaureate → First Baccalaureate or Second Baccalaureate
	•	track/branch (only shown for specific grades):
		◦	Second Secondary → Arts or Science
		◦	Second Baccalaureate → Medicine and Life Sciences, Engineering and Computer Science, Business, Arts and Humanities
		◦	First Secondary and First Baccalaureate → no track selection

3.2 Code System Expansion

Expand code types beyond lesson/package/term:
	•	year code (access to a full year)
	•	term code (access to a specific term)
	•	month code (access to a specific content section/month)
	•	lesson code (access to a specific lesson)
	•	video code (access to one or more specific videos — admin selects which videos)
	•	exam code (access to a specific exam)
	•	balance/credit code (adds credit to student account)

Code purchase and distribution:
	•	QR code — scannable for instant redemption
	•	direct purchase — student buys directly

Code management enhancements:
	•	discount code support
	•	expiration and validity period
	•	admin can modify, extend, revoke, or adjust any code at any time

3.3 Content Hierarchy Restructure

Add Year and Term levels to the content hierarchy:
	•	Package
	•	Year (a package contains a year)
	•	Term (a year contains any number of terms)
	•	Content Section / Month (content groups within a term)
	•	Lesson
	•	Video Item (each video can have a type/tag set by admin)

3.4 Database and API Updates

Update schema and API to support:
	•	new student profile fields (DOB, gender, parent status, student code)
	•	conditional grade/track relationships
	•	year and term entities in content hierarchy
	•	new code type entities and balance system
	•	QR generation and scanning endpoints

3.5 Admin Panel Updates

Update admin interfaces for:
	•	new student data fields visibility and filtering
	•	year/term management in content structure
	•	new code type creation (video codes, exam codes, balance codes)
	•	QR code generation and printing
	•	discount management

Deliverables
	•	rebuilt registration flow (single step, all fields)
	•	expanded code engine with 7 code types
	•	QR and direct purchase support
	•	content hierarchy with Year/Term levels
	•	updated database schema and API
	•	updated admin panel

⸻

Phase 4 — Question Bank Expansion and Smart Academic Control

Goal

Make the academic system much stronger and more adaptive.

Objectives
	•	expand question bank deeply
	•	improve exam generation
	•	improve lesson-question connection
	•	improve progression rules
	•	add stronger academic intelligence without going fully AI-heavy yet

Scope

4.1 Advanced Question Bank

Support classification by:
	•	grade
	•	unit
	•	lesson
	•	difficulty
	•	question type
	•	exam year
	•	idea repetition
	•	academic tags
	•	error pattern tags

4.2 Exam Generator

Allow admin/teacher to generate exams based on:
	•	lesson
	•	unit
	•	package
	•	question count
	•	difficulty mix
	•	type mix
	•	pass threshold
	•	timing

4.3 Student-Specific Exam Logic

Allow rules such as:
	•	retry weak area exams
	•	unlock targeted practice
	•	suggest exams based on prior mistakes

4.4 Lesson Intelligence

Link every lesson directly to:
	•	post-lesson questions
	•	recommended revision
	•	homework
	•	relevant question bank segments

4.5 Progression Rules

Support rules such as:
	•	exam required before next item
	•	homework required before next item
	•	pass threshold required
	•	path controlled by code type or package type

4.6 Content Experience Enhancements

Add:
	•	mind map display
	•	revision quick mode
	•	most common mistakes section
	•	key points section
	•	timeline support
	•	maps and comparison displays

Deliverables
	•	advanced question bank
	•	exam generator
	•	progression rule engine v1
	•	stronger content-academic linking
	•	improved academic experience layer

⸻

Phase 5 — AI Layer

Goal

Add limited but valuable AI features.

Objectives
	•	improve academic support
	•	improve weak-point detection
	•	improve essay review
	•	improve question generation
	•	keep AI bounded to teacher content and approved academic scope

Scope

5.1 AI Question Generation

Allow AI-assisted generation based on:
	•	lesson content
	•	teacher notes
	•	question style
	•	target difficulty

4.2 Basic Performance Analysis

AI should analyze:
	•	weak lessons
	•	repeated mistakes
	•	low-performing areas

4.3 Essay Correction Assistant

AI should support essay evaluation using:
	•	teacher-provided model answer
	•	answer comparison
	•	suggested score
	•	feedback points
	•	ideal answer structure

4.4 Controlled Study Assistant

Build a lesson assistant that answers only from:
	•	approved platform content
	•	teacher’s uploaded academic material
	•	bounded lesson context

No open-web generic assistant behavior.

4.5 Explain My Mistake

Add AI-guided explanation of wrong answers:
	•	why the answer is wrong
	•	what concept was missed
	•	what lesson to revisit

4.6 Recommendation Engine

Suggest:
	•	next lesson
	•	next exam
	•	revision content
	•	retry areas

Deliverables
	•	AI question generation v1
	•	essay support AI
	•	weak-point analysis AI
	•	controlled academic assistant
	•	recommendation engine v1

⸻

Phase 6 — Watch Control, Protection, and Smart Access Control

Goal

Add tighter control over lesson consumption and account misuse.

Objectives
	•	enforce watch logic
	•	manage replay limits
	•	manage watch-minute limits
	•	reduce sharing abuse
	•	improve unlock control

Scope

6.1 Video-Level Watch Rules

Each video can support:
	•	max watch minutes
	•	max replay count
	•	completion threshold
	•	partial skip allowance
	•	allowed speed limits

6.2 Admin / Assistant Overrides

Allow authorized users to:
	•	increase watch minutes
	•	increase replay count
	•	unlock video manually
	•	reset lesson watch state if justified

6.3 Account Protection

Implement:
	•	device limit rules
	•	IP behavior tracking
	•	suspicious activity detection
	•	forced logout logic
	•	session review

6.4 Dynamic Watermark

Add watermark overlay later with:
	•	student name
	•	phone or student identifier
	•	moving position logic

6.5 Code Rule Expansion

Support more advanced code logic:
	•	type-based stacking
	•	delayed activation
	•	special access features
	•	selective expiration
	•	combined access + permission logic

6.6 Access Path Logic

Allow lesson unlock based on:
	•	code type
	•	package structure
	•	completion rules
	•	exam score
	•	homework status

Deliverables
	•	watch control engine
	•	account protection enhancements
	•	dynamic watermark v1
	•	advanced code engine
	•	advanced unlock logic

⸻

Phase 7 — Revenue Optimization and Business Analytics

Goal

Turn the platform into a smarter business system.

Objectives
	•	improve conversion
	•	improve retention
	•	improve reporting
	•	improve package sales intelligence

Scope

7.1 Sales Dashboard

Build analytics for:
	•	package sales
	•	code group performance
	•	activation counts
	•	revenue by package
	•	conversion by channel/source

7.2 Source Tracking

Track student acquisition via:
	•	campaign
	•	code source
	•	representative
	•	assistant
	•	center
	•	referral
	•	organic source

7.3 Promotions and Coupons

Support:
	•	discount coupons
	•	upsell codes
	•	event offers
	•	referral rewards

7.4 Smart Upsell

Show offers such as:
	•	unlock term package
	•	unlock revision package
	•	unlock advanced exam package

based on student behavior.

7.5 Dropout and Friction Analytics

Track:
	•	lesson abandonment
	•	inactivity risk
	•	difficult content bottlenecks
	•	drop-off points in study flow

Deliverables
	•	sales analytics
	•	source analytics
	•	promotion engine
	•	upsell system
	•	retention analytics

⸻

6) Recommended System Entities

User and Identity
	•	User
	•	Role
	•	Permission
	•	UserSession
	•	Device
	•	ParentContact

Student Domain
	•	StudentProfile
	•	StudentStatus
	•	StudentProgress
	•	StudentLeaderboard
	•	StudentNotificationPreference

Content Domain
	•	Program
	•	Package
	•	PackageSection
	•	Lesson
	•	LessonVideo
	•	LessonSummary
	•	LessonResource
	•	MindMap
	•	RevisionBlock

Assessment Domain
	•	Exam
	•	ExamQuestion
	•	QuestionBankItem
	•	StudentExamAttempt
	•	StudentAnswer
	•	Homework
	•	HomeworkSubmission

Access Domain
	•	CodeGroup
	•	AccessCode
	•	CodeActivation
	•	StudentAccessGrant

Tracking Domain
	•	VideoWatchEvent
	•	LessonProgress
	•	EngagementMetric
	•	WarningEvent
	•	NotificationEvent
	•	AuditLog

AI Domain
	•	AITask
	•	AIAnalysisResult
	•	EssayReviewResult
	•	WeaknessInsight
	•	RecommendationItem

⸻

7) Recommended API Domains

The backend API should be divided into clear domains.

Auth
	•	register
	•	verify otp
	•	login
	•	refresh token
	•	logout

Profile
	•	get profile
	•	update profile
	•	complete registration
	•	parent data update

Packages and Lessons
	•	list packages
	•	get package details
	•	get lesson details
	•	list lesson videos
	•	get lesson progress

Codes
	•	redeem code
	•	confirm code activation
	•	list user codes
	•	list admin code groups
	•	create code group
	•	bulk generate codes

Exams
	•	get exam
	•	submit exam
	•	get result
	•	list exam history

Homework
	•	list homework
	•	submit homework
	•	review homework
	•	get homework result

Tracking
	•	send watch event
	•	update progress
	•	get engagement state

Notifications
	•	list notifications
	•	mark read
	•	trigger internal notification

Admin
	•	manage users
	•	manage content
	•	manage codes
	•	manage reports
	•	manage settings

AI
	•	generate questions
	•	analyze student weakness
	•	review essay
	•	ask lesson assistant
	•	explain mistake

⸻

8) Recommended Deployment Structure

Services

Recommended initial services:
	•	Frontend Web App — Next.js
	•	Main Backend API — .NET
	•	Node Worker Service — BullMQ workers
	•	PostgreSQL
	•	Redis
	•	Object/File Storage if needed later
	•	Reverse Proxy / Gateway
	•	Monitoring / Logging

Environment separation

Use at least:
	•	Development
	•	Staging
	•	Production

⸻

9) Major Risks to Avoid

9.1 Building too much too early

Do not try to launch AI, advanced parent portal, advanced anti-sharing, and full analytics in the MVP.

9.2 Weak code engine design

The code system is one of the business core engines.
If it is badly designed, the whole monetization layer gets messy.

9.3 Hard-coding YouTube behavior

Build provider abstraction from the start.

9.4 Overloading registration

Use two-step registration or your conversion rate will get punched in the face.

9.5 Too much control too early

The platform should guide and control students, not suffocate them.

9.6 Unbounded AI

AI must stay tied to approved academic content.

⸻

10) Recommended Execution Order

Stage 1
	•	Phase 0
	•	Phase 1

Stage 2
	•	Phase 2
	•	Phase 2.5
	•	Phase 3

Stage 3
	•	Phase 4
	•	Phase 5

Stage 4
	•	Phase 6
	•	Phase 7

⸻

11) What Should Be Built First Immediately

If starting execution tomorrow, the first build order should be:
	1.	technical architecture and repo structure
	2.	database schema draft
	3.	auth and roles
	4.	package/content system
	5.	code engine
	6.	student dashboard
	7.	exam engine
	8.	admin panel
	9.	homework
	10.	notifications
	11.	assistant workflows
	12.	AI
	13.	protection layer
	14.	business analytics

⸻
