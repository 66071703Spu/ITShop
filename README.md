# ITShop ## ภาพรวมโปรเจกต์

ITShop เป็นโปรเจกต์ ASP.NET Core MVC สำหรับร้านค้าอุปกรณ์ไอที โดยแยกโครงสร้างตามแนว MVC ชัดเจน

- `Controllers/` ควบคุม flow การทำงานของ request
- `Models/` เก็บโครงสร้างข้อมูลจริงและความสัมพันธ์กับฐานข้อมูล
- `ViewModels/` จัดรูปข้อมูลสำหรับส่งไป View
- `Helpers/` รวม business logic ที่ใช้ซ้ำหลายจุด
- `Views/` แสดงผลหน้าจอฝั่งผู้ใช้และหลังบ้าน

โปรเจกต์นี้มีทั้งฝั่งลูกค้าและฝั่งหลังบ้าน

- ฝั่งลูกค้า: Home, Product, Cart, Order, Account
- ฝั่งหลังบ้าน: Admin, SuperAdmin

หมายเหตุ:

- เดิมเคยมี `CatagoryController` สำหรับ route เก่าของหมวดหมู่สินค้า แต่ตอนนี้ถูกลบออกแล้ว และระบบใช้ `ProductController` เป็นจุดหลักของหมวดหมู่สินค้าแทน

## โครงสร้างหลักของระบบ

### MVC ในโปรเจกต์นี้

แนวคิดของโปรเจกต์นี้คือ

1. Controller รับ request และกำหนด flow
2. Model คือข้อมูลจริงจากฐานข้อมูล
3. ViewModel คือข้อมูลที่จัดรูปเพื่อใช้ในหน้า view
4. Helper คือ business rule หรือ utility ที่ใช้ซ้ำหลาย controller

ตัวอย่างเช่น

- `ProductController` ดึงสินค้าและ map เป็น `ProductViewModel`
- `PromotionPriceCalculator` คำนวณราคาหลังลด
- `Views/Product/ProductDetail.cshtml` แสดงผลรายละเอียดสินค้า

### ลำดับการทำงานของ request ในโปรเจกต์นี้

เวลา request วิ่งเข้ามาในระบบ โดยทั่วไปจะเป็นลำดับนี้

1. Browser เรียก route เช่น `/Product/ProductDetail/5`
2. Controller รับ request และตรวจ query string, session หรือสิทธิ์ของผู้ใช้
3. Controller ใช้ `Csi402dbContext` ดึงข้อมูล model จากฐานข้อมูล
4. ถ้าต้องคำนวณกฎธุรกิจเพิ่มเติม จะเรียกใช้ helper เช่นโปรโมชัน, คูปอง, invite หรือสิทธิ์หลังบ้าน
5. Controller map ข้อมูลให้เป็น ViewModel ที่หน้า Razor ใช้งานได้ทันที
6. Razor View แสดงผลโดยไม่ต้องไปรวม business logic หนัก ๆ เอง

แนวนี้เป็นเหตุผลว่าทำไมโค้ดหลายส่วนในโปรเจกต์ถึงมีขั้น `query -> helper -> map viewmodel -> return view` ค่อนข้างชัด

## Controllers

### HomeController

ไฟล์: `Controllers/HomeController.cs`

หน้าที่หลักคือดูแลหน้าแรกของระบบและหน้าข้อมูลทั่วไป เช่น About, Privacy และ Error

ฟังก์ชันสำคัญ

- `Index()`
  รวบรวมข้อมูลทั้งหมดของหน้า Home เช่น banners, promotions, new arrivals, best sellers, product sections และส่งเป็น `HomeViewModel`
- `Privacy()`
  เปิดหน้า Privacy
- `About()`
  เปิดหน้า About
- `Error()`
  เปิดหน้าแสดง error
- `GetProductsByCategory(...)`
  ใช้ดึงสินค้าตามหมวดหมู่เพื่อประกอบ section บนหน้า Home
- `GetBanners(...)`
  ดึง banners ตามตำแหน่งและช่วงเวลาที่ active
- `MapBanner(...)`
  แปลง `Banner` เป็น `BannerViewModel`
- `MapProduct(...)`
  แปลง `Product` เป็น `ProductViewModel` พร้อมข้อมูลราคาหลังโปรโมชัน

เกี่ยวข้องกับ

- `ViewModels/HomeViewModel.cs`
- `ViewModels/ProductViewmodels.cs`
- `ViewModels/BannerViewModel.cs`
- `Helpers/PromotionPriceCalculator.cs`
- `Views/Home/Index.cshtml`

### ProductController

