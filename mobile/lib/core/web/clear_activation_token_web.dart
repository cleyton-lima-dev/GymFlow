import 'package:web/web.dart' as web;

import 'activation_route_compat.dart';

String? _initialActivationToken;

void prepareDirectActivationRoute() {
  final location = Uri.parse(web.window.location.href);
  final activationLocation =
      activationHashRouteForDirectUrl(location) ?? location;
  final route = Uri.tryParse(activationLocation.fragment);
  if (route?.path != '/ativar') return;

  // Capture before the router can write its initial location to browser history.
  _initialActivationToken = route?.queryParameters['token'];
  final cleanLocation =
      activationHashRouteWithoutToken(activationLocation) ?? activationLocation;
  if (cleanLocation != location) {
    web.window.history.replaceState(null, '', cleanLocation.toString());
  }
}

String captureActivationToken(String routeToken) {
  final initialToken = _initialActivationToken;
  _initialActivationToken = null;
  return routeToken.isNotEmpty ? routeToken : initialToken ?? '';
}

void clearActivationTokenFromAddressBar() {
  final location = Uri.parse(web.window.location.href);
  final cleanLocation = activationHashRouteWithoutToken(location);
  if (cleanLocation == null) return;
  web.window.history.replaceState(null, '', cleanLocation.toString());
}
