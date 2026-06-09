INSERT INTO users ("Id", "FullName", "PhoneNumber", "PasswordHash", "IsActive", "IsProfileComplete", "CreatedAt")
VALUES ('f4b82937-293e-48a3-a002-decf9a1efab8', 'System Admin', '01000000000', '$2a$11$Ub5qfncCa6acE7Ipis5sdeQPu0RkFsTgEEMdx3WgtDF/w//CMmXeG', true, true, NOW())
ON CONFLICT ("PhoneNumber") DO NOTHING;

INSERT INTO user_roles ("UserId", "RoleId")
VALUES ('f4b82937-293e-48a3-a002-decf9a1efab8', 'cf96578e-27c7-402e-b394-740e805c5f65')
ON CONFLICT DO NOTHING;

INSERT INTO users ("Id", "FullName", "PhoneNumber", "PasswordHash", "IsActive", "IsProfileComplete", "CreatedAt")
VALUES ('e4b82937-293e-48a3-a002-decf9a1efab8', 'Student User', '01234567890', '$2a$11$cyThAVQyyCXIprrLnAK8o.IBNiFbfmXlWyyc5SExtmOsK50c.b0TW', true, true, NOW())
ON CONFLICT ("PhoneNumber") DO NOTHING;

INSERT INTO user_roles ("UserId", "RoleId")
VALUES ('e4b82937-293e-48a3-a002-decf9a1efab8', 'e1f4d51f-a2dd-44b4-8d37-85a8b86709ba')
ON CONFLICT DO NOTHING;

UPDATE users SET "PasswordHash" = '$2a$11$.u4R1QuDrWR50LlZYu7wx.xijQHJlXvxmf0AF4hETg..xQ3t3i9t.' WHERE "PhoneNumber" = '01111111111';