ไฟล์: `Controllers/ProductController.cs`

หน้าที่หลักคือจัดการหน้ารายการสินค้าและหน้ารายละเอียดสินค้า

ฟังก์ชันสำคัญ

- `Index(...)`
  แสดงรายการสินค้า รองรับ search, filter, category, special filter, sorting และ pagination logic แบบพื้นฐาน
- `ProductDetail(int id)`
  แสดงรายละเอียดสินค้า 1 ชิ้น พร้อมรูป, ส่วนลด, คูปอง, related products และข้อมูล package/comset
- `ResolveSpecialFilters(...)`
  แปลง special filter จาก query ให้เป็นค่าที่ระบบใช้ได้
- `NormalizeSpecialFilter(...)`
  normalize ชื่อ filter ให้สม่ำเสมอ
- `GetPageTitle(...)`
  สร้างชื่อหน้าให้เหมาะกับ category/filter ปัจจุบัน
- `ApplyStockFilter(...)`
  กรองสินค้าตามสถานะสต็อก
- `ApplySpecialFilters(...)`
  ใช้ special filter เฉพาะทางกับ query สินค้า
- `ApplySorting(...)`
  เรียงสินค้า เช่น newest, price low-high, best seller
- `GetRelatedProducts(...)`
  เลือกสินค้าที่เกี่ยวข้องกับสินค้าปัจจุบัน
- `CalculateRelatedScore(...)`
  ให้คะแนนความเกี่ยวข้องของสินค้าแต่ละตัว
- `ApplyPackageDetails(...)`
  เติมข้อมูล package/comset ลงใน `ProductViewModel`
- `MapPackageComponent(...)`
  แปลง component ย่อยใน package เป็น `PackageComponentViewModel`
- `BuildPackageComponentDescription(...)`
  สร้างข้อความอธิบายส่วนประกอบใน package
- `IsComsetProduct(...)`
  เช็คว่าสินค้าเป็น comset หรือไม่
- `ExtractTokens(...)`
  ตัดคำจากชื่อ/คำอธิบายเพื่อใช้กับ related scoring
- `MapProduct(...)`
  แปลง `Product` เป็น `ProductViewModel`

เกี่ยวข้องกับ

- `Models/Product.cs`
- `ViewModels/ProductViewmodels.cs`
- `Helpers/PromotionPriceCalculator.cs`
- `Helpers/CouponPromotionHelper.cs`
- `Views/Product/ProductDetail.cshtml`

### CartController

ไฟล์: `Controllers/CartController.cs`

หน้าที่หลักคือจัดการตะกร้าสินค้า, checkout, คูปอง และการสร้างคำสั่งซื้อ

ฟังก์ชันสำคัญ

- `Cart()`
  โหลดรายการสินค้าใน cart จาก session แล้วรีเฟรชข้อมูลให้ตรงกับสินค้าปัจจุบัน
- `ApplyCoupon(...)`
  รับ coupon code จากผู้ใช้และตรวจว่าคูปองใช้ได้กับ cart/checkout ปัจจุบันหรือไม่
- `RemoveCoupon(...)`
  ลบ coupon ออกจาก session
- `AddToCart(...)`
  เพิ่มสินค้าลง cart ปกติ
- `BuyNow(...)`
  สร้าง flow ซื้อทันทีโดยไม่ผ่าน cart ปกติ
- `UpdateQuantity(...)`
  เพิ่มหรือลดจำนวนสินค้าใน cart
- `Remove(...)`
  ลบสินค้าออกจาก cart
- `Checkout(...)`
  เตรียมข้อมูล checkout เช่น ที่อยู่, shipping options, coupon และรายการสินค้า
- `PlaceOrder(...)`
  สร้าง Order, OrderItem, Shipment, Payment, InventoryTransaction, CouponRedemption และล้าง cart เมื่อสั่งซื้อสำเร็จ
- `OrderSuccess()`
  เปิดหน้ายืนยันคำสั่งซื้อสำเร็จ
- `LoadSessionCart()` / `SaveSessionCart(...)`
  โหลดและบันทึก cart ปกติใน session
- `LoadBuyNowItems()` / `SaveBuyNowItems(...)`
  โหลดและบันทึก buy now flow ใน session
- `AddProductToCartInternal(...)`
  helper กลางสำหรับเพิ่มสินค้าเข้า cart

เกี่ยวข้องกับ

- `ViewModels/CartViewmodels.cs`
- `Models/Order.cs`
- `Models/OrderItem.cs`
- `Models/Shipment.cs`
- `Models/Payment.cs`
- `Models/InventoryTransaction.cs`
- `Helpers/CouponPromotionHelper.cs`
- `Helpers/InvitePromotionHelper.cs`

