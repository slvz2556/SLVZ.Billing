using System.Text.Json;

namespace SLVZ.Billing.Core;

public class PurchaseModel
{
    public string? productId { get; set; }
    public string? purchaseToken { get; set; }
    public JsonElement? state { get; set; }
    public string? payload { get; set; }
    public string? signature { get; set; }
    public string? orderId { get; set; }
    public string? originalJson { get; set; }
    public long? purchaseTime { get; set; }
    public string? type { get; set; }
}

public class ItemModel
{
    public string? productId { get; set; }
    public string? description { get; set; }
    public string? price { get; set; }
    public string? title { get; set; }
    public string? type { get; set; }
}