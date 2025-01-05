using System.ComponentModel.DataAnnotations;

namespace web1.Models
{
    public class ProductFeature
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string FeatureVector { get; set; } // Tên thuộc tính là FeatureVector, không phải Features
        public Product Product { get; set; }
    }
} 