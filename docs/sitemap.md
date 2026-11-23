# Sitemap

The addresses of the pages built so far are in `src/Peekbox.Web/Navigation/SiteLinks.cs`, and `RoutesTests` lists every one; a page marked (M5) or (later) is not built yet, and its address is a proposal. `{slug}` and `{token}` mean one page that serves many items, posts or orders.

```
Peekbox
├── Trang chủ ................................ /
├── Hộp quà theo dịp ......................... /dip/{id} (tet, 20-11, phu-nu)
│   ├── Một hộp (photo, every item, total) ... /hop/{slug}
│   └── "Dùng hộp này" fills the box and opens /builder
├── Tạo hộp quà .............................. /builder
│   ├── Món (item detail) .................... /items/{slug}
│   └── Đóng gói và xem lại .................. /builder/review
│       └── Giỏ hàng ......................... /cart
│           └── Thanh toán ................... /checkout
│               ├── Món vừa hết hàng ......... /checkout/soldout
│               └── Xác nhận đơn hàng ........ /orders/{token}
│                   └── Thanh toán thất bại .. /orders/{token}/payment
├── Giới thiệu và cam kết .................... /about
├── Blog ..................................... /blog
│   └── Bài viết ............................. /blog/{slug}
├── Điều khoản và bảo mật .................... /legal
├── Tra cứu đơn hàng ......................... /track
├── Không tìm thấy trang (404) ............... any unknown address
└── Quản trị ................................. /admin (goes to the orders)
    ├── Đăng nhập ............................ /admin/login
    ├── Đơn hàng ............................. /admin/orders
    ├── Tài khoản ............................ /admin/account
    ├── Không có quyền (403) ................. /admin/denied
    └── later: items, campaigns, settings, cutoffs, coupons, reviews, stats, users
```

**Menus.** Header: Trang chủ, Tạo hộp quà, Dịp, Giới thiệu, Blog, Tra cứu đơn hàng, plus cart and theme. "Dịp" has no page of its own: it goes to `/#cho-tung-dip` (the six cards on the home page), and a menu under it opens on hover or keyboard focus with the six occasion pages (on a phone they are listed under it). On a phone the same links open as a column under the header from the menu button (mockup state 21). Footer: Tạo hộp quà, Giới thiệu, Blog, Tra cứu đơn hàng, the six occasions, and Điều khoản · Chính sách bảo mật in one small line at the bottom. The terms and the privacy link jump to a text on `/legal` (`/legal#dieu-khoan`, `/legal#bao-mat`). "Cam kết" is not a menu item or a footer link: the commitments are a part of the About page (`/about#cam-ket`, linked from the home page's "Xem cam kết" button). The header shows its links in a row from 1100 px wide; below that they are in the menu. In the admin, the sidebar ends with an account chip with the logout button; on a phone it is an initials button that opens a small menu (mockup state 23).

**Occasion path.** Home (occasion card) → Occasion page → (a hamper's page) → "Dùng hộp này" → Builder, then the main path. The builder's "Hộp gợi ý" cards open the same hamper pages.

**Main customer path.** Home → Builder → (Item) → Review → Cart → Checkout → Confirmation. There are back links at every step.

## Breadcrumbs

Every page but the home page and the error pages shows one trail under the header (`Crumbs.Trail` in the view; the last step is the page itself).

| Page | Trail |
|---|---|
| `/blog` | Trang chủ › Blog |
| `/blog/{slug}` | Trang chủ › Blog › the post's title |
| `/builder` | Trang chủ › Tạo hộp quà |
| `/builder/review` | Trang chủ › Tạo hộp quà › Xem lại hộp quà |
| `/cart` | Trang chủ › Giỏ hàng |
| `/checkout` (and the sold-out and demo notices) | Trang chủ › Giỏ hàng › Thanh toán |
| `/orders/{token}` and `/orders/{token}/payment` | Trang chủ › Tra cứu đơn hàng › Đơn hàng {number} |
| `/track` | Trang chủ › Tra cứu đơn hàng |
| `/about` | Trang chủ › Giới thiệu |
| `/legal` | Trang chủ › Điều khoản và bảo mật |
| `/dip/{id}` | Trang chủ › the occasion |
| `/hop/{slug}` | Trang chủ › the occasion (or Tạo hộp quà when the visitor came from the builder) › the hamper |
| `/items/{slug}` | Trang chủ › Tạo hộp quà › the item's category › the item |
