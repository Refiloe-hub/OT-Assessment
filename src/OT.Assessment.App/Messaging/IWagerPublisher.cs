using OT.Assessment.App.Models;

namespace OT.Assessment.App.Messaging;

public interface IWagerPublisher
{
    Task PublishAsync(CasinoWagerRequest wager, CancellationToken cancellationToken = default);
}