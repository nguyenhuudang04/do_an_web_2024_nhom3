using Microsoft.AspNetCore.Mvc;
using web1.Services;
using web1.Models;
using Microsoft.Extensions.Configuration;

namespace web1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageSearchController : ControllerBase
    {
        //Dùng để trích xuất đặc trưng từ ảnh.
        private readonly ImageProcessingService _imageProcessingService;
        //Dùng để tìm sản phẩm tương tự dựa trên đặc trưng.
        private readonly ImageMatchingService _imageMatchingService;
        private readonly ILogger<ImageSearchController> _logger;
        private readonly IConfiguration _configuration;

        public ImageSearchController(
            ImageProcessingService imageProcessingService,
            ImageMatchingService imageMatchingService,
            ILogger<ImageSearchController> logger,
            IConfiguration configuration)
        {
            _imageProcessingService = imageProcessingService;
            _imageMatchingService = imageMatchingService;
            _logger = logger;
            _configuration = configuration;
        }
        //Nhận file ảnh từ người dùng, trích xuất đặc trưng, tìm sản phẩm tương tự, và trả về kết quả.
        [HttpPost("search")]
        public async Task<IActionResult> Search(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                {
                    return BadRequest("No image file provided");
                }

                // Kiểm tra kích thước file
                var maxFileSize = _configuration.GetValue<long>("FileUpload:MaxFileSize");
                if (imageFile.Length > maxFileSize)
                {
                    return BadRequest($"File size exceeds maximum limit of {maxFileSize / 1024 / 1024}MB");
                }

                // Kiểm tra định dạng file
                var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions")
                    .Get<string[]>() ?? new[] { ".jpg", ".jpeg", ".png" };
                
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest($"Invalid file format. Allowed formats: {string.Join(", ", allowedExtensions)}");
                }

                // Trích xuất đặc trưng
                var features = await _imageProcessingService.ExtractFeatures(imageFile);
                if (features == null || features.Count == 0)
                {
                    return BadRequest("Could not extract features from the image");
                }

                // Tìm sản phẩm tương tự
                var similarProducts = await _imageMatchingService.FindSimilarProducts(features);

                // Format kết quả trả về
                var results = similarProducts.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    ImageUrl = p.Images.FirstOrDefault()?.ImagePath ?? "default.jpg",
                    p.Description,
                    CategoryName = p.Category?.Name,
                    DiscountedPrice = p.GetDiscountedPrice()
                });

                _logger.LogInformation($"Found {similarProducts.Count} similar products");
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image search");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
    }
} 