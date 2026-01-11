using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace R3E.Features.Image
{
    public class ImageService : IImageService
    {
        private const string IMAGE_REDIRECT = "https://game.raceroom.com/store/image_redirect?id=";
        private const string FALLBACK_IMAGE = "https://prod.r3eassets.com/assets/content/carmanufactor/raceroom-4596-image-full.webp";

        private readonly HttpClient httpClient;
        private readonly ILogger<ImageService> logger;
        private readonly IMemoryCache cache;

        public ImageService(ILogger<ImageService> logger,
                            IMemoryCache memoryCache,
                            HttpClient httpClient)
        {
            this.logger = logger ?? NullLogger<ImageService>.Instance;
            this.cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<string> GetManufacturerImageAsync(int manufacturerId, ImageSize size = ImageSize.Small)
        {
            logger.LogDebug("Fetching manufacturer image for ID: {ManufacturerId} with size: {Size}", manufacturerId, size);
            var imageUrl = await GetImageUrlAsync(manufacturerId.ToString(), size);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Manufacturer ID: {ManufacturerId}, Image URL: {ImageUrl}", manufacturerId, imageUrl);
                if (!imageUrl.Contains("carmanufactor"))
                {
                    logger.LogWarning("Unexpected image URL format for manufacturer ID: {ManufacturerId}", manufacturerId);
                }
            }
            return imageUrl;
        }

        public async Task<string> GetClassImageAsync(int classId, ImageSize size = ImageSize.Small)
        {
            logger.LogDebug("Fetching class image for ID: {ClassId} with size: {Size}", classId, size);
            var imageUrl = await GetImageUrlAsync(classId.ToString(), size);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Class ID: {ClassId}, Image URL: {ImageUrl}", classId, imageUrl);
                if (!imageUrl.Contains("carclass"))
                {
                    logger.LogWarning("Unexpected image URL format for class ID: {ClassId}", classId);
                }
            }
            return imageUrl;
        }

        private async Task<string> GetImageUrlAsync(string id, ImageSize size)
        {
            var cacheKey = $"img_{id}_{size}";

            if (cache.TryGetValue(cacheKey, out string? cachedUrl))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Cache hit for image ID {Id} with size {Size}", id, size);
                }

                return cachedUrl!;
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Cache miss for image ID {Id} with size {Size}", id, size);
            }

            try
            {
                var url = BuildImageUrl(id, size);
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode && response.RequestMessage?.RequestUri != null)
                {
                    var imageUrl = response.RequestMessage.RequestUri.ToString();
                    logger.LogDebug("Successfully retrieved image URL for ID {Id}: {ImageUrl}", id, imageUrl);

                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    };
                    cache.Set(cacheKey, imageUrl, cacheOptions);

                    return imageUrl;
                }

                logger.LogWarning("Failed to retrieve image for ID {Id}. Status: {StatusCode}", id, response.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while fetching image for ID: {Id}", id);
            }

            logger.LogDebug("Returning fallback image for ID: {Id}", id);
            return FALLBACK_IMAGE;
        }

        private static string BuildImageUrl(string id, ImageSize size)
        {
            var baseUrl = IMAGE_REDIRECT + id;
            return size switch
            {
                ImageSize.Thumb => $"{baseUrl}&size=thumb",
                ImageSize.Small => $"{baseUrl}&size=small",
                ImageSize.Full => baseUrl,
                _ => baseUrl
            };
        }
    }
}
