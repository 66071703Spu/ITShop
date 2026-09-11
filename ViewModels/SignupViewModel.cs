namespace ITShop.ViewModels
{
    public class SignupViewModel
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? InviteCode { get; set; }
        public string? InviterName { get; set; }
        public string? InvitePromotionName { get; set; }

        // ถึงแม้ตารางที่อยู่จะแยกในฐานข้อมูล แต่ field นี้ช่วยให้ฟอร์มสมัครและฟอร์มแอดมินเดิมยังใช้ได้
        public string? Address { get; set; }

        // เตรียมชื่อ property สำรองให้ view ปัจจุบันยังเรียกใช้ได้เหมือนเดิม
        public string Username
        {
            get => Email;
            set => Email = value;
        }

        public string FullName
        {
            get => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";
            set
            {
                var parts = (value ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                FirstName = parts.Length > 0 ? parts[0] : string.Empty;
                LastName = parts.Length > 1 ? parts[1] : string.Empty;
            }
        }

        public string Phone
        {
            get => PhoneNumber ?? string.Empty;
            set => PhoneNumber = value;
        }
    }
}