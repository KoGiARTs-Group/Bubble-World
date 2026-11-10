import { Router } from 'express';
import { config } from '../config';

const router = Router();

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// The "Get the app" QR on Unity's Link Companion App screen encodes this URL —
// one QR, two stores. The scanning phone's browser hits this route and its
// User-Agent picks the destination (Android → Google Play, iOS → App Store).
// Desktop scanners (or a missing target link) get a small chooser page with
// both store buttons. The targets come from the backend .env, keeping the
// store listings a backend-side config change (no client rebuild).
router.get('/download', (req, res) => {
  const ua = req.header('user-agent') || '';
  const play = config.app.googlePlayUrl;
  const appStore = config.app.appStoreUrl;

  if (/android/i.test(ua) && play) {
    res.redirect(302, play);
    return;
  }
  if (/(iphone|ipad|ipod)/i.test(ua) && appStore) {
    res.redirect(302, appStore);
    return;
  }

  const buttons = [
    play ? `<a href="${escapeHtml(play)}">Get it on Google Play</a>` : '',
    appStore ? `<a class="secondary" href="${escapeHtml(appStore)}">Download on the App Store</a>` : '',
  ]
    .filter(Boolean)
    .join('\n      ');
  const placeholder = !play && !appStore
    ? '<p class="muted">Store links are coming soon.</p>'
    : '';

  res.status(200).type('html').send(`<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Get the BubbleWorld Companion App</title>
  <style>
    body { font-family: system-ui, -apple-system, sans-serif; background: #0b1114; color: #eaf2ee;
           display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; }
    main { text-align: center; max-width: 420px; padding: 24px; }
    a { display: block; margin: 12px auto; padding: 14px 20px; border-radius: 10px;
        background: #4ddb80; color: #08110b; text-decoration: none; font-weight: 600; }
    a.secondary { background: #1d2a2f; color: #eaf2ee; }
    .muted { opacity: 0.7; font-size: 14px; }
  </style>
</head>
<body>
  <main>
    <h1>BubbleWorld Companion App</h1>
    <p>Install the app, then scan the link code shown in your game.</p>
      ${buttons || placeholder}
  </main>
</body>
</html>`);
});

export default router;
