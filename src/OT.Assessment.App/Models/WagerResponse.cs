namespace OT.Assessment.App.Models;

public class WagerResponse
{
    public Guid WagerId { get; set; }
    public string Game { get; set; }
    public string Provider { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}