using R3E.Data;

namespace R3E.API
{
    public interface ISharedSource
    {
        event Action<Shared>? DataUpdated;
        Shared Data { get; }
    }
}
