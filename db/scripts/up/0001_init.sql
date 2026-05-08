-- Initial schema for BallastLane Personal Expense Tracker
-- Tables: Users (auth) + Expenses (business entity)

CREATE TABLE Users
(
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY DEFAULT NEWID(),
    Email           NVARCHAR(256)    NOT NULL,
    PasswordHash    NVARCHAR(500)    NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);

CREATE TABLE Expenses
(
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Expenses PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    Amount          DECIMAL(18, 2)   NOT NULL,
    Description     NVARCHAR(500)    NULL,
    Category        NVARCHAR(50)     NOT NULL,
    IncurredAt      DATETIME2(3)     NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Expenses_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
    CONSTRAINT CK_Expenses_Amount_Positive CHECK (Amount > 0),
    CONSTRAINT CK_Expenses_Category CHECK (Category IN ('Food', 'Transport', 'Housing', 'Leisure', 'Health', 'Education', 'Other'))
);

CREATE INDEX IX_Expenses_UserId_IncurredAt
    ON Expenses (UserId, IncurredAt DESC);
