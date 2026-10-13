import 'package:flutter/material.dart';
import 'package:web/web.dart' as web;

import 'platform_app.dart';
import 'platform_api.dart';

void main() {
  const url = String.fromEnvironment('API_BASE_URL');
  final uri = Uri.tryParse(url);
  if (uri == null ||
      !uri.hasAuthority ||
      (uri.scheme != 'https' &&
          !(uri.scheme == 'http' &&
              ['localhost', '127.0.0.1', '[::1]'].contains(uri.host)))) {
    throw StateError(
      'Configure API_BASE_URL HTTPS ou localhost para homologação.',
    );
  }
  final fragment = Uri.base.fragment;
  final token = fragment.startsWith('activate=') ? fragment.substring(9) : null;
  web.window.history.replaceState(
    null,
    '',
    Uri.base.replace(fragment: '').toString(),
  );
  runApp(PlatformApp(api: PlatformApi(url), activationToken: token));
}
