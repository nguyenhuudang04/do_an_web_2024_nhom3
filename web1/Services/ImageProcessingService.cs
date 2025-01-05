using OpenCvSharp;
using OpenCvSharp.Features2D;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace web1.Services
{
    public class ImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly string _uploadPath;
        private const int MaxFeatures = 100;

        public ImageProcessingService(ILogger<ImageProcessingService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _uploadPath = Path.Combine(env.WebRootPath, "uploads", "search");
            
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }

        public async Task<List<float>> ExtractFeatures(IFormFile imageFile)
        {
            string filePath = string.Empty;
            try
            {
                //  Tạo tên file duy nhất để tránh xung đột khi nhiều người dùng tải file cùng lúc.
                //File ảnh được lưu tạm vào thư mục uploads/search để sử dụng trong xử lý tiếp theo.
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                filePath = Path.Combine(_uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                //Cv2.ImRead: Dùng OpenCV để đọc file ảnh từ đường dẫn.
                using var src = Cv2.ImRead(filePath);
                if (src.Empty())
                {
                    throw new Exception("Cannot read image file");
                }

                // Resize ảnh để tăng tốc độ xử lý
                using var resized = new Mat();
                Cv2.Resize(src, resized, new OpenCvSharp.Size(224, 224));

                // Chuyển sang ảnh xám
                using var gray = new Mat();
                Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);
                //SIFT chỉ cần thông tin cường độ ánh sáng (grayscale) để trích xuất đặc trưng, không cần thông tin màu.
                // Sử dụng SIFT để trích xuất đặc trưng
                using var sift = SIFT.Create(MaxFeatures);
                using var descriptors = new Mat();
                //Tìm các điểm đặc trưng và tính toán vector đặc trưng cho mỗi điểm.
                sift.DetectAndCompute(gray, null, out _, descriptors);

                if (descriptors.Empty())
                {
                    _logger.LogWarning("No features detected in image");
                    return new List<float>();
                }

                var features = new List<float>();
                try
                {
                    // Chuyển đổi ma trận đặc trưng thành danh sách
                    for (int i = 0; i < descriptors.Rows; i++)
                    {
                        for (int j = 0; j < descriptors.Cols; j++)
                        {
                            features.Add(descriptors.At<float>(i, j));
                        }
                    }

                    return features;
                }
                finally
                {
                    descriptors?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting image features");
                throw;
            }
            finally
            {
                // Xóa file tạm
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
} 