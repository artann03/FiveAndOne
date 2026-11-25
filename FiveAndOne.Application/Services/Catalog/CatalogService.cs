using FiveAndOne.Application.Dtos.Catalog;
using FiveAndOne.Infrastructure.Repositories.Catalog;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Catalog;

public interface ICatalogService
{
    Task<List<SportResponse>> GetSportsAsync(CancellationToken cancellationToken = default);
    Task<List<AmenityResponse>> GetAmenitiesAsync(CancellationToken cancellationToken = default);
}

public class CatalogService : ICatalogService
{
    private readonly ICatalogRepository _catalog;

    public CatalogService(ICatalogRepository catalog)
    {
        _catalog = catalog;
    }

    public async Task<List<SportResponse>> GetSportsAsync(CancellationToken cancellationToken = default)
    {
        var sports = await _catalog.GetSportsAsync(activeOnly: true, cancellationToken);
        return sports.Select(SportResponse.FromEntity).ToList();
    }

    public async Task<List<AmenityResponse>> GetAmenitiesAsync(CancellationToken cancellationToken = default)
    {
        var amenities = await _catalog.GetAmenitiesAsync(cancellationToken);
        return amenities.Select(AmenityResponse.FromEntity).ToList();
    }
}
