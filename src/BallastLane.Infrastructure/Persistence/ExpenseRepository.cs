using System.Data;
using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using BallastLane.Domain.Expenses;
using Microsoft.Data.SqlClient;

namespace BallastLane.Infrastructure.Persistence;

public sealed class ExpenseRepository(SqlExecutor sqlExecutor) : IExpenseRepository
{
    private const string SelectColumns =
        "Id, UserId, Amount, Description, Category, IncurredAt, CreatedAt";

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        await sqlExecutor.ExecuteNonQueryAsync(
            commandText: """
                INSERT INTO Expenses (Id, UserId, Amount, Description, Category, IncurredAt, CreatedAt)
                VALUES (@id, @userId, @amount, @description, @category, @incurredAt, @createdAt);
                """,
            configureCommand: cmd =>
            {
                cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = expense.Id;
                cmd.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = expense.UserId;
                AddAmountParameter(cmd, expense.Amount);
                AddDescriptionParameter(cmd, expense.Description);
                AddCategoryParameter(cmd, expense.Category);
                cmd.Parameters.Add("@incurredAt", SqlDbType.DateTime2).Value = expense.IncurredAt;
                cmd.Parameters.Add("@createdAt", SqlDbType.DateTime2).Value = expense.CreatedAt;
            },
            cancellationToken: cancellationToken);
    }

    public async Task<Expense?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return await sqlExecutor.ExecuteReaderAsync(
            commandText: $"SELECT {SelectColumns} FROM Expenses WHERE Id = @id AND UserId = @userId",
            configureCommand: cmd =>
            {
                cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
                cmd.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
            },
            read: ReadSingleAsync,
            cancellationToken: cancellationToken);
    }

    public async Task<PagedResult<Expense>> ListByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int offset = (page - 1) * pageSize;

        return await sqlExecutor.ExecuteReaderAsync(
            commandText: $"""
                SELECT COUNT(1) FROM Expenses WHERE UserId = @userId;
                SELECT {SelectColumns} FROM Expenses
                WHERE UserId = @userId
                ORDER BY IncurredAt DESC, Id ASC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
                """,
            configureCommand: cmd =>
            {
                cmd.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                cmd.Parameters.Add("@pageSize", SqlDbType.Int).Value = pageSize;
            },
            read: async (reader, ct) =>
            {
                await reader.ReadAsync(ct).ConfigureAwait(false);
                int totalCount = reader.GetInt32(0);

                await reader.NextResultAsync(ct).ConfigureAwait(false);

                List<Expense> items = [];
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    items.Add(HydrateFromReader(reader));
                }

                return new PagedResult<Expense>(items, page, pageSize, totalCount);
            },
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Expense expense, CancellationToken cancellationToken)
    {
        await sqlExecutor.ExecuteNonQueryAsync(
            commandText: """
                UPDATE Expenses
                SET Amount = @amount,
                    Description = @description,
                    Category = @category,
                    IncurredAt = @incurredAt
                WHERE Id = @id AND UserId = @userId;
                """,
            configureCommand: cmd =>
            {
                cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = expense.Id;
                cmd.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = expense.UserId;
                AddAmountParameter(cmd, expense.Amount);
                AddDescriptionParameter(cmd, expense.Description);
                AddCategoryParameter(cmd, expense.Category);
                cmd.Parameters.Add("@incurredAt", SqlDbType.DateTime2).Value = expense.IncurredAt;
            },
            cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        int rowsAffected = await sqlExecutor.ExecuteNonQueryAsync(
            commandText: "DELETE FROM Expenses WHERE Id = @id AND UserId = @userId;",
            configureCommand: cmd =>
            {
                cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
                cmd.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
            },
            cancellationToken: cancellationToken);
        return rowsAffected > 0;
    }

    private static void AddAmountParameter(SqlCommand cmd, decimal value)
    {
        SqlParameter param = cmd.Parameters.Add("@amount", SqlDbType.Decimal);
        param.Precision = 18;
        param.Scale = 2;
        param.Value = value;
    }

    private static void AddDescriptionParameter(SqlCommand cmd, string? value)
    {
        SqlParameter param = cmd.Parameters.Add("@description", SqlDbType.NVarChar, 500);
        param.Value = (object?)value ?? DBNull.Value;
    }

    private static void AddCategoryParameter(SqlCommand cmd, ExpenseCategory category)
    {
        SqlParameter param = cmd.Parameters.Add("@category", SqlDbType.NVarChar, 50);
        param.Value = category.ToString();
    }

    private static async Task<Expense?> ReadSingleAsync(
        SqlDataReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }
        return HydrateFromReader(reader);
    }

    private static Expense HydrateFromReader(SqlDataReader reader)
    {
        return Expense.Hydrate(
            id: reader.GetGuid(0),
            userId: reader.GetGuid(1),
            amount: reader.GetDecimal(2),
            description: reader.IsDBNull(3) ? null : reader.GetString(3),
            category: Enum.Parse<ExpenseCategory>(reader.GetString(4)),
            incurredAt: DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
            createdAt: DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc));
    }
}
