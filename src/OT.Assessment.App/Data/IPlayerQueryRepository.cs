using OT.Assessment.App.Models;

namespace OT.Assessment.App.Data;

public interface IPlayerQueryRepository
{
    Task<PagedResponse<WagerResponse>> GetPlayerWagersAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<TopSpenderResponse>> GetTopSpendersAsync(int count, CancellationToken cancellationToken);
}