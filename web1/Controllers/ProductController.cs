using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web1.Data;
using web1.Models;
using Microsoft.AspNetCore.Authorization;
using web1.Services;

namespace web1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<ProductController> _logger;
        private readonly ProductFeatureService _productFeatureService;
        private readonly string _productImageFolder;

        public ProductController(ApplicationDbContext context, 
            IWebHostEnvironment hostEnvironment,
            ILogger<ProductController> logger,
            ProductFeatureService productFeatureService)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _productFeatureService = productFeatureService;

            // Tạo các thư mục cần thiết nếu chưa tồn tại
            var wwwrootPath = hostEnvironment.WebRootPath;
            var imagesPath = Path.Combine(wwwrootPath, "images");
            var productsPath = Path.Combine(imagesPath, "products");
            var defaultPath = Path.Combine(imagesPath, "default");

            Directory.CreateDirectory(imagesPath);
            Directory.CreateDirectory(productsPath);
            Directory.CreateDirectory(defaultPath);

            // Tạo file no-image.png nếu chưa tồn tại
            var noImagePath = Path.Combine(defaultPath, "no-image.png");
            if (!System.IO.File.Exists(noImagePath))
            {
                using (var bitmap = new System.Drawing.Bitmap(200, 200))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        g.Clear(System.Drawing.Color.LightGray);
                        g.DrawString("No Image", 
                            new System.Drawing.Font("Arial", 16),
                            System.Drawing.Brushes.Gray,
                            new System.Drawing.PointF(50, 80));
                    }
                    bitmap.Save(noImagePath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            _productImageFolder = Path.Combine(hostEnvironment.WebRootPath, "images", "products");
            
            // Tự động tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(_productImageFolder))
            {
                Directory.CreateDirectory(_productImageFolder);
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Chỉ lấy các đánh giá đã được phê duyệt
            ViewBag.ApprovedReviews = product.Reviews.Where(r => r.IsApproved).ToList();

            // Lấy các sản phẩm liên quan cùng category
            var relatedProducts = await _context.Products
                .Include(p => p.Images)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            return View(product);
        }

        public async Task<IActionResult> Index(int? categoryId)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Category)
                    .AsQueryable();

                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                var products = await query.ToListAsync();
                ViewBag.Categories = await _context.Categories.ToListAsync();
                ViewBag.SelectedCategoryId = categoryId;
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                return View("Error");
            }
        }

        public IActionResult Create()
        {
            ViewBag.Categories = _context.Categories.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, List<IFormFile> images)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    foreach (var image in images)
                    {
                        if (image != null && image.Length > 0)
                        {
                            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                            var filePath = Path.Combine(_productImageFolder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(stream);
                            }

                            product.Images.Add(new ProductImage { ImagePath = fileName });
                        }
                    }
                    
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error saving product: " + ex.Message);
                }
            }
            return View(product);
        }

        public async Task<IActionResult> AdminDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Reviews)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    TempData["Error"] = "Không tìm thấy sản phẩm";
                    return RedirectToAction(nameof(Index));
                }

                // Xóa các hình ảnh từ thư mục wwwroot
                foreach (var image in product.Images)
                {
                    var imagePath = Path.Combine(_hostEnvironment.WebRootPath, "images", "products", image.ImagePath);
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                // Xóa sản phẩm và các dữ liệu liên quan
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Xóa sản phẩm thành công";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm");
                TempData["Error"] = "Có lỗi xảy ra khi xóa sản phẩm";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // Thêm action confirm delete
        public async Task<IActionResult> DeleteConfirmation(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, List<IFormFile>? newImages, List<int>? imagesToDelete)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Validate discount percentage
                    if (product.DiscountPercentage < 0 || product.DiscountPercentage > 100)
                    {
                        ModelState.AddModelError("DiscountPercentage", "Discount must be between 0 and 100");
                        return View(product);
                    }

                    // Get existing product with images
                    var existingProduct = await _context.Products
                        .Include(p => p.Images)
                        .FirstOrDefaultAsync(p => p.Id == id);

                    if (existingProduct == null)
                    {
                        return NotFound();
                    }

                    // Update basic properties
                    existingProduct.Name = product.Name;
                    existingProduct.Description = product.Description;
                    existingProduct.Price = product.Price;
                    existingProduct.CategoryId = product.CategoryId;
                    existingProduct.DiscountPercentage = product.DiscountPercentage;

                    // Delete selected images
                    if (imagesToDelete != null && imagesToDelete.Any())
                    {
                        string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "images", "products");
                        var imagesToRemove = existingProduct.Images
                            .Where(i => imagesToDelete.Contains(i.Id))
                            .ToList();

                        foreach (var image in imagesToRemove)
                        {
                            var imagePath = Path.Combine(uploadsFolder, image.ImagePath);
                            if (System.IO.File.Exists(imagePath))
                            {
                                System.IO.File.Delete(imagePath);
                            }
                            _context.ProductImages.Remove(image);
                        }
                    }

                    // Add new images
                    if (newImages != null && newImages.Any())
                    {
                        string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "images", "products");
                        
                        foreach (var image in newImages)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(fileStream);
                            }

                            existingProduct.Images.Add(new ProductImage
                            {
                                ImagePath = uniqueFileName,
                                ProductId = product.Id
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Product updated successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating product {ProductId}", id);
                    ModelState.AddModelError("", "Error updating product. Please try again.");
                }
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(product);
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            try
            {
                string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "images", "products");
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(fileStream);
                }

                return uniqueFileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image");
                throw;
            }
        }
    }
} 