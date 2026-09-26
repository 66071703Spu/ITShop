using System.Text.Json;
using ITShop.Helpers;
using ITShop.Models;
using ITShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Controllers;

public class CartController : Controller
{
    private readonly Csi402dbContext _db;
    private const decimal ShippingFee = 60m;
    private const string CartSessionKey = "CartItems";
    private const string BuyNowSessionKey = "BuyNowItems";
    private const string CartCouponSessionKey = "CartCouponCode";
    private const string BuyNowCouponSessionKey = "BuyNowCouponCode";
    private static readonly ShippingProviderOptionViewModel[] ShippingProviders =
    {
        new() { Value = "Thailand Post", Label = "Thailand Post", Description = "จัดส่งมาตรฐาน 2-4 วัน" },
        new() { Value = "Flash Express", Label = "Flash Express", Description = "จัดส่งด่วน 1-2 วัน" },
        new() { Value = "Kerry Express", Label = "Kerry Express", Description = "จัดส่งเอกชนพร้อมเลขติดตาม" }
    };

    public CartController(Csi402dbContext db)
    {
        _db = db;
    }

    // แสดงหน้าตะกร้าหลังอัปเดตราคา โปรโมชัน และสต็อกล่าสุดของสินค้า
    public IActionResult Cart()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // รีเฟรชข้อมูลสินค้าใน session cart ให้ตรงกับฐานข้อมูลล่าสุด
        var refreshResult = RefreshCartItemsWithSummary(LoadSessionCart());
        var items = refreshResult.Items;
        SaveSessionCart(items);
        ApplyCartRefreshMessages(refreshResult);

        // ตรวจคูปองที่บันทึกไว้ซ้ำอีกครั้งก่อนส่งหน้า cart ไปแสดง
        var appliedCoupon = ResolveAppliedCoupon(items, userId.Value, false, false);
        ViewBag.ShippingFee = ShippingFee;
        PopulateCouponViewBag(appliedCoupon, items, userId.Value);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // นำคูปองไปใช้กับตะกร้าปกติหรือ flow แบบ Buy Now
    public IActionResult ApplyCoupon(string? couponCode, bool buyNow = false, bool returnToCheckout = false)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // ปรับรูปแบบรหัสคูปองก่อนเพื่อให้ตรวจเทียบได้ตรงกัน
        var normalizedCouponCode = NormalizeCouponCode(couponCode);
        if (normalizedCouponCode == null)
        {
            TempData["CartError"] = "กรุณากรอก Coupon Code";
            return RedirectToCouponTarget(buyNow, returnToCheckout);
        }

        // บันทึกคูปอง รีเฟรชสินค้า แล้วตรวจว่าคูปองยังใช้กับรายการปัจจุบันได้หรือไม่
        SaveCouponCode(normalizedCouponCode, buyNow);
        var refreshResult = buyNow
            ? RefreshCartItemsWithSummary(LoadBuyNowItems())
            : RefreshCartItemsWithSummary(LoadSessionCart());
        var items = refreshResult.Items;

        if (buyNow)
        {
            SaveBuyNowItems(items);
        }
        else
        {
            SaveSessionCart(items);
        }

        ApplyCartRefreshMessages(refreshResult);
        var appliedCoupon = ResolveAppliedCoupon(items, userId.Value, buyNow, true);

        // ถ้าคูปองไม่ผ่านเงื่อนไขกับตะกร้าปัจจุบันให้หยุดและย้อนกลับ
        if (appliedCoupon == null)
        {
            return RedirectToCouponTarget(buyNow, returnToCheckout);
        }

