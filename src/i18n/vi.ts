// Vietnamese text used by more than one page. Vietnamese is the main language:
// every key must exist here. en.ts must have exactly the same keys (the
// compiler checks it). Text that belongs to ONE page can also live here, under
// a key that starts with the page name, e.g. 'builder.title'.
//
// {n} style markers are filled in by t('key', { n: 3 }).

export const vi = {
  'skip.toContent': 'Bỏ qua và vào nội dung',

  'nav.main': 'Menu chính',
  'nav.home': 'Trang chủ',
  'nav.builder': 'Tạo hộp quà',
  'nav.about': 'Giới thiệu',
  'nav.commitments': 'Cam kết',
  'nav.blog': 'Blog',
  'nav.openMenu': 'Mở menu',
  'nav.closeMenu': 'Đóng menu',

  'cart.link': 'Giỏ hàng ({n})',
  'cart.aria': 'Giỏ hàng, {n} món',

  'lang.group': 'Ngôn ngữ',
  'lang.vi': 'Tiếng Việt',
  'lang.en': 'English',

  'theme.toDark': 'Bật chế độ tối',
  'theme.toLight': 'Bật chế độ sáng',

  'footer.tagline': 'Hộp quà cho Tết, 20/11 và 8/3.',
  'footer.about': 'Giới thiệu',
  'footer.commitments': 'Cam kết',
  'footer.blog': 'Blog',
  'footer.terms': 'Điều khoản',
  'footer.privacy': 'Chính sách bảo mật',
  'footer.contact': '[SỐ ĐIỆN THOẠI] · [EMAIL]',

  'home.dataCheck': 'Kiểm tra khung: đã tải {n} món mẫu.',
} as const;

export type MessageKey = keyof typeof vi;
