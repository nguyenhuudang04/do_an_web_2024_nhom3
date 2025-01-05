using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using web1.Data;
using web1.Models;

namespace web1.Services
{
    public class ImageMatchingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ImageMatchingService> _logger;
        //độ tương đòng
        private const double SimilarityThreshold = 0.6;

        public ImageMatchingService(ApplicationDbContext context, ILogger<ImageMatchingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Product>> FindSimilarProducts(List<float> queryFeatures)
        {
            try
            {
                //Lấy danh sách sản phẩm có đặc trưng:
                var products = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Images)
                    .Include(p => p.ProductFeatures)
                    .Where(p => p.ProductFeatures.Any())
                    .ToListAsync();

                _logger.LogInformation($"Found {products.Count} products with features");

                var similarProducts = new List<(Product Product, double Similarity)>();

                foreach (var product in products)
                {
                    if (product.ProductFeatures?.Any() != true)
                        continue;

                    double maxSimilarity = 0;
                    foreach (var feature in product.ProductFeatures)
                    {
                        //FeatureVector: Chuỗi các số thực được lưu dưới dạng CSV trong cơ sở dữ liệu.
                        //Split và Select: Chuyển đổi chuỗi thành danh sách số thực.
                        var productFeatures = feature.FeatureVector
                            .Split(',')
                            .Select(float.Parse)
                            .ToList();
                        //CalculateCosineSimilarity: Tính độ tương đồng giữa queryFeatures và productFeatures.
                        var similarity = CalculateCosineSimilarity(queryFeatures, productFeatures);
                        if (similarity > maxSimilarity)
                        {
                            maxSimilarity = similarity;
                        }
                    }
                    //Chỉ thêm sản phẩm vào danh sách nếu độ tương đồng lớn hơn hoặc bằng SimilarityThreshold (0.6).
                    if (maxSimilarity >= SimilarityThreshold)
                    {
                        similarProducts.Add((product, maxSimilarity));
                    }
                }

                // Sắp xếp theo độ tương đồng giảm dần và lấy top 10 kết quả
                return similarProducts
                    .OrderByDescending(x => x.Similarity)
                    .Take(10)
                    .Select(x => x.Product)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar products");
                throw;
            }
        }
        //Tính toán độ tương đồng Cosine giữa hai vector đặc trưng.
        private double CalculateCosineSimilarity(List<float> v1, List<float> v2)
        {
            if (v1.Count != v2.Count)
            {
                _logger.LogWarning($"Vector length mismatch: v1={v1.Count}, v2={v2.Count}");
                return 0;
            }


            //công thức Cosine Similarity
            double dotProduct = 0;
            double norm1 = 0;
            double norm2 = 0;

            for (int i = 0; i < v1.Count; i++)
            {
                dotProduct += v1[i] * v2[i];
                norm1 += v1[i] * v1[i];
                norm2 += v2[i] * v2[i];
            }

            norm1 = Math.Sqrt(norm1);
            norm2 = Math.Sqrt(norm2);

            if (norm1 == 0 || norm2 == 0)
            {
                _logger.LogWarning("Zero norm detected");
                return 0;
            }

            var similarity = dotProduct / (norm1 * norm2);
            _logger.LogDebug($"Similarity calculated: {similarity}");
            return similarity;
        }
    }
} 