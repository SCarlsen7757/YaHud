namespace R3E.Features.Image
{
    public interface IImageService
    {
        Task<string> GetManufacturerImageAsync(int manufacturerId, ImageSize size);
        Task<string> GetClassImageAsync(int classId, ImageSize size);

        string GetManufacturerImageCached(int manufacturerId, ImageSize size);
        string GetClassImageCached(int classId, ImageSize size);
    }
}
