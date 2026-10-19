using OT.Assessment.Consumer.Models;

namespace OT.Assessment.Consumer.Persistence;

public interface IWagerRepository
{
    Task SaveAsync(CasinoWagerMessage wager, CancellationToken cancellationToken);
}