        TempData["CartSuccess"] = $"ใช้คูปอง {appliedCoupon.Code} เรียบร้อยแล้ว";
        return RedirectToCouponTarget(buyNow, returnToCheckout);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบคูปองที่กำลังใช้อยู่ในตะกร้าหรือ Buy Now
    public IActionResult RemoveCoupon(bool buyNow = false, bool returnToCheckout = false)
    {
        ClearCouponCode(buyNow);
        TempData["CartSuccess"] = "ลบคูปองที่ใช้แล้ว";
        return RedirectToCouponTarget(buyNow, returnToCheckout);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // เพิ่มสินค้าลงในตะกร้าที่เก็บไว้ใน session
    public IActionResult AddToCart(int productId, string? productName, decimal price, string? imageUrl, int quantity = 1)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // ใช้ logic กลางในการเพิ่มสินค้าเพื่อให้การเช็คสต็อกเหมือนกันทุกจุด
        var result = AddProductToCartInternal(productId, productName, price, imageUrl, quantity);

        if (!result.Success)
        {
            TempData["CartError"] = result.Message ?? "สินค้านี้หมดชั่วคราว ยังไม่สามารถเพิ่มลงตะกร้าได้";
            return RedirectToAction("ProductDetail", "Product", new { id = productId });
        }

        TempData["CartSuccess"] = result.Message ?? "เพิ่มสินค้าในตะกร้าเรียบร้อยแล้ว";

        return RedirectToAction("Cart");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // เริ่ม flow ซื้อทันทีโดยไม่ยุ่งกับตะกร้าปกติ
    public IActionResult BuyNow(int productId, string? productName, decimal price, string? imageUrl, int quantity = 1)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // ปรับจำนวนที่สั่งให้ไม่เกินสต็อกล่าสุดก่อนสร้างรายการ Buy Now
        var normalizedQuantity = NormalizeQuantityForProduct(productId, quantity);
        if (!normalizedQuantity.HasValue)
        {
            TempData["CartError"] = "สินค้านี้หมดชั่วคราว ยังไม่สามารถสั่งซื้อได้";
            return RedirectToAction("ProductDetail", "Product", new { id = productId });
        }

        if (normalizedQuantity.Value < quantity)
        {
            TempData["CartSuccess"] = $"จำนวนสินค้าที่สั่งได้ถูกปรับเป็น {normalizedQuantity.Value} ตาม stock ที่มีอยู่";
        }

        // โหมดซื้อทันทีจะเก็บข้อมูลสินค้าแยกจากตะกร้าปกติเพื่อไปยืนยันคำสั่งซื้อทันที
        var buyNowItems = new List<CartViewModel>
        {
            CreateCartItem(productId, productName, price, imageUrl, normalizedQuantity.Value, 1)
        };

        SaveBuyNowItems(buyNowItems);
        return RedirectToAction("Checkout", new { buyNow = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // เพิ่มหรือลดจำนวนสินค้าที่อยู่ใน session cart
    public IActionResult UpdateQuantity(int cartItemId, string actionType)
    {
        // ปรับจำนวนเฉพาะใน session ก่อน ยังไม่ตัดสต็อกจริงในฐานข้อมูล
        var items = LoadSessionCart();
        var item = items.FirstOrDefault(c => c.CartItemId == cartItemId);

        if (item != null)
        {
            if (actionType == "increase")
            {
                var maxQuantity = item.Stock.GetValueOrDefault();
                if (maxQuantity > 0)
                {
                    item.Quantity = Math.Min(item.Quantity + 1, maxQuantity);
                }
            }
            else if (actionType == "decrease")
            {
                item.Quantity -= 1;

                if (item.Quantity <= 0)
                {
                    items.Remove(item);
                }
            }

            SaveSessionCart(items);
        }

        return RedirectToAction("Cart");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบรายการสินค้าออกจากตะกร้าใน session
    public IActionResult Remove(int cartItemId)
    {
        var items = LoadSessionCart();
        var item = items.FirstOrDefault(c => c.CartItemId == cartItemId);

        if (item != null)
        {
            items.Remove(item);
            SaveSessionCart(items);
        }

        return RedirectToAction("Cart");
    }

    // เตรียมข้อมูลสำหรับหน้า checkout เช่น ที่อยู่ ขนส่ง และคูปอง
    public IActionResult Checkout(bool buyNow = false)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // รีเฟรชข้อมูลตะกร้าก่อนเข้า checkout เพื่อใช้ราคาและสต็อกล่าสุด
        var refreshResult = buyNow
            ? RefreshCartItemsWithSummary(LoadBuyNowItems())
            : RefreshCartItemsWithSummary(LoadSessionCart());
        var items = refreshResult.Items;
        if (items.Count == 0)
        {
            ApplyCartRefreshMessages(refreshResult);
            TempData["CartError"] = buyNow ? "ไม่พบสินค้าสำหรับ Buy Now" : "ยังไม่มีสินค้าในตะกร้า";
            return RedirectToAction("Cart");
        }

        if (buyNow)
        {
            SaveBuyNowItems(items);
        }
        else
        {
            SaveSessionCart(items);
        }

        ApplyCartRefreshMessages(refreshResult);

        // โหลดที่อยู่และตัวเลือกขนส่งสำหรับใช้ในฟอร์มยืนยันการสั่งซื้อ
        var userAddresses = _db.Addresses
            .Where(a => a.UserId == userId.Value)
            .OrderByDescending(a => a.IsDefault == true)
            .ThenByDescending(a => a.AddressId)
            .ToList();

        ViewBag.IsBuyNow = buyNow;
        ViewBag.AvailableAddresses = userAddresses
            .Select(a => new CheckoutAddressOptionViewModel
            {
                AddressId = a.AddressId,
                AddressLine = a.AddressLine ?? string.Empty,
                FullAddress = BuildAddressText(a),
                IsDefault = a.IsDefault == true
            })
            .ToList();
        ViewBag.SelectedAddressId = userAddresses.FirstOrDefault()?.AddressId;
        ViewBag.ShippingProviders = ShippingProviders.ToList();
        ViewBag.SelectedShippingProvider = ShippingProviders.First().Value;
        ViewBag.ShippingFee = ShippingFee;

        // เตรียมข้อมูลคูปองที่ใช้อยู่และคูปองอื่นที่ยังใช้ได้กับรายการนี้
        PopulateCouponViewBag(ResolveAppliedCoupon(items, userId.Value, buyNow, false), items, userId.Value);

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // แปลงข้อมูลในตะกร้าให้กลายเป็น order payment shipment และประวัติคลังสินค้า
    public IActionResult PlaceOrder(string paymentMethod, int? addressId, string? shippingProvider, bool buyNow = false)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // รีเฟรชตะกร้าอีกครั้งเพื่อให้ใช้ข้อมูลล่าสุดตอนสร้างคำสั่งซื้อ
        var refreshResult = buyNow
            ? RefreshCartItemsWithSummary(LoadBuyNowItems())
            : RefreshCartItemsWithSummary(LoadSessionCart());
        var cartItems = refreshResult.Items;

        if (buyNow)
        {
            SaveBuyNowItems(cartItems);
        }
        else
        {
            SaveSessionCart(cartItems);
        }

        if (refreshResult.RemovedUnavailableCount > 0 || refreshResult.AdjustedQuantityCount > 0)
        {
            ApplyCartRefreshMessages(refreshResult);
            TempData["CartError"] = "มีสินค้าบางรายการในตะกร้าถูกปรับตาม stock ล่าสุด กรุณาตรวจสอบก่อนยืนยันคำสั่งซื้ออีกครั้ง";
            return RedirectToAction("Checkout", new { buyNow });
        }

        // ตรวจว่าที่อยู่จัดส่งที่เลือกยังเป็นของผู้ใช้คนปัจจุบันอยู่จริง
        if (cartItems.Count == 0)
        {
            TempData["CartError"] = buyNow ? "ไม่พบสินค้าสำหรับ Buy Now" : "ยังไม่มีสินค้าในตะกร้า";
            return RedirectToAction("Cart");
        }

        var shippingAddress = _db.Addresses
            .FirstOrDefault(a => a.AddressId == addressId && a.UserId == userId.Value);
        if (shippingAddress == null)
        {
            TempData["CartError"] = "กรุณาเลือกที่อยู่จัดส่งก่อนยืนยันคำสั่งซื้อ";
            return RedirectToAction("Checkout", new { buyNow });
        }

        var normalizedShippingProvider = NormalizeShippingProvider(shippingProvider);
        if (paymentMethod != "Cash on Delivery" && paymentMethod != "Bank Transfer")
        {
            TempData["CartError"] = "วิธีชำระเงินไม่ถูกต้อง กรุณาเลือกใหม่";
            return RedirectToAction("Checkout", new { buyNow });
        }

        // เช็คว่าเป็นคำสั่งซื้อแรกของผู้ใช้หรือไม่เพื่อใช้กับระบบ invite reward
        var hadPreviousOrders = _db.Orders.Any(o => o.UserId == userId.Value);

        // ตรวจคูปองซ้ำอีกครั้งกับตะกร้ารอบสุดท้ายก่อนสร้าง order
        var appliedCoupon = ResolveAppliedCoupon(cartItems, userId.Value, buyNow, true);
        if (GetCouponCode(buyNow) != null && appliedCoupon == null)
        {
            return RedirectToAction("Checkout", new { buyNow });
        }

        // แยกยอดรวมออกเป็นยอดเดิม ส่วนลดอัตโนมัติ ส่วนลดคูปอง และค่าจัดส่ง
        var originalSubtotal = cartItems.Sum(ci => ci.OriginalSubTotal);
        var autoDiscountAmount = cartItems.Sum(ci => ci.DiscountAmount * ci.Quantity);
        var couponDiscountAmount = appliedCoupon?.DiscountAmount ?? 0m;
        var discountAmount = autoDiscountAmount + couponDiscountAmount;
        var finalTotal = originalSubtotal - discountAmount + ShippingFee;

        // ใช้ transaction ครอบการตัดสต็อกและสร้าง order เพื่อให้ข้อมูลสอดคล้องกัน
        using var transaction = _db.Database.BeginTransaction();
        var componentStockChanges = new List<(int ProductId, int Quantity)>();

        foreach (var item in cartItems)
        {
            if (!item.ProductId.HasValue || item.ProductId.Value <= 0)
            {
                continue;
            }

            var updatedRows = _db.Database.ExecuteSqlInterpolated($@"
                UPDATE products
                SET stock = stock - {item.Quantity}
                WHERE product_id = {item.ProductId.Value}
                  AND stock IS NOT NULL
                  AND stock >= {item.Quantity};");

            if (updatedRows == 0)
            {
                transaction.Rollback();
                TempData["CartError"] = $"สินค้า {item.ProductName} มี stock ไม่เพียงพอแล้ว กรุณาตรวจสอบตะกร้าอีกครั้ง";
                return RedirectToAction("Checkout", new { buyNow });
            }

            var orderedProduct = _db.Products
                .Include(product => product.Category)
                .First(product => product.ProductId == item.ProductId.Value);
            if (PackageAvailabilityHelper.IsComset(orderedProduct))
            {
                var package = PackageAvailabilityHelper.FindPackage(_db, orderedProduct);
                if (package == null || PackageAvailabilityHelper.GetAvailableStock(orderedProduct, package) < item.Quantity)
                {
                    transaction.Rollback();
                    TempData["CartError"] = $"ชิ้นส่วนในเซต {item.ProductName} ไม่พร้อมขายแล้ว";
                    return RedirectToAction("Checkout", new { buyNow });
                }

                foreach (var component in package.PackageItems)
                {
                    var quantity = item.Quantity * component.Quantity!.Value;
                    var componentRows = _db.Database.ExecuteSqlInterpolated($@"
                        UPDATE products SET stock = stock - {quantity}
                        WHERE product_id = {component.ProductId!.Value} AND stock >= {quantity};");
                    if (componentRows != 1)
                    {
                        transaction.Rollback();
                        TempData["CartError"] = $"ชิ้นส่วนในเซต {item.ProductName} มี stock ไม่เพียงพอแล้ว";
                        return RedirectToAction("Checkout", new { buyNow });
                    }

                    componentStockChanges.Add((component.ProductId.Value, quantity));
                }
            }
        }

        // สร้างข้อมูล order หลักก่อนเพื่อให้ตารางอื่นใช้อ้างอิง order id ได้
        var order = new Order
        {
            UserId = userId.Value,
            // A temporary unique value lets the database assign the order ID first.
            OrderNumber = $"TMP-{Guid.NewGuid():N}",
            TotalAmount = originalSubtotal,
            DiscountAmount = discountAmount,
            ShippingFee = ShippingFee,
            FinalAmount = finalTotal,
            Status = "pending",
            CreatedAt = DateTime.Now
        };

        _db.Orders.Add(order);
        _db.SaveChanges();
        order.OrderNumber = $"ORD{DateTime.Now:yyyyMMddHHmmss}-{order.OrderId:D6}";

        // บันทึกประวัติสถานะ order และข้อมูลการจัดส่งสำหรับติดตามภายหลัง
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.OrderId,
            Status = order.Status,
            ChangedAt = DateTime.Now
        });

        _db.Shipments.Add(new Shipment
        {
            OrderId = order.OrderId,
            AddressId = shippingAddress.AddressId,
            ShippingProvider = normalizedShippingProvider,
            TrackingNumber = null,
            Status = "pending",
            CreatedAt = DateTime.Now
        });

        // สร้างรายการสินค้าที่ซื้อและบันทึกประวัติการตัดสต็อกของแต่ละชิ้น
        foreach (var item in cartItems)
        {
            if (!item.ProductId.HasValue || item.ProductId.Value <= 0)
            {
                continue;
            }

            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = item.ProductId.Value,
                Quantity = item.Quantity,
                UnitPrice = item.Price,
                TotalPrice = item.SubTotal
            });

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = item.ProductId.Value,
                TransactionType = "OUT",
                Quantity = item.Quantity,
                ReferenceOrderId = order.OrderId,
                CreatedAt = DateTime.Now
            });
        }

