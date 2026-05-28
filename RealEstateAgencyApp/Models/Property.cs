namespace RealEstateAgencyApp.Models;

public sealed class Property
{
    public int PropertyID { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public double Area { get; set; }
    public int Rooms { get; set; }
    public int Floor { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OwnerID { get; set; }
}
