namespace RealEstateAgencyApp.Models;

public sealed class Deal
{
    public int DealID { get; set; }
    public int PropertyID { get; set; }
    public int ClientID { get; set; }
    public string DealType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DealDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
