using System.Net.Mail;
using System.Net;
namespace web1.Services
{
    public interface IEmailService
    {
        Task SendOrderConfirmationEmailAsync(string toEmail, string orderNumber, decimal totalAmount);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOrderConfirmationEmailAsync(string toEmail, string orderNumber, decimal totalAmount)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var fromEmail = smtpSettings["FromEmail"];
            var smtpHost = smtpSettings["Host"];
            var smtpPort = int.Parse(smtpSettings["Port"]);
            var smtpUsername = smtpSettings["Username"];
            var smtpPassword = smtpSettings["Password"];

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail),
                Subject = "Xác nhận đơn hàng thành công",
                Body = $@"<html>
                         <body>
                            <h2>Cảm ơn bạn đã đặt hàng!</h2>
                            <p>Đơn hàng của bạn đã được xác nhận:</p>
                            <p>Mã đơn hàng: {orderNumber}</p>
                            <p>Tổng tiền: {totalAmount:C}</p>
                            <p>Chúng tôi sẽ xử lý đơn hàng của bạn trong thời gian sớm nhất.</p>
                         </body>
                         </html>",
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using (var client = new SmtpClient(smtpHost, smtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                await client.SendMailAsync(message);
            }
        }
    }
}
