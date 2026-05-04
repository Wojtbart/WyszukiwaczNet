namespace WyszukiwaczApp.Models;
public class ScrapedItem
{
    public string? Title { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? ImageUrl { get; set; }
    public string? Url { get; set; }
    public string? Location { get; set; }
    public string? AdditionalInfo { get; set; }
    public string? SellerName { get; set; }
    public string? State { get; set; }
    public VehicleDetail? VehicleDetail { get; set; }
    public string? OriginalPrice { get; set; }
    public string? PromoPrice { get; set; }
    public string? FreeDelivery { get; set; }
    public string? Delivery { get; set; }
    public string? Rating { get; set; }
    public string? RatingInStars { get; set; }
    public string? CommentsNumber { get; set; }
    public string? Area { get; set; }
    public string? Rooms { get; set; }
}

public class VehicleDetail
{
    public int? ProductionYear { get; set; }
    public int? Mileage { get; set; }
    public string? FuelType { get; set; }
    public string? Gearbox { get; set; }
    public int? EnginePower { get; set; }
    public string? BodyType { get; set; }
}

public class PlatformData
{
    public int Count { get; set; }
    public List<ScrapedItem>? Output { get; set; }
}

public class DataWrapper
{
    public PlatformData? OlxData { get; set; }
    public PlatformData? AmazonData { get; set; }
    public PlatformData? OtoDomData { get; set; }
    public PlatformData? OtoMotoData { get; set; }
    public PlatformData? AutoscoutData { get; set; }
    public PlatformData? GratkaData { get; set; }
    public PlatformData? SprzedajemyData { get; set; }
    public PlatformData? AutocentrumData { get; set; }
    public PlatformData? SamochodyData { get; set; }
    public PlatformData? PracujData { get; set; }
}

public class GetDataResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DataWrapper? Data { get; set; }
}

public class DataModel
{
    public List<string> websites { get; set; } = new();
    public string phrase { get; set; } = string.Empty;
    public string request_number { get; set; } = "30";
    public string? additional_phrase { get; set; }
}
