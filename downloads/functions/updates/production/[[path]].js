const routePrefix = '/updates/production/';
const objectPrefix = 'production/';

const fixedFiles = new Map([
  ['release.json', { contentType: 'application/json; charset=utf-8', cacheControl: 'no-store' }],
  ['release.sig', { contentType: 'text/plain; charset=utf-8', cacheControl: 'no-store' }],
]);

// Matches the versioned filename emitted by Build-ProductionInstaller.ps1.
const installerName = /^AvelriGestao-Production-(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\+[1-9]\d*-[0-9a-f]{8}\.exe$/;

const noStoreHeaders = {
  'Cache-Control': 'no-store',
  'X-Content-Type-Options': 'nosniff',
};

function resolveFile(requestUrl) {
  const url = new URL(requestUrl);
  const rawPath = requestUrl.slice(url.origin.length).split(/[?#]/, 1)[0];
  // Require canonical paths: no query, encoding, slash variants, or dot segments.
  if (url.search || url.hash || rawPath !== url.pathname || /[%\\]/.test(rawPath)) {
    return null;
  }
  if (!rawPath.startsWith(routePrefix)) {
    return null;
  }

  const suffix = rawPath.slice(routePrefix.length);
  const fixed = fixedFiles.get(suffix);
  if (fixed) {
    return { key: objectPrefix + suffix, ...fixed };
  }
  if (suffix.startsWith('installers/')) {
    const name = suffix.slice('installers/'.length);
    if (name.length <= 128 && installerName.test(name)) {
      return {
        key: objectPrefix + 'installers/' + name,
        contentType: 'application/octet-stream',
        contentDisposition: `attachment; filename="${name}"`,
        cacheControl: 'public, max-age=31536000, immutable',
      };
    }
  }
  return null;
}

function errorResponse(status, method) {
  const message = status === 404 ? 'Not Found' : status === 405 ? 'Method Not Allowed' : 'Service Unavailable';
  const headers = { ...noStoreHeaders, 'Content-Type': 'text/plain; charset=utf-8' };
  if (status === 405) headers.Allow = 'GET, HEAD';
  return new Response(method === 'HEAD' ? null : message, { status, headers });
}

export async function onRequest({ request, env }) {
  const file = resolveFile(request.url);
  if (!file) return errorResponse(404, request.method);
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    return errorResponse(405, request.method);
  }
  if (!env?.RELEASES || typeof env.RELEASES.get !== 'function' ||
      typeof env.RELEASES.head !== 'function') {
    return errorResponse(503, request.method);
  }

  let object;
  try {
    object = request.method === 'HEAD'
      ? await env.RELEASES.head(file.key)
      : await env.RELEASES.get(file.key);
  } catch {
    return errorResponse(503, request.method);
  }
  if (!object) return errorResponse(404, request.method);

  const headers = {
    'Content-Type': file.contentType,
    'Content-Length': String(object.size),
    'Cache-Control': file.cacheControl,
    'X-Content-Type-Options': 'nosniff',
  };
  if (file.contentDisposition) headers['Content-Disposition'] = file.contentDisposition;
  return new Response(request.method === 'HEAD' ? null : object.body, {
    status: 200,
    headers,
  });
}