### OrderController

ไฟล์: `Controllers/OrderController.cs`

หน้าที่หลักคือดูรายการคำสั่งซื้อของผู้ใช้และดูรายละเอียดออเดอร์

ฟังก์ชันสำคัญ

- `MyOrders()`
  ดึงคำสั่งซื้อทั้งหมดของ user ปัจจุบันแล้ว map เป็น `OrderViewModel`
- `OrderDetail(int id)`
  ดึงคำสั่งซื้อรายรายการพร้อม order items, payment, shipment และ address
- `CancelOrder(int id, string? cancelReason)`
  ยกเลิกออเดอร์ ถ้ายังไม่ถึงสถานะที่ยกเลิกไม่ได้ พร้อมคืน stock และบันทึกประวัติสถานะ
- `MapOrderSummary(...)`
  แปลง `Order` เป็นข้อมูลสรุปสำหรับ list/detail
- `MapOrderDetail(...)`
  เติมรายการสินค้าย่อยสำหรับหน้า detail
- `BuildAddressText(...)`
  helper แสดงที่อยู่แบบพร้อมใช้

เกี่ยวข้องกับ

- `ViewModels/OrderViewModels.cs`
- `Views/Order/MyOrders.cshtml`
- `Views/Order/OrderDetail.cshtml`

### AccountController

ไฟล์: `Controllers/AccountCotroller.cs`

หน้าที่หลักคือระบบสมาชิก, login, signup, profile, addresses, password และ invite

ฟังก์ชันสำคัญ

- `Login()` / `Login(LoginViewModel data)`
  เปิดหน้า login และตรวจ user เพื่อตั้ง session `UserId`, `UserEmail`, `UserRole`
- `Signup(string? invite = null)`
  เปิดหน้าสมัครสมาชิกและรองรับ invite code จาก query string
- `Register(SignupViewModel data)`
  สมัครสมาชิกจริง สร้าง user, address เริ่มต้น, user role และประวัติ invite ถ้ามี
- `Profile()`
  สร้าง profile dashboard ของผู้ใช้ โดยรวม orders, coupons, addresses และ invite overview
- `EditProfile()` / `EditProfile(EditProfileViewModel data)`
  เปิดฟอร์มและบันทึกการแก้ไขข้อมูลส่วนตัว
- `ManageAddresses()`
  เปิดหน้าจัดการที่อยู่
- `AddAddress(...)`
  เพิ่มที่อยู่ใหม่ให้ user
- `SetDefaultAddress(int id)`
  ตั้งที่อยู่เริ่มต้น
- `DeleteAddress(int id)`
  ลบที่อยู่และตั้ง default ใหม่ถ้าจำเป็น
- `ChangePassword()` / `ChangePassword(ChangePasswordViewModel data)`
  เปลี่ยนรหัสผ่านสำหรับผู้ใช้ที่ login อยู่
- `ForgotPassword()` / `ForgotPassword(ForgotPasswordViewModel data)`
  reset password แบบง่ายโดยใช้ email
- `Logout()`
  ล้าง session ทั้งหมด
- `Delete(string email)`
  ลบผู้ใช้จากฝั่ง admin ภายใต้เงื่อนไขว่าต้องไม่มี order history
- `GetCurrentUser(...)`
  helper โหลด user ปัจจุบันจาก session
- `PopulateInviteSignupContext(...)`
  เติมข้อมูล invite ลงใน signup form
- `MapRecentOrder(...)`
  map order ล่าสุดเป็นการ์ดย่อสำหรับ dashboard

เกี่ยวข้องกับ

- `ViewModels/LoginViewModel.cs`
- `ViewModels/SignupViewModel.cs`
- `ViewModels/ForgotPasswordViewModel.cs`
- `ViewModels/AccountProfileViewModels.cs`
- `Helpers/InvitePromotionHelper.cs`
- `Helpers/CouponPromotionHelper.cs`

จุดที่ควรสังเกต

- ไฟล์ชื่อ `AccountCotroller.cs` สะกดชื่อไฟล์ผิด แต่ class ภายในยังเป็น `AccountController` และระบบยังทำงานได้ตามปกติ
- controller นี้เป็นศูนย์กลางของ session ผู้ใช้ทั่วไป เช่น `UserId`, `UserEmail`, `UserRole`
- profile page ไม่ได้แค่ดึงข้อมูล user อย่างเดียว แต่รวม order, coupon, address และ invite history เข้ามาในหน้าเดียวด้วย

