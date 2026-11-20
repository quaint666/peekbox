# Sitemap

The addresses of the pages built so far are in `src/Peekbox.Web/Navigation/SiteLinks.cs`, and `RoutesTests` lists every one; a page marked (M4), (M5) or (later) is not built yet, and its address is a proposal. `{slug}` and `{token}` mean one page that serves many items, posts or orders.

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
│               ├── Thanh toán thất bại ...... (M5)
│               └── Xác nhận đơn hàng ........ /orders/{token}
├── Giới thiệu và cam kết .................... /about (M4)
├── Blog ..................................... /blog (M4)
│   └── Bài viết ............................. /blog/{slug} (M4)
├── Điều khoản và bảo mật .................... /legal (M4)
├── Tra cứu đơn hàng ......................... /track
├── Không tìm thấy trang (404) ............... any unknown address (M4; an empty 404 until then)
└── Quản trị (later) ......................... /admin
    ├── login
    └── orders, items, campaigns, settings, cutoffs, coupons, reviews, stats, users, account
```

**Menus.** Header: Trang chủ, Tạo hộp quà, Giới thiệu, Cam kết, Blog, Tra cứu đơn hàng, plus cart and theme. On a phone the same links open as a column under the header from the menu button (mockup state 21). Footer: Giới thiệu, Cam kết, Blog, Tra cứu đơn hàng, Điều khoản, Chính sách bảo mật. "Cam kết" and the privacy link jump to a section of `/about` and `/legal` (`/about#cam-ket`, `/legal#bao-mat`). The header shows its links in a row from 1100 px wide; below that they are in the menu. In the admin, the sidebar ends with an account chip with the logout button; on a phone it is an initials button that opens a small menu (mockup state 23).

**Occasion path.** Home (occasion card) → Occasion page → (a hamper's page) → "Dùng hộp này" → Builder, then the main path. The builder's "Hộp gợi ý" cards open the same hamper pages.

**Main customer path.** Home → Builder → (Item) → Review → Cart → Checkout → Confirmation. There are back links at every step.
