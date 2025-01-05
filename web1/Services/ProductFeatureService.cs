using web1.Data;
using web1.Models;

namespace web1.Services
{
    public class ProductFeatureService
    {
        private readonly ApplicationDbContext _context;
        private readonly ImageProcessingService _imageProcessingService;
        private readonly ILogger<ProductFeatureService> _logger;

        public ProductFeatureService(
            ApplicationDbContext context,
            ImageProcessingService imageProcessingService,
            ILogger<ProductFeatureService> logger)
        {
            _context = context;
            _imageProcessingService = imageProcessingService;
            _logger = logger;
        }

        public async Task ExtractAndSaveFeatures(Product product, IFormFile imageFile)
        {
            try
            {
                var features = await _imageProcessingService.ExtractFeatures(imageFile);
                var featureVector = string.Join(",", features);

                var productFeature = new ProductFeature
                {
                    ProductId = product.Id,
                    FeatureVector = featureVector
                };

                _context.ProductFeatures.Add(productFeature);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting and saving features for product {ProductId}", product.Id);
                throw;
            }
        }
    }
} 