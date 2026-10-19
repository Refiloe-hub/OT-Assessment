using System.Data;
using Microsoft.Data.SqlClient;
using OT.Assessment.App.Models;

namespace OT.Assessment.App.Data;

public class SqlPlayerQueryRepository : IPlayerQueryRepository
{
    private readonly string _connectionString;

    public SqlPlayerQueryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DatabaseConnection")
            ?? throw new InvalidOperationException("Connection string 'DatabaseConnection' is missing.");
    }

    public async Task<PagedResponse<WagerResponse>> GetPlayerWagersAsync(
        Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.usp_GetPlayerWagers", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@AccountId", accountId);
        command.Parameters.AddWithValue("@Page", page);
        command.Parameters.AddWithValue("@PageSize", pageSize);

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<WagerResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new WagerResponse
            {
                WagerId = reader.GetGuid(0),
                Game = reader.GetString(1),
                Provider = reader.GetString(2),
                Amount = reader.GetDecimal(3),
                CreatedDate = reader.GetDateTimeOffset(4)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var total = reader.GetInt32(0);

        return new PagedResponse<WagerResponse>
        {
            Data = items,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<IReadOnlyList<TopSpenderResponse>> GetTopSpendersAsync(int count, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.usp_GetTopSpenders", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@Count", count);

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<TopSpenderResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TopSpenderResponse
            {
                AccountId = reader.GetGuid(0),
                Username = reader.GetString(1),
                TotalAmountSpend = reader.GetDecimal(2)
            });
        }
        return items;
    }
}