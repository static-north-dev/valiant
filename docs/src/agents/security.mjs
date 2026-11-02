export function contentSecurityPolicy(nonce) {
  return [
    "default-src 'self'",
    `script-src 'self'${nonce ? ` 'nonce-${nonce}'` : ''}`,
    "script-src-attr 'none'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data:",
    "font-src 'self' data:",
    "connect-src 'self'",
    "frame-src 'self' https://challenges.cloudflare.com",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join('; ');
}

export function withSecurityHeaders(response) {
  const headers = new Headers(response.headers);
  const html = /^text\/html\b/i.test(headers.get('Content-Type') ?? '');
  const nonce = html && response.body
    ? btoa(String.fromCharCode(...crypto.getRandomValues(new Uint8Array(16))))
    : undefined;
  headers.set('Content-Security-Policy', contentSecurityPolicy(nonce));
  headers.set('X-Frame-Options', 'DENY');
  if (html) {
    // HTML nonces must not be reused by a cache or retain validators for the original bytes.
    headers.set('Cache-Control', 'private, no-store');
    for (const name of ['Content-Length', 'ETag', 'Last-Modified', 'Content-MD5', 'Content-Digest', 'Repr-Digest', 'Digest']) {
      headers.delete(name);
    }
  }
  const secured = new Response(response.body, {status: response.status, statusText: response.statusText, headers});
  return nonce
    ? new HTMLRewriter().on('script', {element(element) {element.setAttribute('nonce', nonce);}}).transform(secured)
    : secured;
}
