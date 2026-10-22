using Optinull.Application.Routing;

namespace Optinull.Application.Tests.Maps;

/// <summary>Serves one fixed image for every tile, or nothing when it is switched off.</summary>
internal sealed class FakeTiles : IMapTileClient
{
    private readonly byte[]? _image;

    public FakeTiles(byte[]? image) => _image = image;

    public string Attribution => "\u00a9 Test map";

    public int Calls { get; private set; }

    public Task<byte[]?> GetTileAsync(int zoom, int x, int y, CancellationToken cancellationToken = default)
    {
        Calls++;

        return Task.FromResult(_image);
    }
}
