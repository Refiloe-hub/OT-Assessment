using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using OT.Assessment.Consumer.Models;

namespace OT.Assessment.Consumer.Persistence;

public class SqlWagerRepository : IWagerRepository
{
    private readonly string _connectionString;

    public SqlWagerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DatabaseConnection")
            ?? throw new InvalidOperationException("Connection string 'DatabaseConnection' is missing.");
    }

    public async Task SaveAsync(CasinoWagerMessage wager, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.usp_InsertCasinoWager", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@WagerId", wager.WagerId);
        command.Parameters.AddWithValue("@AccountId", wager.AccountId);
        command.Parameters.AddWithValue("@Username", wager.Username);
        command.Parameters.AddWithValue("@Provider", wager.Provider);
        command.Parameters.AddWithValue("@GameName", wager.GameName);
        command.Parameters.AddWithValue("@Theme", (object)wager.Theme ?? DBNull.Value);
        command.Parameters.AddWithValue("@BrandId", wager.BrandId);
        command.Parameters.AddWithValue("@TransactionId", wager.TransactionId);
        command.Parameters.AddWithValue("@ExternalReferenceId", wager.ExternalReferenceId);
        command.Parameters.AddWithValue("@TransactionTypeId", wager.TransactionTypeId);
        command.Parameters.AddWithValue("@Amount", wager.Amount);
        command.Parameters.AddWithValue("@NumberOfBets", wager.NumberOfBets);
        command.Parameters.AddWithValue("@CountryCode", wager.CountryCode);
        command.Parameters.AddWithValue("@DurationMs", wager.Duration);
        command.Parameters.AddWithValue("@CreatedDateTime", wager.CreatedDateTime);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}