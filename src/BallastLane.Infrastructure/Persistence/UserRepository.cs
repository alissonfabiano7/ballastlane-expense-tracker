using BallastLane.Application.Users;
using BallastLane.Domain.Users;
using Microsoft.Data.SqlClient;

namespace BallastLane.Infrastructure.Persistence;

public sealed class UserRepository(SqlExecutor sqlExecutor) : IUserRepository
{
    private const string SelectColumns = "Id, Email, PasswordHash, CreatedAt";

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await sqlExecutor.ExecuteReaderAsync(
            commandText: $"SELECT {SelectColumns} FROM Users WHERE Id = @id",
            configureCommand: cmd => cmd.Parameters.Add("@id", System.Data.SqlDbType.UniqueIdentifier).Value = id,
            read: ReadSingleAsync,
            cancellationToken: cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await sqlExecutor.ExecuteReaderAsync(
            commandText: $"SELECT {SelectColumns} FROM Users WHERE Email = @email",
            configureCommand: cmd =>
            {
                SqlParameter param = cmd.Parameters.Add("@email", System.Data.SqlDbType.NVarChar, 256);
                param.Value = email;
            },
            read: ReadSingleAsync,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        int? count = await sqlExecutor.ExecuteScalarAsync<int>(
            commandText: "SELECT COUNT(1) FROM Users WHERE Email = @email",
            configureCommand: cmd =>
            {
                SqlParameter param = cmd.Parameters.Add("@email", System.Data.SqlDbType.NVarChar, 256);
                param.Value = email;
            },
            cancellationToken: cancellationToken);
        return count is > 0;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await sqlExecutor.ExecuteNonQueryAsync(
            commandText: """
                INSERT INTO Users (Id, Email, PasswordHash, CreatedAt)
                VALUES (@id, @email, @passwordHash, @createdAt);
                """,
            configureCommand: cmd =>
            {
                cmd.Parameters.Add("@id", System.Data.SqlDbType.UniqueIdentifier).Value = user.Id;
                SqlParameter emailParam = cmd.Parameters.Add("@email", System.Data.SqlDbType.NVarChar, 256);
                emailParam.Value = user.Email;
                SqlParameter hashParam = cmd.Parameters.Add("@passwordHash", System.Data.SqlDbType.NVarChar, 500);
                hashParam.Value = user.PasswordHash;
                cmd.Parameters.Add("@createdAt", System.Data.SqlDbType.DateTime2).Value = user.CreatedAt;
            },
            cancellationToken: cancellationToken);
    }

    private static async Task<User?> ReadSingleAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return User.Hydrate(
            id: reader.GetGuid(0),
            email: reader.GetString(1),
            passwordHash: reader.GetString(2),
            createdAt: DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc));
    }
}
