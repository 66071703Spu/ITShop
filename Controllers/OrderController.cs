using ITShop.Models;
using ITShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Controllers;

public class OrderController : Controller
{
    private readonly Csi402dbContext _db;

    public OrderController(Csi402dbContext db)
    {
        _db = db;
    }

    // แสดงประวัติคำสั่งซื้อของผู้ใช้ที่ล็อกอินอยู่
    public IActionResult MyOrders()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var orders = _db.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Brand)
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
                .ThenInclude(s => s.Address)
            .Where(o => o.UserId == userId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .ToList()
            .Select(MapOrderSummary)
            .ToList();

        return View(orders);
    }

    // แสดงรายละเอียดของคำสั่งซื้อหนึ่งรายการที่เป็นของผู้ใช้ปัจจุบัน
    public IActionResult OrderDetail(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var order = _db.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Brand)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
                .ThenInclude(s => s.Address)
            .FirstOrDefault(o => o.OrderId == id && o.UserId == userId.Value);

        if (order == null)
        {
            return RedirectToAction("MyOrders");
        }

        return View(MapOrderDetail(order));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ยกเลิกคำสั่งซื้อเมื่อสถานะยังอนุญาตและคืนสต็อกให้สินค้าทุกรายการ
    public IActionResult CancelOrder(int id, string? cancelReason)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var order = _db.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
            .FirstOrDefault(o => o.OrderId == id && o.UserId == userId.Value);

        if (order == null)
        {
            return RedirectToAction("MyOrders");
        }

        var status = (order.Status ?? string.Empty).ToLower();
        if (status is "paid" or "packed" or "shipped" or "in_transit" or "delivered" or "returned" or "cancelled"
            || order.Shipments.Any(shipment => shipment.Status is "packed" or "shipped" or "in_transit" or "delivered" or "returned"))
        {
            TempData["OrderError"] = "ออเดอร์นี้ไม่สามารถยกเลิกได้แล้ว";
            return RedirectToAction("OrderDetail", new { id });
        }

        using var transaction = _db.Database.BeginTransaction();
        var changed = _db.Database.ExecuteSqlInterpolated($@"
            UPDATE orders SET status = 'cancelled'
            WHERE order_id = {order.OrderId}
              AND (status IS NULL OR status NOT IN ('paid', 'packed', 'shipped', 'in_transit', 'delivered', 'returned', 'cancelled'))
              AND NOT EXISTS (SELECT 1 FROM shipments WHERE order_id = {order.OrderId}
                  AND status IN ('packed', 'shipped', 'in_transit', 'delivered', 'returned'));");
        if (changed != 1)
        {
            transaction.Rollback();
            TempData["OrderError"] = "ออเดอร์นี้ไม่สามารถยกเลิกได้แล้ว";
            return RedirectToAction("OrderDetail", new { id });
        }

        order.Status = "cancelled";
        order.CancelReason = string.IsNullOrWhiteSpace(cancelReason) ? "ยกเลิกโดยผู้ใช้" : cancelReason;
        order.CancelledAt = DateTime.Now;
        foreach (var payment in order.Payments.Where(value => value.PaymentStatus == "pending"))
        {
            payment.PaymentStatus = "cancelled";
        }

        var stockReturns = _db.InventoryTransactions
            .Where(entry => entry.ReferenceOrderId == order.OrderId && entry.TransactionType == "OUT"
                && entry.ProductId.HasValue && entry.Quantity.HasValue)
            .AsEnumerable()
            .GroupBy(entry => entry.ProductId!.Value)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(entry => entry.Quantity!.Value) })
            .ToList();

        foreach (var stockReturn in stockReturns)
        {
            _db.Database.ExecuteSqlInterpolated($@"
                UPDATE products SET stock = COALESCE(stock, 0) + {stockReturn.Quantity}
                WHERE product_id = {stockReturn.ProductId};");
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = stockReturn.ProductId,
                TransactionType = "IN",
                Quantity = stockReturn.Quantity,
                ReferenceOrderId = order.OrderId,
                CreatedAt = DateTime.Now
            });
        }

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.OrderId,
            Status = "cancelled",
            ChangedAt = DateTime.Now
        });

        _db.SaveChanges();
        transaction.Commit();

        TempData["OrderSuccess"] = "ยกเลิกออเดอร์เรียบร้อยแล้ว";
        return RedirectToAction("OrderDetail", new { id });
    }

    // แปลงข้อมูล order ให้เป็นรูปแบบสรุปสำหรับหน้า list และ detail
    private static OrderViewModel MapOrderSummary(Order order)
    {
        var brandNames = order.OrderItems
            .Select(i => i.Product?.Brand?.BrandName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        return new OrderViewModel
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber ?? $"ORD{order.OrderId}",
            TotalAmount = order.TotalAmount ?? 0,
            DiscountAmount = order.DiscountAmount ?? 0,
            ShippingFee = order.ShippingFee ?? 0,
            FinalAmount = order.FinalAmount ?? 0,
            Status = order.Status ?? "pending",
            PaymentMethod = order.Payments.OrderByDescending(p => p.PaidAt).Select(p => p.PaymentMethod).FirstOrDefault() ?? "-",
            PaymentStatus = order.Payments.OrderByDescending(p => p.PaidAt).Select(p => p.PaymentStatus).FirstOrDefault() ?? "-",
            ShippingProvider = order.Shipments.OrderByDescending(s => s.CreatedAt).Select(s => s.ShippingProvider).FirstOrDefault() ?? "-",
            ShippingStatus = order.Status == "cancelled"
                ? "cancelled"
                : order.Shipments.OrderByDescending(s => s.CreatedAt).Select(s => s.Status).FirstOrDefault() ?? "-",
            TrackingNumber = order.Shipments.OrderByDescending(s => s.CreatedAt).Select(s => s.TrackingNumber).FirstOrDefault(),
            ShippingAddress = BuildAddressText(order.Shipments.OrderByDescending(s => s.CreatedAt).Select(s => s.Address).FirstOrDefault()),
            CancelReason = order.CancelReason,
            CreatedAt = order.CreatedAt,
            BrandSummary = brandNames.Count == 0
                ? null
                : string.Join(", ", brandNames.Take(3)) + (brandNames.Count > 3 ? " ..." : string.Empty)
        };
    }

    // เติมรายละเอียดระดับรายการสินค้าเพิ่มเข้าไปในข้อมูลสรุปของ order
    private static OrderViewModel MapOrderDetail(Order order)
    {
        var vm = MapOrderSummary(order);
        vm.Items = order.OrderItems.Select(item => new OrderItemViewModel
        {
            ProductName = item.Product?.Name ?? "-",
            BrandName = item.Product?.Brand?.BrandName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            ImageUrl = item.Product?.ProductImages?.OrderByDescending(i => i.IsMain == true).Select(i => i.ImageUrl).FirstOrDefault()
        }).ToList();

        return vm;
    }

    // แปลงข้อมูลที่อยู่ให้เป็นข้อความบรรทัดเดียวสำหรับแสดงผล
    private static string BuildAddressText(Address? address)
    {
        if (address == null)
        {
            return "-";
        }

        return string.IsNullOrWhiteSpace(address.AddressLine) ? "-" : address.AddressLine.Trim();
    }
}
