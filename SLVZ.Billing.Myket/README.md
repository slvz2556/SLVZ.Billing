# 📦 SLVZ Billing Myket

**SLVZ Billing Myket** is a lightweight and easy-to-use .NET library for handling **in-app purchases via Myket** on Android.  
It simplifies the integration of the Myket payment system for **.NET MAUI** apps without needing to write any Java code directly.

---

## ✨ Features

- Supports both **in-app purchases** and **subscriptions**
- Simple, async-based API
- No extra dependencies required
- Works with .NET MAUI
- Secure and easy to integrate

---

## ⚙️ Installation

```bash
Install-Package SLVZ.Billing.Myket
```

## ⚙️ Usage Example

## 1. Initialize the library
```bash
using SLVZ.Billing.Core;

List<string> products = new() { "coin_pack_1", "subscription_premium" };

Billing.Initialize(this, "YOUR_RSA_KEY_HERE", products);
```
Initialize must be called once when the app starts (e.g., in your main Activity).

## 2. Purchase a product
```bash
using SLVZ.Billing.Myket;

var result = await Billing.Purchase("coin_pack_1" , "Optional payload");

if (result.Status)
    Console.WriteLine($"Purchase successful: {result.Product.productId}");
else
    Console.WriteLine("Purchase failed.");

```

## 3. Purchase a subscription
```bash
using SLVZ.Billing.Myket;

var result = await Billing.Subscription("coin_pack_1" , "Optional payload");

if (result.Status)
    Console.WriteLine("Subscription activated!");
```

## 4. All availible items to purchase 
```bash
using SLVZ.Billing.Myket;

var items = await Billing.AllAvailibleItems();
```
It will return all subs and purchase items with details

## 5. Consume a product (for repeatable items)
```bash
using SLVZ.Billing.Myket;

var result = await Billing.Consume(purchaseModel);

if (result.Status)
    Console.WriteLine($"Item '{result.ProductID}' Consumed");
```

## 6. Data Models (PurchaseModel)
```bash
public class PurchaseModel
{
    public string? productId { get; set; }
    public string? purchaseToken { get; set; }
    public JsonElement? state { get; set; }
    public string? payload { get; set; }
    public string? signature { get; set; }
    public string? orderId { get; set; }
    public string? originalJson { get; set; }
    public string? purchaseTime { get; set; }
    public string? type { get; set; }
}
```

## 7. ItemModel
Contains product details.
```bash
public class ItemModel
{
    public string? productId { get; set; }
    public string? description { get; set; }
    public string? price { get; set; }
    public string? title { get; set; }
    public string? type { get; set; }
}
```


## ⚠️ Notes

- Call **`Billing.Initialize()`** once before using any other methods.  
- All operations are **asynchronous** — make sure to use `await`.  
- If a purchase or subscription fails or is canceled, the returned `Status` will be `false`.  
- Always **validate the purchase signature on your server** to ensure transaction integrity.  
- The **RSA key** must match the one from your Myket developer panel.  
- For consumable items, remember to call **`Consume()`** after a successful purchase.  
- Do **not** call `Initialize()` multiple times — the helper automatically handles initialization state.  
- Make sure to declare all your product IDs when calling `Initialize()`.  
- All callbacks return strongly typed models (`PurchaseModel`, `ItemModel`) for simplicity.

---

👨‍💻 **Author:** [SLVZ](https://slvz.dev/?lang=en)