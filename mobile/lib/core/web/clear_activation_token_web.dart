import 'package:web/web.dart' as web;

import 'activation_route_compat.dart';

void prepareDirectActivationRoute() {
  final location = Uri.parse(web.window.location.href);
  final hashRoute = activationHashRouteForDirectUrl(location);
  if (hashRoute != null) {
    web.window.history.replaceState(null, '', hashRoute.toString());
  }
}

void clearActivationTokenFromAddressBar() {
  final location = Uri.parse(web.window.location.href);
  final cleanLocation = activationHashRouteWithoutToken(location);
  if (cleanLocation == null) return;
  web.window.history.replaceState(null, '', cleanLocation.toString());
}
