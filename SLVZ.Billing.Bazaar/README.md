# 📦 SLVZ Billing Bazaar

**SLVZ Billing Bazaar** is a lightweight and easy-to-use .NET library for handling **in-app purchases via Cafe Bazaar** on Android.
It binds the [Poolakey](https://github.com/cafebazaar/Poolakey) SDK directly, so you can use Cafe Bazaar billing in **.NET MAUI** apps with a clean async C# API and without writing any Java or Kotlin code.

---

## ✨ Features

- Supports both **in-app purchases** and **subscriptions**
- Simple, `async`/`await` based API with strongly typed models
- Free trial check for subscriptions (`CheckTrialSubscription`)
- **Automatic reconnect**: if the connection to Bazaar is lost, the next call reconnects by itself
- No extra setup: all required dependencies come in through NuGet
- Works with .NET MAUI

---

## ⚙️ Installation

```bash
dotnet add package SLVZ.Billing.Bazaar
```

or from the Package Manager Console:

```bash
Install-Package SLVZ.Billing.Bazaar
```

> The Cafe Bazaar app must be installed on the device for billing to work.

---

## 🚀 Usage

### 1. Initialize the library

```csharp
using SLVZ.Billing;

List<string> products = new() { "coin_pack_1", "subscription_premium" };

bool connected = await InAppBilling.Initialize(this, "YOUR_RSA_KEY_HERE", products);
```

- Call it when the app starts, with your main **Activity** (for MAUI, `MainActivity` which is an `AppCompatActivity`).
- It returns `true` once the connection to Bazaar is established, `false` if it fails (for example Bazaar is not installed).
- Calling it again (for example in `OnCreate`) is safe: it does not create a second connection, it only refreshes the Activity used for the purchase screen.
- The Activity is required for `Purchase` and `Subscription`, because Bazaar's payment screen is launched from it.

### 2. Purchase a product

```csharp
var result = await InAppBilling.Purchase("coin_pack_1", "Optional payload");

if (result.Status)
    Console.WriteLine($"Purchase successful: {result.Product.productId}");
else
    Console.WriteLine("Purchase failed.");
```

For consumable items you can consume the product right after a successful purchase:

```csharp
var result = await InAppBilling.Purchase("coin_pack_1", "Optional payload", consumeAfter: true);
```

### 3. Purchase a subscription

```csharp
var result = await InAppBilling.Subscription("subscription_premium", "Optional payload");

if (result.Status)
    Console.WriteLine("Subscription activated!");
```

### 4. Get all available items (with details)

```csharp
var items = await InAppBilling.AllInappItems();
var subs  = await InAppBilling.AllSubItems();

if (items.Status)
    foreach (var item in items.Products)
        Console.WriteLine($"{item.title}: {item.price}");
```

The list is built from the product IDs you passed to `Initialize()`.

### 5. Consume a product (for repeatable items)

```csharp
var result = await InAppBilling.Consume(purchaseModel);

if (result.Status)
    Console.WriteLine($"Item '{result.ProductID}' consumed");
```

### 6. Check the user's existing purchases and subscriptions

```csharp
var purchases = await InAppBilling.CheckPurchases();      // purchased and not yet consumed
var subscriptions = await InAppBilling.CheckSubscriptions(); // active subscriptions

if (subscriptions.Status && subscriptions.Products.Count > 0)
    Console.WriteLine("User has an active subscription");
```

### 7. Check free trial availability

```csharp
var trial = await InAppBilling.CheckTrialSubscription();

if (trial.Status && trial.IsAvailable)
    Console.WriteLine($"Free trial available: {trial.TrialPeriodDays} days");
```

`Status == false` means the check itself failed, so `IsAvailable` should not be trusted in that case.

---

## 🧩 Data Models

### PurchaseModel

```csharp
public class PurchaseModel
{
    public string? productId { get; set; }
    public string? purchaseToken { get; set; }
    public JsonElement? state { get; set; }   // purchase state name, e.g. "PURCHASED"
    public string? payload { get; set; }
    public string? signature { get; set; }
    public string? orderId { get; set; }
    public string? originalJson { get; set; }
    public long? purchaseTime { get; set; }
    public string? type { get; set; }         // "inapp" or "subs"
}
```

### ItemModel

Contains product details.

```csharp
public class ItemModel
{
    public string? productId { get; set; }
    public string? description { get; set; }
    public string? price { get; set; }
    public string? title { get; set; }
    public string? type { get; set; }
}
```

---

## ⚠️ Notes

- Call **`InAppBilling.Initialize()`** once at app start, before using any other method. If the connection to Bazaar drops later, the other methods reconnect automatically using the context and RSA key you gave to `Initialize()`.
- All operations are **asynchronous**: make sure to use `await`.
- If an operation fails or the user cancels, the returned `Status` is `false` (and the model or list is empty).
- Always **validate the purchase signature on your server** to ensure transaction integrity.
- The **RSA key** must match the one from your Bazaar developer panel.
- For consumable items, call **`Consume()`** after a successful purchase (or use `consumeAfter: true`), otherwise the user cannot buy the item again.
- Make sure to declare all your product IDs when calling `Initialize()`, since `AllInappItems()` and `AllSubItems()` use that list.
- All results are returned as strongly typed models (`PurchaseModel`, `ItemModel`) for simplicity.

---

👨‍💻 **Author:** [SLVZ](https://slvz.dev/?lang=en)