### AdminController

ไฟล์: `Controllers/AdminCotroller.cs`

หน้าที่หลักคือจัดการระบบหลังบ้านสำหรับ admin เช่น dashboard, สินค้า, แบรนด์, banner, promotion, stock, order, shipment และลูกค้า

ฟังก์ชันสำคัญ

- `OnActionExecuting(...)`
  เช็คว่าเป็น admin/superadmin และมี permission สำหรับ action ปัจจุบัน
- `Dashboard()`
  แสดง dashboard หลังบ้านพร้อม recent orders
- `Profile()`
  แสดง profile ของ admin ปัจจุบัน
- `ProductList()`
  แสดงรายการสินค้า
- `AddProduct()` / `AddProduct(ProductViewModel data)`
  เปิดฟอร์มและบันทึกสินค้าใหม่ รวมทั้งรองรับสินค้า Comset
- `EditProduct()` / `EditProduct(ProductViewModel data)`
  เปิดฟอร์มและบันทึกการแก้ไขสินค้า
- `DeleteProduct(int id)`
  ลบสินค้า ถ้ายังไม่มี order history
- `ManageStock()` / `UpdateStock(...)`
  แสดงและแก้สต็อก พร้อมบันทึก inventory transaction
- `BrandList()`, `AddBrand()`, `EditBrand()`, `DeleteBrandLogo()`, `DeleteBrand()`
  จัดการแบรนด์และโลโก้แบรนด์
- `BannerList()`, `AddBanner()`, `EditBanner()`, `DeleteBanner()`
  จัดการ banner สำหรับหน้า Home
- `PromotionList()`, `AddPromotion()`, `EditPromotion()`, `DeletePromotion()`
  จัดการโปรโมชั่นและ coupon ที่ผูกกับโปรโมชั่น
- `OrderList()`
  แสดงคำสั่งซื้อทั้งหมดในหลังบ้าน
- `EditShipment()` / `EditShipment(AdminShipmentUpdateViewModel data)`
  จัดการ shipment ของคำสั่งซื้อ
- `Userlist()`, `CustomerProfile()`, `Adduser()`, `Edituser()`, `Deleteuser()`
  จัดการบัญชีลูกค้าในหลังบ้าน

helper ภายในที่สำคัญ

- `BuildBackOfficeProfile(...)`
- `MapAdminOrder(...)`
- `BuildShipmentUpdateViewModel(...)`
- `ApplyShipmentTimestamps(...)`
- `PopulateProductFormOptions()`
- `SyncProductPackage(...)`
- `TryValidatePromotion(...)`
- `ResolvePromotionProductIds(...)`
- `SyncPromotionCoupon(...)`
- `TrySaveUploadedImage(...)`
- `MapProduct(...)`

เกี่ยวข้องกับ

- `ViewModels/AdminViewModels.cs`
- `ViewModels/ProductViewmodels.cs`
- `ViewModels/BannerViewModel.cs`
- `Helpers/BackOfficeAccessHelper.cs`
- `Helpers/BrandLogoHelper.cs`

จุดที่ควรสังเกต

- ไฟล์ชื่อ `AdminCotroller.cs` สะกดชื่อไฟล์ผิด แต่ class ภายในยังเป็น `AdminController`
- controller นี้เป็น controller ที่ใหญ่ที่สุดในระบบ เพราะรวมหลายโดเมนของหลังบ้านไว้ในที่เดียว
- method กลุ่ม product, promotion และ shipment มี helper ภายในจำนวนมาก เพราะต้องแยก validation และ mapping ออกจาก action หลักไม่ให้ยาวเกินไป

### SuperAdminController

ไฟล์: `Controllers/SuperAdminController.cs`

หน้าที่หลักคือจัดการ role, permission และบัญชี staff ระดับสูง

ฟังก์ชันสำคัญ

- `OnActionExecuting(...)`
  บังคับให้เข้าได้เฉพาะ superadmin
- `Dashboard()`
  แสดง dashboard ระดับสิทธิ์ เช่น จำนวน admins, superadmins, permissions และ recent admins
- `Profile()`
  แสดง profile ของ superadmin ปัจจุบัน
- `AdminList()`
  แสดงรายชื่อ staff ที่เป็น admin หรือ superadmin
- `AdminProfile(int id)`
  เปิดดู profile ของ staff รายคน
- `AddAdmin()` / `AddAdmin(AdminUserFormViewModel data)`
  เพิ่มบัญชี staff ใหม่
- `EditAdmin()` / `EditAdmin(AdminUserFormViewModel data)`
  แก้ไขข้อมูล staff และ role
