namespace RealEstateAgencyApp.Models;

public sealed class Client
{
    public int ClientID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ClientType { get; set; } = string.Empty;
}
