// Share on the item page (F31): "Sao chép liên kết" and, where the browser has one, the system's share sheet. The Facebook link is
// a plain link and works without this file; the two buttons are hidden until it runs, so a browser without scripts never shows a
// button that does nothing. The address shared is the one the server wrote (data-share-url), never the address bar's.
import { showToast } from './lib/toast.js';

const copy = document.querySelector<HTMLButtonElement>('[data-share-copy]');
const system = document.querySelector<HTMLButtonElement>('[data-share-system]');
const box = copy?.closest<HTMLElement>('[data-share-url]') ?? null;
const url = box?.dataset['shareUrl'] ?? '';
const title = box?.dataset['shareTitle'] ?? document.title;

if (copy && url) {
  copy.hidden = false;
  copy.addEventListener('click', () => {
    // The Clipboard API needs a secure page (https, or localhost) and a click; refused, the visitor is told what to do instead.
    navigator.clipboard.writeText(url).then(
      () => showToast('Đã sao chép liên kết'),
      () => showToast('Không sao chép được. Hãy sao chép địa chỉ trên thanh trình duyệt.'),
    );
  });
}

if (system && url && typeof navigator.share === 'function') {
  system.hidden = false;
  system.addEventListener('click', () => {
    navigator.share({ title, url }).catch((error: unknown) => {
      // Closing the sheet is not a failure.
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        showToast('Không mở được chia sẻ. Hãy dùng “Sao chép liên kết”.');
      }
    });
  });
}
