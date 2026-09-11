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
            .FirstOrDefault(o => o.OrderId == id && o.UserId == userId.Value);

        if (order == null)
        {
            return RedirectToAction("MyOrders");
        }

        var status = (order.Status ?? string.Empty).ToLower();
        if (status == "packed" || status == "shipped" || status == "delivered" || status == "cancelled")
        {
            TempData["OrderError"] = "ออเดอร์นี้ไม่สามารถยกเลิกได้แล้ว";
            return RedirectToAction("OrderDetail", new { id });
        }

        order.Status = "cancelled";
        order.CancelReason = string.IsNullOrWhiteSpace(cancelReason) ? "ยกเลิกโดยผู้ใช้" : cancelReason;
        order.CancelledAt = DateTime.Now;

        foreach (var item in order.OrderItems)
        {
            if (item.Product != null)
            {
                item.Product.Stock = (item.Product.Stock ?? 0) + item.Quantity;
                _db.Products.Update(item.Product);

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = item.Product.ProductId,
                    TransactionType = "IN",
                    Quantity = item.Quantity,
                    ReferenceOrderId = order.OrderId,
                    CreatedAt = DateTime.Now
                });
            }
        }

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.OrderId,
            Status = "cancelled",
            ChangedAt = DateTime.Now
        });

        _db.SaveChanges();

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
            ShippingStatus = order.Shipments.OrderByDescending(s => s.CreatedAt).Select(s => s.Status).FirstOrDefault() ?? "-",
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
