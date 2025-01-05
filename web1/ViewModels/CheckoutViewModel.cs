using web1.Models;

namespace web1.ViewModels
{
    public class CheckoutViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public List<CartItem> CartItems { get; set; }
        public decimal TotalAmount { get; set; }
    }
} 