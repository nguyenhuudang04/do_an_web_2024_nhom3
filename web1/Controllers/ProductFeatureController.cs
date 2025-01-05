using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web1.Data;
using web1.Models;
using web1.Services;
using Microsoft.AspNetCore.Authorization;

namespace web1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductFeatureController : Controller
    {
        // Truy cập dữ liệu từ cơ sở dữ liệu, bao gồm bảng sản phẩm, hình ảnh, và đặc trưng.
        private readonly ApplicationDbContext _context;
        //Dịch vụ trích xuất đặc trưng từ hình ảnh.
        private readonly ImageProcessingService _imageProcessingService;
        private readonly ILogger<ProductFeatureController> _logger;

        //ung cấp thông tin về môi trường web, như đường dẫn gốc của ứng dụng.
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductFeatureController(
            ApplicationDbContext context,
            ImageProcessingService imageProcessingService,
            ILogger<ProductFeatureController> logger,
            IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _imageProcessingService = imageProcessingService;
            _logger = logger;
            _hostEnvironment = hostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductFeatures)
                .ToListAsync();

            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> ExtractFeatures(int productId)
        {
            try
            {
                //ìm sản phẩm theo ID:
                var product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.ProductFeatures)
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null)
                {
                    return NotFound();
                }

                if (!product.Images.Any())
                {
                    return BadRequest("Product has no images");
                }

                // Xóa các đặc trưng cũ
                _context.ProductFeatures.RemoveRange(product.ProductFeatures);

                foreach (var image in product.Images)
                {
                    // Đường dẫn đến file ảnh
                    string imagePath = Path.Combine(_hostEnvironment.WebRootPath, "images", "products", image.ImagePath);
                    if (!System.IO.File.Exists(imagePath))
                    {
                        continue;
                    }

                    // Đọc file ảnh
                    using var stream = new FileStream(imagePath, FileMode.Open);
                    var formFile = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(imagePath));

                    // Trích xuất đặc trưng
                    var features = await _imageProcessingService.ExtractFeatures(formFile);
                    if (features != null && features.Any())
                    {
                        var featureVector = string.Join(",", features);
                        var productFeature = new ProductFeature
                        {
                            ProductId = product.Id,
                            FeatureVector = featureVector
                        };
                        _context.ProductFeatures.Add(productFeature);
                    }
                }
                //Lưu đặc trưng mới vào cơ sở dữ liệu.
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting features for product {ProductId}", productId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExtractAllFeatures()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.ProductFeatures)
                    .ToListAsync();

                int successCount = 0;
                foreach (var product in products)
                {
                    if (!product.Images.Any())
                        continue;

                    try
                    {
                        // Xóa đặc trưng cũ
                        _context.ProductFeatures.RemoveRange(product.ProductFeatures);

                        foreach (var image in product.Images)
                        {
                            string imagePath = Path.Combine(_hostEnvironment.WebRootPath, "images", "products", image.ImagePath);
                            if (!System.IO.File.Exists(imagePath))
                                continue;

                            using var stream = new FileStream(imagePath, FileMode.Open);
                            var formFile = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(imagePath));

                            var features = await _imageProcessingService.ExtractFeatures(formFile);
                            if (features != null && features.Any())
                            {
                                var featureVector = string.Join(",", features);
                                var productFeature = new ProductFeature
                                {
                                    ProductId = product.Id,
                                    FeatureVector = featureVector
                                };
                                _context.ProductFeatures.Add(productFeature);
                            }
                        }
                        await _context.SaveChangesAsync();
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing product {ProductId}", product.Id);
                    }
                }

                return Json(new { success = true, processedCount = successCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch processing");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
} 