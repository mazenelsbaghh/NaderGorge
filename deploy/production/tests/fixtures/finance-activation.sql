CREATE TABLE teacher_profiles ("Id" uuid PRIMARY KEY, "FinancePreset" int NOT NULL);
INSERT INTO teacher_profiles SELECT gen_random_uuid(),0 FROM generate_series(1,17);
INSERT INTO teacher_profiles VALUES ('73cf9e05-d068-4a0c-b8e6-dcba16554417',1),('ff2b0754-3dcd-4b33-85a9-f5869e0c2768',1),('2a0e7d2f-1dd7-4af0-9974-c999489899b2',2);
CREATE TABLE teacher_financial_agreements (
 "Id" uuid PRIMARY KEY,"TeacherId" uuid,"ScopeType" int,"ScopeId" uuid,"Trigger" int,"AllocationMode" int,"AllocationValue" numeric,
 "PriceBasis" int,"EffectiveFrom" timestamptz,"EffectiveTo" timestamptz,"IsActive" bool,"Reason" text,"CreatedByUserId" uuid,"CreatedAt" timestamptz,"UpdatedAt" timestamptz);
INSERT INTO teacher_financial_agreements SELECT gen_random_uuid(),"Id",5,NULL,2,0,70,1,CURRENT_TIMESTAMP-interval '1 day',NULL,true,'previous terms',NULL,CURRENT_TIMESTAMP-interval '1 day',NULL FROM teacher_profiles;
CREATE TABLE code_groups ("Id" uuid PRIMARY KEY,"TeacherId" uuid,"Name" text,"AccountingRecordedAt" timestamptz,"CodeType" int,"AccountingTiming" int,"RevenueAllocationMode" int,"RevenueAllocationValue" numeric);
INSERT INTO code_groups VALUES
 ('10000000-0000-0000-0000-000000000001','2a0e7d2f-1dd7-4af0-9974-c999489899b2','fixture-1',NULL,0,1,0,70),
 ('10000000-0000-0000-0000-000000000002','2a0e7d2f-1dd7-4af0-9974-c999489899b2','fixture-2',NULL,0,1,0,70),
 ('10000000-0000-0000-0000-000000000003','2a0e7d2f-1dd7-4af0-9974-c999489899b2','fixture-3',CURRENT_TIMESTAMP,0,1,0,70);
CREATE TABLE teacher_financial_events ("Id" uuid PRIMARY KEY,"SourceType" int,"SourceId" uuid);
CREATE TABLE access_codes ("Id" uuid PRIMARY KEY,"CodeGroupId" uuid,"IsConsumed" bool);
INSERT INTO access_codes VALUES (gen_random_uuid(),'10000000-0000-0000-0000-000000000002',true);
CREATE TABLE access_code_activation_logs ("AccessCodeId" uuid);
CREATE TABLE code_group_financial_terms ("CodeGroupId" uuid,"AgreementId" uuid,"Trigger" int);
INSERT INTO code_group_financial_terms SELECT "Id",NULL,1 FROM code_groups;
