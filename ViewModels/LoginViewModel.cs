namespace ITShop.ViewModels
{

    public class LoginViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // เตรียมชื่อ property สำรองให้ตรงกับป้ายชื่อและฟิลด์ของ UI ปัจจุบัน
        public string Username
        {
            get => Email;
            set => Email = value;
        }
    }
}