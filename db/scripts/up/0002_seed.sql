-- Idempotent seed: demo user + sample expenses across categories.
-- Demo credentials: demo@ballastlane.test / Demo@123
-- The hash below is Argon2id with parameters m=19456, t=2, p=1 (OWASP 2024).
-- Verified by Argon2idPasswordHasherTests.Verify_known_seed_hash_succeeds.

DECLARE @DemoUserId   UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @DemoEmail    NVARCHAR(256)    = N'demo@ballastlane.test';
DECLARE @DemoHash     NVARCHAR(500)    =
    N'argon2id$m=19456$t=2$p=1$/bbweUenC6ZPycMfvsiSLw==$jUvhKxaxhXbTcD1CHOCT7fDbXLtt2wM/8rWdfE632/Y=';

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @DemoUserId)
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, CreatedAt)
    VALUES (@DemoUserId, @DemoEmail, @DemoHash, SYSUTCDATETIME());
END;

;WITH SeedExpenses (Id, Amount, Description, Category, DaysAgo) AS (
    SELECT * FROM (VALUES
        (CAST('22222222-2222-2222-2222-222222220001' AS UNIQUEIDENTIFIER),
            CAST(12.50 AS DECIMAL(18,2)), N'Coffee at downtown cafe',         N'Food',      1),
        (CAST('22222222-2222-2222-2222-222222220002' AS UNIQUEIDENTIFIER),
            CAST(45.00 AS DECIMAL(18,2)), N'Weekly groceries',                N'Food',      3),
        (CAST('22222222-2222-2222-2222-222222220003' AS UNIQUEIDENTIFIER),
            CAST(28.75 AS DECIMAL(18,2)), N'Rideshare to office',             N'Transport', 2),
        (CAST('22222222-2222-2222-2222-222222220004' AS UNIQUEIDENTIFIER),
            CAST(1500.00 AS DECIMAL(18,2)), N'Monthly rent',                  N'Housing',   5),
        (CAST('22222222-2222-2222-2222-222222220005' AS UNIQUEIDENTIFIER),
            CAST(60.00 AS DECIMAL(18,2)), N'Cinema night',                    N'Leisure',   7),
        (CAST('22222222-2222-2222-2222-222222220006' AS UNIQUEIDENTIFIER),
            CAST(120.00 AS DECIMAL(18,2)), N'Pharmacy refill',                N'Health',    4),
        (CAST('22222222-2222-2222-2222-222222220007' AS UNIQUEIDENTIFIER),
            CAST(199.00 AS DECIMAL(18,2)), N'Online course subscription',     N'Education', 10),
        (CAST('22222222-2222-2222-2222-222222220008' AS UNIQUEIDENTIFIER),
            CAST(35.40 AS DECIMAL(18,2)), N'Hardware store',                  N'Other',     6)
    ) AS v(Id, Amount, Description, Category, DaysAgo)
)
INSERT INTO Expenses (Id, UserId, Amount, Description, Category, IncurredAt, CreatedAt)
SELECT s.Id, @DemoUserId, s.Amount, s.Description, s.Category,
       DATEADD(DAY, -s.DaysAgo, SYSUTCDATETIME()),
       SYSUTCDATETIME()
FROM SeedExpenses s
WHERE NOT EXISTS (SELECT 1 FROM Expenses e WHERE e.Id = s.Id);