- `DeleteAdmin(int id)`
  ลบบัญชี staff โดยกันการลบตัวเองและกันการลบ superadmin คนสุดท้าย
- `Permissions()`
  สร้างหน้าจอ role-permission matrix
- `SaveRolePermissions(RolePermissionBulkUpdateViewModel data)`
  บันทึก permission ของ roles ทั้งหมด
- `MapAdminUser(...)`
  map user เป็น `AdminViewModel`
- `BuildBackOfficeProfile(...)`
  สร้าง profile model สำหรับฝั่งหลังบ้าน
- `GetStaffRoleOptions()`
  สร้าง dropdown role สำหรับ admin/superadmin
- `IsLastSuperAdmin(...)`
  กันไม่ให้ระบบเหลือ superadmin เป็นศูนย์
- `BuildPermissionLabel(...)`
  แปลง permission code เป็นข้อความที่อ่านง่าย

เกี่ยวข้องกับ

- `ViewModels/SuperAdminViewModels.cs`
- `ViewModels/AdminViewModels.cs`
- `Helpers/BackOfficeAccessHelper.cs`

## Helpers

### PromotionPriceCalculator

ไฟล์: `Helpers/PromotionPriceCalculator.cs`

หน้าที่คือคำนวณราคาหลังโปรโมชันอัตโนมัติ และสร้าง label ส่วนลด

ฟังก์ชันสำคัญ

- `Calculate(Product product, DateTime? now = null)`
  คำนวณราคาหลังโปรโมชันที่ดีที่สุดสำหรับสินค้า
- `HasAutoApplyPromotion(...)`
  เช็คว่ามีโปรโมชัน auto apply หรือไม่
- `CalculateDiscountAmount(...)`
  คำนวณจำนวนเงินส่วนลดจาก percent/fixed
- `BuildDiscountLabel(...)`
  สร้างข้อความเช่น `-10%` หรือ `-200 THB`

ทำไมต้องแยกเป็น helper

- ใช้ซ้ำทั้ง Home, Product, Cart และหน้าที่แสดงส่วนลด
- ถ้าใส่ใน controller จะทำให้ logic การคำนวณราคาซ้ำหลายจุด

### CouponPromotionHelper

ไฟล์: `Helpers/CouponPromotionHelper.cs`

หน้าที่คือคัดและคำนวณว่าคูปองไหนควรแสดงให้ user เห็น และใช้ได้กับสินค้าปัจจุบันหรือไม่

ฟังก์ชันสำคัญ

- `GetVisibleCoupons(...)`
  ดึง coupon promotions ที่มองเห็นได้
- `MapCoupon(...)`
  แปลง promotion เป็น `CouponDisplayViewModel`
- `CalculateEligibleSubtotal(...)`
  คำนวณ subtotal ของสินค้าที่เข้าเงื่อนไขคูปอง
- `BuildApplicableProductSummary(...)`
  สรุปรายการสินค้าที่คูปองนี้ใช้ได้
- `BuildEligibilityMessage(...)`
  สร้างข้อความอธิบาย eligibility

ทำไมต้องแยกเป็น helper

- coupon logic ถูกใช้ทั้งใน cart, checkout และ profile
- ถ้ากระจายไปหลาย controller จะควบคุม business rule ยาก

### InvitePromotionHelper

ไฟล์: `Helpers/InvitePromotionHelper.cs`

หน้าที่คือดูแลระบบ invite ตั้งแต่สมัครสมาชิกผ่าน invite code จนถึงแจก reward coupon หลัง order แรก

ฟังก์ชันสำคัญ

- `NormalizeInviteCode(...)`
- `GetPrimaryActiveInvitePromotion(...)`
- `EnsurePersistentInviteCode(...)`
- `GetValidInviteSignupContext(...)`
- `GenerateUniquePersistentInviteCode(...)`
- `GenerateUniqueRewardCouponCode(...)`
- `BuildInviteLink(...)`
- `BuildInviteProductSummary(...)`
- `GrantInviteRewardForFirstOrder(...)`

ทำไมต้องแยกเป็น helper

- invite flow เกี่ยวข้องทั้ง Account, Profile, Cart, Order และ Coupon
- ถ้าเขียนใน controller ตัวเดียวจะซ้ำและผูกกันแน่นเกินไป

### BackOfficeAccessHelper

ไฟล์: `Helpers/BackOfficeAccessHelper.cs`

หน้าที่คือรวมกฎ role และ permission ฝั่งหลังบ้าน

ฟังก์ชันสำคัญ