        foreach (var change in componentStockChanges)
        {
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = change.ProductId,
                TransactionType = "OUT",
                Quantity = change.Quantity,
                ReferenceOrderId = order.OrderId,
                CreatedAt = DateTime.Now
            });
        }

        // บันทึกการใช้คูปองเมื่อ order พร้อมจะถูกบันทึกจริงแล้วเท่านั้น
        if (appliedCoupon != null)
        {
            _db.CouponRedemptions.Add(new CouponRedemption
            {
                CouponId = appliedCoupon.CouponId,
                UserId = userId.Value,
                OrderId = order.OrderId,
                UsedAt = DateTime.Now
            });
        }

        // บันทึกข้อมูลการชำระเงินไว้ใช้ในหน้าประวัติคำสั่งซื้อ
        _db.Payments.Add(new Payment
        {
            OrderId = order.OrderId,
            PaymentMethod = paymentMethod,
            PaymentStatus = "pending",
            PaidAt = null
        });

        // แจก reward ให้ผู้เชิญเฉพาะกรณีที่ผู้ถูกเชิญเพิ่งสั่งซื้อครั้งแรก
        string? rewardCouponCode = null;
        if (!hadPreviousOrders)
        {
            rewardCouponCode = InvitePromotionHelper.GrantInviteRewardForFirstOrder(_db, userId.Value, DateTime.Now);
        }

        _db.SaveChanges();
        transaction.Commit();

        // ล้างข้อมูล checkout ที่ทำรายการเสร็จแล้วเพื่อเริ่มรอบใหม่ในครั้งถัดไป
        if (buyNow)
        {
            SaveBuyNowItems(new List<CartViewModel>());
            ClearCouponCode(true);
        }
        else
        {
            SaveSessionCart(new List<CartViewModel>());
            ClearCouponCode(false);
        }

        TempData["OrderNumber"] = order.OrderNumber;
        if (!string.IsNullOrWhiteSpace(rewardCouponCode))
        {
            TempData["CartSuccess"] = $"คำสั่งซื้อแรกสำเร็จแล้ว ระบบสร้าง reward coupon ให้ผู้เชิญเรียบร้อย ({rewardCouponCode})";
        }
        return RedirectToAction("OrderSuccess");
    }

    // เปิดหน้าสำเร็จหลังสั่งซื้อเสร็จ
    public IActionResult OrderSuccess()
    {
        return View();
    }

    // โหลดข้อมูลตะกร้าปกติจาก session
    private List<CartViewModel> LoadSessionCart()
    {
        var cartJson = HttpContext.Session.GetString(CartSessionKey);
        if (string.IsNullOrWhiteSpace(cartJson))
        {
            return new List<CartViewModel>();
        }

        return JsonSerializer.Deserialize<List<CartViewModel>>(cartJson) ?? new List<CartViewModel>();
    }

    // บันทึกข้อมูลตะกร้าปกติกลับลง session
    private void SaveSessionCart(List<CartViewModel> items)
    {
        HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(items));
    }

    // โหลดข้อมูล Buy Now ชั่วคราวจาก session
    private List<CartViewModel> LoadBuyNowItems()
    {
        var cartJson = HttpContext.Session.GetString(BuyNowSessionKey);
        if (string.IsNullOrWhiteSpace(cartJson))
        {
            return new List<CartViewModel>();
        }

        return JsonSerializer.Deserialize<List<CartViewModel>>(cartJson) ?? new List<CartViewModel>();
    }

    // บันทึกข้อมูล Buy Now ชั่วคราวกลับลง session
    private void SaveBuyNowItems(List<CartViewModel> items)
    {
        HttpContext.Session.SetString(BuyNowSessionKey, JsonSerializer.Serialize(items));
    }

    // ส่งข้อความแจ้งเตือนเมื่อมีสินค้าถูกลบหรือปรับจำนวนระหว่างรีเฟรชตะกร้า
    private void ApplyCartRefreshMessages(RefreshCartResult refreshResult)
    {
        if (refreshResult.RemovedUnavailableCount > 0)
        {
            AppendTempDataMessage("CartError",
                $"มีสินค้า {refreshResult.RemovedUnavailableCount} รายการถูกนำออกจากตะกร้าเพราะหมด stock หรือไม่พร้อมขาย");
        }

        if (refreshResult.AdjustedQuantityCount > 0)
        {
            AppendTempDataMessage("CartSuccess",
                $"มีสินค้า {refreshResult.AdjustedQuantityCount} รายการถูกปรับจำนวนตาม stock ล่าสุด");
        }
    }

    // ต่อข้อความเพิ่มใน TempData เดิมโดยไม่ทับข้อความเก่า
    private void AppendTempDataMessage(string key, string message)
    {
        var existing = TempData[key] as string;
        TempData[key] = string.IsNullOrWhiteSpace(existing)
            ? message
            : $"{existing} {message}";
    }

    // ทำงานเพิ่มสินค้าลงตะกร้าด้วย logic กลางที่ใช้ร่วมกัน
    private AddToCartResult AddProductToCartInternal(int productId, string? productName, decimal price, string? imageUrl, int quantity)
    {
        var normalizedQuantity = NormalizeQuantityForProduct(productId, quantity);
        if (!normalizedQuantity.HasValue)
        {
            return new AddToCartResult(false, "สินค้านี้หมดชั่วคราว ยังไม่สามารถเพิ่มลงตะกร้าได้");
        }

        var items = LoadSessionCart();
        var existingItem = items.FirstOrDefault(i => i.ProductId == productId);
        var targetQuantity = normalizedQuantity.Value;

        if (existingItem == null)
        {
            items.Add(CreateCartItem(productId, productName, price, imageUrl, targetQuantity,
                items.Count == 0 ? 1 : items.Max(i => i.CartItemId) + 1));

            SaveSessionCart(items);
            return targetQuantity < quantity
                ? new AddToCartResult(true, $"เพิ่มสินค้าในตะกร้าโดยปรับจำนวนเป็น {targetQuantity} ตาม stock ที่มีอยู่")
                : new AddToCartResult(true, "เพิ่มสินค้าในตะกร้าเรียบร้อยแล้ว");
        }

        var stockLimit = NormalizeQuantityForProduct(productId, int.MaxValue) ?? targetQuantity;
        var requestedTotal = existingItem.Quantity + targetQuantity;
        var adjustedTotal = Math.Min(requestedTotal, stockLimit);

        if (adjustedTotal <= existingItem.Quantity)
        {
            SaveSessionCart(items);
            return new AddToCartResult(false, $"สินค้าในตะกร้ามีครบตาม stock แล้ว ({existingItem.Quantity} ชิ้น)");
        }

        existingItem.Quantity = adjustedTotal;
        SaveSessionCart(items);

        return adjustedTotal < requestedTotal
            ? new AddToCartResult(true, $"เพิ่มสินค้าในตะกร้าโดยปรับจำนวนรวมเป็น {adjustedTotal} ตาม stock ที่มีอยู่")
            : new AddToCartResult(true, "เพิ่มสินค้าในตะกร้าเรียบร้อยแล้ว");
    }

    // สร้าง snapshot ของสินค้าในตะกร้าจากข้อมูลล่าสุดในฐานข้อมูล
    private CartViewModel CreateCartItem(int productId, string? productName, decimal price, string? imageUrl, int quantity, int cartItemId)
    {
        var dbProduct = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.ProductImages)
            .Include(p => p.Promotions)
            .FirstOrDefault(p => p.ProductId == productId);

        var promotionPrice = dbProduct != null
            ? PromotionPriceCalculator.Calculate(dbProduct)
            : null;

        return new CartViewModel
        {
            CartItemId = cartItemId,
            ProductId = productId,
            ProductName = dbProduct?.Name ?? (productName ?? "สินค้า"),
            BrandName = dbProduct?.Brand?.BrandName,
            Price = promotionPrice?.FinalPrice ?? dbProduct?.Price ?? price,
            Quantity = quantity,
            Stock = dbProduct?.Stock,
            OriginalPrice = promotionPrice?.OriginalPrice ?? dbProduct?.Price ?? price,
            DiscountAmount = promotionPrice?.DiscountAmount ?? 0m,
            HasAutoAppliedPromotion = promotionPrice?.HasDiscount ?? false,
            ActivePromotionName = promotionPrice?.PromotionName,
            DiscountLabel = promotionPrice?.DiscountLabel,
            ImageUrl = dbProduct?.ProductImages
                .OrderByDescending(i => i.IsMain == true)
                .Select(i => i.ImageUrl)
                .FirstOrDefault() ?? imageUrl ?? "https://placehold.co/100x100?text=No+Image"
        };
    }

    // ตรวจรายการสินค้าในตะกร้ากับฐานข้อมูลอีกครั้งแล้วสรุปสิ่งที่ถูกลบหรือถูกปรับ
    private RefreshCartResult RefreshCartItemsWithSummary(List<CartViewModel> items)
    {
        if (items.Count == 0)
        {
            return new RefreshCartResult(items, 0, 0);
        }

        var productIds = items
            .Where(i => i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .Distinct()
            .ToList();

        var products = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.Promotions)
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionary(p => p.ProductId);

        var refreshedItems = new List<CartViewModel>();
        var removedUnavailableCount = 0;
        var adjustedQuantityCount = 0;

        foreach (var item in items)
        {
            if (!item.ProductId.HasValue || !products.TryGetValue(item.ProductId.Value, out var product))
            {
                removedUnavailableCount += 1;
                continue;
            }

            var stock = PackageAvailabilityHelper.GetAvailableStock(
                product, PackageAvailabilityHelper.FindPackage(_db, product));
            if (stock <= 0)
            {
                removedUnavailableCount += 1;
                continue;
            }

            var promotionPrice = PromotionPriceCalculator.Calculate(product);
            var normalizedQuantity = Math.Min(Math.Max(item.Quantity, 1), stock);
            if (normalizedQuantity != item.Quantity)
            {
                adjustedQuantityCount += 1;
            }

            item.ProductName = product.Name;
            item.BrandName = product.Brand?.BrandName;
            item.Price = promotionPrice.FinalPrice;
            item.OriginalPrice = promotionPrice.OriginalPrice;
            item.DiscountAmount = promotionPrice.DiscountAmount;
            item.HasAutoAppliedPromotion = promotionPrice.HasDiscount;
            item.ActivePromotionName = promotionPrice.PromotionName;
            item.DiscountLabel = promotionPrice.DiscountLabel;
            item.Stock = stock;
            item.Quantity = normalizedQuantity;
            item.ImageUrl = product.ProductImages
                .OrderByDescending(i => i.IsMain == true)
                .Select(i => i.ImageUrl)
                .FirstOrDefault() ?? item.ImageUrl;

            refreshedItems.Add(item);
        }

        return new RefreshCartResult(refreshedItems, removedUnavailableCount, adjustedQuantityCount);
    }

    // ปรับจำนวนที่ขอให้สอดคล้องกับสต็อกของสินค้าปัจจุบัน
    private int? NormalizeQuantityForProduct(int productId, int quantity)
    {
        var product = _db.Products
            .Include(p => p.Category)
            .FirstOrDefault(p => p.ProductId == productId);
        var availableStock = product == null || string.Equals(product.Status, "inactive", StringComparison.OrdinalIgnoreCase)
            ? 0
            : PackageAvailabilityHelper.GetAvailableStock(product, PackageAvailabilityHelper.FindPackage(_db, product));
        if (availableStock <= 0)
        {
            return null;
        }

        return Math.Min(Math.Max(quantity, 1), availableStock);
    }

    private sealed record AddToCartResult(bool Success, string? Message);
    private sealed record RefreshCartResult(List<CartViewModel> Items, int RemovedUnavailableCount, int AdjustedQuantityCount);

    // แปลงคูปองที่เก็บใน session ให้เป็นสถานะคูปองที่ผ่านการตรวจสอบแล้วสำหรับตะกร้าปัจจุบัน
    private AppliedCouponState? ResolveAppliedCoupon(List<CartViewModel> items, int userId, bool buyNow, bool setTempDataError)
    {
        var couponCode = GetCouponCode(buyNow);
        if (couponCode == null)
        {
            return null;
        }

        var couponPromotion = _db.Promotions
            .Include(p => p.Coupon)
                .ThenInclude(c => c!.CouponRedemptions)
            .Include(p => p.Products)
            .FirstOrDefault(p =>
                p.CouponId.HasValue
                && p.Coupon != null
                && p.Coupon.Code != null
                && p.Coupon.Code.ToLower() == couponCode.ToLower()
                && (p.PromotionType ?? string.Empty).ToLower() == "coupon");

        if (couponPromotion?.Coupon == null)
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "ไม่พบคูปองนี้ในระบบ");
            return null;
        }

        var now = DateTime.Now;
        if ((couponPromotion.StartDate.HasValue && couponPromotion.StartDate > now)
            || (couponPromotion.EndDate.HasValue && couponPromotion.EndDate < now))
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "คูปองนี้ยังไม่อยู่ในช่วงเวลาใช้งาน");
            return null;
        }

        var coupon = couponPromotion.Coupon;
        if (coupon.OwnerUserId.HasValue && coupon.OwnerUserId.Value != userId)
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "คูปองนี้ไม่สามารถใช้กับบัญชีนี้ได้");
            return null;
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate < now)
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "คูปองนี้หมดอายุแล้ว");
            return null;
        }

        if (coupon.UsageLimit.HasValue && coupon.CouponRedemptions.Count >= coupon.UsageLimit.Value)
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "คูปองนี้ถูกใช้ครบจำนวนแล้ว");
            return null;
        }

        if (coupon.CouponRedemptions.Any(redemption => redemption.UserId == userId))
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "บัญชีนี้เคยใช้คูปองนี้ไปแล้ว");
            return null;
        }

        var eligibleProductIds = couponPromotion.Products
            .Select(product => product.ProductId)
            .ToHashSet();
        var eligibleSubtotal = items
            .Where(item => !item.ProductId.HasValue || eligibleProductIds.Count == 0 || eligibleProductIds.Contains(item.ProductId.Value))
            .Sum(item => item.SubTotal);

        if (couponPromotion.MinimumOrderSubtotal.HasValue && eligibleSubtotal < couponPromotion.MinimumOrderSubtotal.Value)
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, $"ยอดสินค้าที่ร่วมรายการต้องถึง {couponPromotion.MinimumOrderSubtotal.Value:N2} บาท");
            return null;
        }

        if (eligibleSubtotal <= 0)
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "คูปองนี้ยังไม่มีสินค้าที่เข้าเงื่อนไขในตะกร้า");
            return null;
        }

        var discountAmount = PromotionPriceCalculator.CalculateDiscountAmount(
            eligibleSubtotal,
            couponPromotion.DiscountType,
            couponPromotion.DiscountValue);

        if (discountAmount <= 0)
        {
            ClearCouponCode(buyNow);
            SetCouponError(setTempDataError, "คูปองนี้ไม่สามารถใช้กับตะกร้าปัจจุบันได้");
            return null;
        }

        return new AppliedCouponState(
            coupon.CouponId,
            coupon.Code ?? couponCode,
            couponPromotion.Name ?? "Coupon Promotion",
            discountAmount,
            eligibleSubtotal,
            couponPromotion.DiscountType,
            couponPromotion.DiscountValue,
            coupon.UsageLimit,
            coupon.CouponRedemptions.Count);
    }

    // อ่านคูปองที่บันทึกไว้ตาม flow ที่กำลังใช้งาน
    private string? GetCouponCode(bool buyNow)
    {
        return HttpContext.Session.GetString(buyNow ? BuyNowCouponSessionKey : CartCouponSessionKey);
    }

    // บันทึกรหัสคูปองให้กับตะกร้าปกติหรือ Buy Now
    private void SaveCouponCode(string couponCode, bool buyNow)
    {
        HttpContext.Session.SetString(buyNow ? BuyNowCouponSessionKey : CartCouponSessionKey, couponCode);
    }

    // ลบรหัสคูปองที่บันทึกไว้ของ flow ที่เลือก
    private void ClearCouponCode(bool buyNow)
    {
        HttpContext.Session.Remove(buyNow ? BuyNowCouponSessionKey : CartCouponSessionKey);
    }

    // เลือกหน้าปลายทางที่จะ redirect หลังจัดการคูปองเสร็จ
    private IActionResult RedirectToCouponTarget(bool buyNow, bool returnToCheckout)
    {
        return returnToCheckout
            ? RedirectToAction("Checkout", new { buyNow })
            : RedirectToAction("Cart");
    }

    // แปลงที่อยู่จัดส่งให้อยู่ในรูปแบบข้อความสั้นสำหรับแสดงผล
    private static string BuildAddressText(Address address)
    {
        return string.IsNullOrWhiteSpace(address.AddressLine) ? "-" : address.AddressLine.Trim();
    }

    // ปรับชื่อผู้ให้บริการขนส่งให้เป็นค่ามาตรฐานที่ระบบรองรับ
    private static string NormalizeShippingProvider(string? shippingProvider)
    {
        var selectedProvider = ShippingProviders.FirstOrDefault(provider =>
            string.Equals(provider.Value, shippingProvider, StringComparison.OrdinalIgnoreCase));

        return selectedProvider?.Value ?? ShippingProviders[0].Value;
    }

    // ปรับรูปแบบข้อความคูปองให้พร้อมสำหรับนำไปเปรียบเทียบ
    private static string? NormalizeCouponCode(string? couponCode)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return null;
        }

        return couponCode.Trim().ToUpperInvariant();
    }

    // ส่งข้อความ error ของคูปองเข้า TempData เมื่อ caller ต้องการให้แสดง
    private void SetCouponError(bool shouldSetTempData, string message)
    {
        if (shouldSetTempData)
        {
            TempData["CartError"] = message;
        }
    }

    // เตรียมข้อมูลคูปองสำหรับใช้แสดงในหน้า cart และ checkout
    private void PopulateCouponViewBag(AppliedCouponState? appliedCoupon, List<CartViewModel> items, int userId)
    {
        ViewBag.AppliedCouponCode = appliedCoupon?.Code;
        ViewBag.AppliedCouponPromotionName = appliedCoupon?.PromotionName;
        ViewBag.AppliedCouponDiscountAmount = appliedCoupon?.DiscountAmount ?? 0m;
        ViewBag.AppliedCouponEligibleSubtotal = appliedCoupon?.EligibleSubtotal ?? 0m;
        ViewBag.AvailableCoupons = CouponPromotionHelper.GetVisibleCoupons(
            _db,
            userId,
            items
                .Where(item => item.ProductId.HasValue)
                .Select(item => new CouponContextItemViewModel
                {
                    ProductId = item.ProductId!.Value,
                    Subtotal = item.SubTotal
                }))
            .Where(coupon => !string.Equals(coupon.Code, appliedCoupon?.Code, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private sealed record AppliedCouponState(
        int CouponId,
        string Code,
        string PromotionName,
        decimal DiscountAmount,
        decimal EligibleSubtotal,
        string? DiscountType,
        decimal? DiscountValue,
        int? UsageLimit,
        int UsedCount);
}
