--
-- PostgreSQL database dump
--

\restrict 1EHQ9rNdNMY1ga79FEtcpH0eyW9bYhPK0r0XZC0PxESFbqtnoTXpCK0MeUFBIKE

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Data for Name: roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.roles VALUES ('3fa3878a-0384-451a-bccf-7935eafa4e36', 'Assistant', 3, '[]', '2026-06-09 03:03:17.496994', NULL);
INSERT INTO public.roles VALUES ('745abe81-a693-48ec-ac64-a39ad66ff188', 'Teacher', 2, '[]', '2026-06-09 03:03:17.49719', NULL);
INSERT INTO public.roles VALUES ('b2ef45b1-ed94-467c-9a09-34617fe96074', 'Student', 4, '[]', '2026-06-09 03:03:17.4968', NULL);
INSERT INTO public.roles VALUES ('ecde77c9-6b7e-4c81-ba00-4f9272eac53b', 'Admin', 1, '[]', '2026-06-09 03:03:17.496526', NULL);


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.users VALUES ('33b9485a-b875-4787-8871-9a6925063287', 'E2E Student MaxDevices', '20000000002', '$2a$11$fnLtBoREaLXsy2PrvAp5H..xFmlmo4WiSVR6kkuHUQixECu9.5PK6', true, true, NULL, '2026-06-09 03:03:17.938373', NULL);
INSERT INTO public.users VALUES ('4402e47d-3a5c-42cb-9ba2-c569b220bf1a', 'E2E Assistant', '20000000003', '$2a$11$Jdl08qmgUdNI3QEdTrnbt.O5JG.IG8N4qzFH3MkXbVaOq1N4P1anq', true, true, NULL, '2026-06-09 03:03:17.623249', NULL);
INSERT INTO public.users VALUES ('a92ec259-0bc1-407b-b88b-d47af2bfaa26', 'E2E Student 1', '20000000001', '$2a$11$Y2GbRuMQoq0ZfmUJ2lyq8OQWf4kMKnZ2Ev4bzpVLlvF6XXxfI.ZFW', true, true, NULL, '2026-06-09 03:03:17.836168', NULL);
INSERT INTO public.users VALUES ('dd3dc8cc-6fc4-46db-b878-1d0eeee90b72', 'E2E Teacher', '20000000004', '$2a$11$hIFsIPsgz.L9gi5LhZnSHeuEe/gmPL3QpWlkNFi1Ji7aquZnuz7p6', true, true, NULL, '2026-06-09 03:03:17.72806', NULL);
INSERT INTO public.users VALUES ('e48d7d52-e8ae-4238-a1b1-9b349599007d', 'E2E Admin', '20000000000', '$2a$11$Ljw0kIvYMtSRSGmtuldFee7cbFdFMStfZhjf1wWzeURDE2MmIVJdu', true, true, NULL, '2026-06-09 03:03:17.4979', NULL);


--
-- Data for Name: student_profiles; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- Data for Name: teacher_profiles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.teacher_profiles VALUES ('9d89c77e-fdf4-4076-9801-e42bf7bf7c4b', 'dd3dc8cc-6fc4-46db-b878-1d0eeee90b72', 'E2E Teacher Bio', 'Physics', 0.20, NULL, '', '2026-06-09 03:03:17.836139', NULL);


--
-- Data for Name: user_roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.user_roles VALUES ('33b9485a-b875-4787-8871-9a6925063287', 'b2ef45b1-ed94-467c-9a09-34617fe96074');
INSERT INTO public.user_roles VALUES ('4402e47d-3a5c-42cb-9ba2-c569b220bf1a', '3fa3878a-0384-451a-bccf-7935eafa4e36');
INSERT INTO public.user_roles VALUES ('a92ec259-0bc1-407b-b88b-d47af2bfaa26', 'b2ef45b1-ed94-467c-9a09-34617fe96074');
INSERT INTO public.user_roles VALUES ('dd3dc8cc-6fc4-46db-b878-1d0eeee90b72', '745abe81-a693-48ec-ac64-a39ad66ff188');
INSERT INTO public.user_roles VALUES ('e48d7d52-e8ae-4238-a1b1-9b349599007d', 'ecde77c9-6b7e-4c81-ba00-4f9272eac53b');


--
-- PostgreSQL database dump complete
--

\unrestrict 1EHQ9rNdNMY1ga79FEtcpH0eyW9bYhPK0r0XZC0PxESFbqtnoTXpCK0MeUFBIKE