- `GetCurrentRole(...)`
- `IsAdminRole(...)`
- `IsSuperAdmin(...)`
- `GetRolePriority(...)`
- `HasPermission(...)`

ทำไมต้องแยกเป็น helper

- AdminController และ SuperAdminController ใช้กฎสิทธิ์เดียวกัน
- ช่วยให้การตรวจสิทธิ์สม่ำเสมอทั้งระบบ

### BrandLogoHelper

ไฟล์: `Helpers/BrandLogoHelper.cs`

หน้าที่คือจัดการข้อมูลตั้งต้นของแบรนด์และช่วยสร้าง slug จากชื่อแบรนด์

ฟังก์ชันสำคัญ

- `SeedDefaultBrands(...)`
- `SlugifyBrandName(...)`

### DatabaseSchemaInitializer

ไฟล์: `Helpers/DatabaseSchemaInitializer.cs`

หน้าที่คือจัดการ schema bootstrap และ seed ข้อมูลตั้งต้นของระบบ

ฟังก์ชันสำคัญ

- `EnsureBrandSchema(...)`
- `EnsureCatalogCategories(...)`
- `SeedCatalogProducts(...)`
- `EnsureAuthorizationData(...)`
- `EnsureComsetPackageSeed(...)`
- `EnsurePromotionCouponSchema(...)`
- `EnsureInvitePromotionSchema(...)`

หมายเหตุสำคัญ

- helper ตัวนี้มีบทบาทคล้าย bootstrap ของระบบ คือช่วยให้ฐานข้อมูลมี schema และข้อมูลตั้งต้นที่จำเป็นก่อนใช้งานจริง
- ถ้าไม่มีส่วนนี้ บาง controller หรือ helper จะทำงานไม่ได้เพราะข้อมูล role, category, brand หรือ promotion ตั้งต้นอาจยังไม่ถูกสร้าง

## Models

### Models หลักที่สำคัญ

- `Models/User.cs`
  เก็บข้อมูลผู้ใช้และความสัมพันธ์กับ addresses, orders, roles, invites
- `Models/Product.cs`
  เก็บข้อมูลสินค้า, category, brand, images, promotions และ order items
- `Models/Order.cs`
  เก็บหัวออเดอร์ เช่น ยอดรวม ส่วนลด ค่าส่ง สถานะ และผู้ซื้อ
- `Models/OrderItem.cs`
  เก็บรายการสินค้าย่อยในแต่ละออเดอร์
- `Models/Shipment.cs`
  เก็บข้อมูลการจัดส่งของคำสั่งซื้อ
- `Models/Promotion.cs`
  เก็บกฎของโปรโมชัน
- `Models/Coupon.cs`
  เก็บ coupon code, owner, expiry, usage limit
- `Models/Address.cs`
  เก็บที่อยู่ของผู้ใช้
- `Models/Role.cs`, `Models/Permission.cs`, `Models/UserRole.cs`, `Models/RolePermission.cs`
  เก็บระบบ role-permission ของโครงการ
- `Models/Csi402dbContext.cs`
  เป็น EF Core DbContext ที่รวม DbSet และ mapping ของทุก model

แนวคิดสำคัญ

- Model ใช้แทนโครงสร้างข้อมูลจริง
- ไม่ควรส่ง model ดิบไป view ตรง ๆ ถ้าหน้านั้นต้องใช้ข้อมูลผสมหลายตารางหรือมี field เพื่อ UI โดยเฉพาะ

ความสัมพันธ์ที่สำคัญในโดเมน

- `User` เชื่อมกับ `Address`, `Order`, `UserRole`, `Coupon`, `CouponRedemption`
- `Product` เชื่อมกับ `Brand`, `Category`, `ProductImage`, `OrderItem`, `Promotion` และ package/comset
- `Order` เชื่อมกับ `OrderItem`, `Shipment`, `Payment`, `OrderStatusHistory`
- `Promotion` เชื่อมกับ `Coupon` และกลุ่มสินค้าเป้าหมาย
- `Role` และ `Permission` เชื่อมกันผ่าน `RolePermission` ส่วน `User` เชื่อม role ผ่าน `UserRole`

มองในเชิงรายงาน สามารถอธิบายได้ว่าระบบนี้มี 4 โดเมนหลักคือ

1. Customer/Auth domain
2. Catalog domain
3. Order/Checkout domain
4. Back-office authorization domain

## ViewModels

### HomeViewModel

ไฟล์: `ViewModels/HomeViewModel.cs`

ใช้กับหน้า Home โดยรวม brands, product sections และ banners ไว้ใน object เดียว

### ProductViewModel

