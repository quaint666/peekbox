# Sitemap

File names are proposals. `?slug=` means one page template that serves many items or posts.

```
Peekbox
├── Trang chủ ................................ index.html
├── Tạo hộp quà .............................. builder.html
│   ├── Món (item detail) .................... item.html?slug=
│   └── Đóng gói và xem lại .................. review.html
│       └── Giỏ hàng ......................... cart.html
│           └── Thanh toán ................... checkout.html
│               ├── Thanh toán thất bại ...... payment-failed.html
│               └── Xác nhận đơn hàng ........ confirmation.html
├── Giới thiệu và cam kết .................... about.html
├── Blog ..................................... blog.html
│   └── Bài viết ............................. blog-post.html?slug=
├── Điều khoản và bảo mật .................... legal.html
├── Không tìm thấy trang (404) ............... 404.html
└── Quản trị (later) ......................... admin/
    ├── login.html
    └── orders, items, campaigns, settings, cutoffs, coupons, reviews, stats, users, account
```

**Menus.** Header: Trang chủ, Tạo hộp quà, Giới thiệu, Cam kết, Blog, plus cart and theme. Footer: Giới thiệu, Cam kết, Blog, Điều khoản, Chính sách bảo mật. "Cam kết" and the privacy link jump to a section of `about.html` and `legal.html`.

**Main customer path.** Home → Builder → (Item) → Review → Cart → Checkout → Confirmation. There are back links at every step.
