namespace OT.Assessment.App.Models;

public class CasinoWagerRequest
{
    public Guid WagerId { get; set; }
    public string Theme { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public Guid TransactionId { get; set; }
    public Guid BrandId { get; set; }
    public Guid AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public Guid ExternalReferenceId { get; set; }
    public Guid TransactionTypeId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedDateTime { get; set; }
    public int NumberOfBets { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string SessionData { get; set; }
    public long Duration { get; set; }
}