ไฟล์: `ViewModels/ProductViewmodels.cs`

ใช้กับทั้งหน้าร้านและหลังบ้านของสินค้า

ข้อมูลที่เพิ่มจาก model ดิบ เช่น

- `RelatedProducts`
- `OriginalPrice`
- `DiscountAmount`
- `HasAutoAppliedPromotion`
- `DiscountLabel`
- `AvailableCoupons`
- `PackageComponents`
- `Quantity`

ทำไมต้องมี

- หน้า product detail และหน้า admin ต้องใช้ข้อมูลมากกว่า `Product` entity ปกติ

### CartViewModel

ไฟล์: `ViewModels/CartViewmodels.cs`

ใช้แทนสินค้าใน cart พร้อมข้อมูลที่พร้อมแสดง เช่นชื่อสินค้า รูป ราคาเดิม ราคาหลังลด และ subtotal

### OrderViewModel

ไฟล์: `ViewModels/OrderViewModels.cs`

ใช้แสดงคำสั่งซื้อในหน้า MyOrders และ OrderDetail

ข้อมูลสำคัญที่ view ใช้

- payment status
- shipping status
- tracking number
- brand summary
- `CanCancel`

### Account/Profile ViewModels

ไฟล์: `ViewModels/AccountProfileViewModels.cs`

ใช้กับ profile dashboard, edit profile, addresses, password และ invite

ตัวที่สำคัญที่สุดคือ `ProfileDashboardViewModel` ซึ่งรวมข้อมูลหลายด้านในหน้าเดียว

### Admin/SuperAdmin ViewModels

ไฟล์:

- `ViewModels/AdminViewModels.cs`
- `ViewModels/SuperAdminViewModels.cs`

ใช้กับหน้าหลังบ้าน เช่น dashboard, order list, shipment edit, brand form, user form และ role-permission matrix

### Auth ViewModels

ไฟล์:

- `ViewModels/LoginViewModel.cs`
- `ViewModels/SignupViewModel.cs`
- `ViewModels/ForgotPasswordViewModel.cs`

ใช้สำหรับรับ input จากฟอร์ม auth และ account flows

### BannerViewModel

ไฟล์: `ViewModels/BannerViewModel.cs`

ใช้กับ list และ form ของ banner ในหลังบ้าน โดยรองรับทั้ง `ImageUrl` และ `ImageFile`

## ทำไมต้องใช้ ViewModel แทนการส่ง Model ไป View ตรง ๆ

เหตุผลหลัก

1. View แต่ละหน้าต้องการข้อมูลไม่เหมือนกัน
2. หลายหน้าต้องรวมข้อมูลจากหลาย model พร้อมกัน
3. บาง field มีไว้เพื่อ UI โดยเฉพาะ เช่น summary, label, dropdown options, upload file, computed state
4. ช่วยไม่ให้ View พึ่งพา EF entity มากเกินไป

ตัวอย่าง

- `OrderViewModel.CanCancel` ช่วยให้ view รู้ทันทีว่าควรแสดงปุ่ม cancel หรือไม่
- `ProductViewModel.DiscountLabel` ช่วยให้หน้า product detail แสดงส่วนลดได้เลย
- `AdminShipmentUpdateViewModel` มีรายการ shipping options พร้อมใช้ใน form

ถ้าไม่ใช้ ViewModel จะเกิดปัญหาแบบนี้ได้ง่าย

- View ต้องเขียนเงื่อนไขซับซ้อนเองมากเกินไป
- View ต้องรู้โครงสร้าง entity หลายตารางเกินจำเป็น
- เปลี่ยน schema model ทีเดียว อาจกระทบหลายหน้าโดยตรง
- การตรวจสอบ input จาก form จะปะปนกับข้อมูลฐานข้อมูลจริงมากเกินไป

## หน้าที่ของส่วนประกอบแต่ละชั้น

### Controller

คุม flow ของ request เช่น redirect, query, map viewmodel, save data

### Model

แทนโครงสร้างข้อมูลจริงในฐานข้อมูลและความสัมพันธ์ระหว่างตาราง

### ViewModel

จัดรูปข้อมูลให้เหมาะกับหน้าจอและฟอร์ม

### Helper

รวม business logic หรือ utility ที่ใช้ซ้ำหลายที่

หลักคิดที่เห็นชัดในโปรเจกต์นี้คือ

- Controller ไม่ควรแบกสูตรคำนวณหรือ validation ธุรกิจทุกอย่างไว้เอง
- View ไม่ควรคำนวณเงื่อนไขธุรกิจหลัก
- Helper และ ViewModel จึงเป็นชั้นกลางที่ช่วยให้โค้ดอ่านง่ายและ reuse ได้จริง

