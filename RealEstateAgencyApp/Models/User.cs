namespace RealEstateAgencyApp.Models;

public sealed class User
{
    public int UserID { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
