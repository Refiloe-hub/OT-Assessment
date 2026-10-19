namespace OT.Assessment.App.Models;

public class PagedResponse<T>
{
    public IReadOnlyList<T> Data { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}