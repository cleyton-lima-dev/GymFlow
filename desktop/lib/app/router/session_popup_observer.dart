import 'package:flutter/widgets.dart';

/// Dialogs live outside the shell subtree. Dismiss them at an authorization
/// boundary so a financial form cannot survive a revoked or replaced session.
class SessionPopupObserver extends NavigatorObserver {
  final Set<PopupRoute<dynamic>> _popups = {};

  @override
  void didPush(Route<dynamic> route, Route<dynamic>? previousRoute) {
    if (route is PopupRoute<dynamic>) _popups.add(route);
  }

  @override
  void didPop(Route<dynamic> route, Route<dynamic>? previousRoute) {
    _popups.remove(route);
  }

  @override
  void didRemove(Route<dynamic> route, Route<dynamic>? previousRoute) {
    _popups.remove(route);
  }

  void dismissPopups() {
    for (final route in _popups.toList().reversed) {
      if (route.isActive) route.navigator?.removeRoute(route);
    }
  }
}