ตัวอย่างที่ชัด

- สูตรคำนวณโปรโมชันไปอยู่ `PromotionPriceCalculator`
- กฎการมองเห็นคูปองไปอยู่ `CouponPromotionHelper`
- กฎ invite ไปอยู่ `InvitePromotionHelper`
- กฎสิทธิ์หลังบ้านไปอยู่ `BackOfficeAccessHelper`

นั่นทำให้ controller ยังพออ่านเป็น flow ธุรกิจได้ เช่น

- รับ request
- โหลดข้อมูล
- เรียก helper
- map viewmodel
- return view หรือ redirect

แทนที่จะกลายเป็นไฟล์ที่มีทั้ง query, validation, business rule, string formatting และ HTML concern ปนกันหมด

## Flow หลักของระบบ

### 1. การแสดงสินค้าในหน้าร้าน

flow หลักคือ

1. `ProductController.Index()` รับ filter จาก query string
2. ดึง `Product` พร้อม `Brand`, `Category`, `Promotions`
3. ใช้ helper/projection เพื่อคำนวณราคาและสถานะโปรโมชัน
4. map เป็น `ProductViewModel`
5. ส่งไปที่หน้า list หรือ detail

สิ่งที่สำคัญใน flow นี้คือ controller ไม่ได้ส่ง `Product` ดิบไป view เพราะหน้าร้านต้องใช้ข้อมูลคำนวณแล้ว เช่นราคาหลังลด, label ส่วนลด, related products และ package details

### 2. การซื้อสินค้าและสร้างออเดอร์

flow หลักคือ

1. ผู้ใช้เพิ่มสินค้าใน `CartController`
2. รายการสินค้าเก็บใน session ก่อน ยังไม่เป็น order จริง
3. ตอนเข้า checkout ระบบจะรีเฟรชสินค้าและสต็อกจากฐานข้อมูลอีกครั้ง
4. ถ้ามี coupon จะตรวจสิทธิ์การใช้ coupon ก่อนคิดยอด
5. `PlaceOrder()` สร้าง `Order`, `OrderItem`, `Shipment`, `Payment`, `InventoryTransaction`
6. ถ้ามี coupon ที่ใช้จริง จะสร้าง `CouponRedemption`
7. ถ้ามี invite promotion ที่เข้าเงื่อนไข อาจแจก reward ตาม flow ที่ helper ดูแล

จุดนี้เป็น flow ธุรกิจหลักที่สุดของระบบ เพราะเชื่อม catalog, stock, payment state, shipment และ promotion เข้าด้วยกัน

### 3. การจัดการหลังบ้าน

flow หลักคือ

1. ผู้ใช้หลังบ้านเข้า `AdminController` หรือ `SuperAdminController`
2. `OnActionExecuting()` เช็ค session และสิทธิ์ก่อนเสมอ
3. action หลักโหลดข้อมูลตามโดเมนที่ดูแล เช่นสินค้า แบรนด์ โปรโมชั่น ออเดอร์ หรือ staff
4. helper ภายใน controller ช่วย validate, map และ sync ความสัมพันธ์ย่อย
5. บันทึกลงฐานข้อมูลและ redirect กลับหน้า list/detail ที่เหมาะสม

ความต่างระหว่างสอง controller นี้คือ

- `AdminController` เน้นดูแลธุรกิจประจำวัน เช่นสินค้า ออเดอร์ ลูกค้า โปรโมชั่น
- `SuperAdminController` เน้นดูแล staff และสิทธิ์ระดับระบบ

## สรุปสั้นสำหรับพรีเซนต์

ถ้าต้องอธิบายระบบนี้แบบสั้นที่สุด สามารถพูดได้ว่า

1. `Controllers` คุม flow ของระบบ เช่น ซื้อสินค้า, จัดการสินค้า, จัดการสิทธิ์
2. `Models` เก็บข้อมูลจริงของสินค้า ผู้ใช้ ออเดอร์ โปรโมชัน และสิทธิ์
3. `ViewModels` ทำให้แต่ละหน้ารับข้อมูลที่พร้อมแสดง โดยไม่ต้องไปจัดรูปเองใน view
4. `Helpers` รวมกฎธุรกิจ เช่น คำนวณโปรโมชัน คัดคูปอง และจัดการ invite
5. ระบบหลังบ้านแยกสิทธิ์แบบ role-permission ทำให้จัดการ admin และ superadmin ได้ยืดหยุ่นกว่า hardcode role ตรง ๆ
