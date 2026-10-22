Uri? activationHashRouteForDirectUrl(Uri location) {
  if (location.path != '/ativar') return null;

  final route = Uri(
    path: location.path,
    query: location.hasQuery ? location.query : null,
  );
  return Uri(
    scheme: location.scheme,
    userInfo: location.userInfo.isEmpty ? null : location.userInfo,
    host: location.host,
    port: location.hasPort ? location.port : null,
    path: '/',
    fragment: route.toString(),
  );
}

Uri? activationHashRouteWithoutToken(Uri location) {
  final route = Uri.tryParse(location.fragment);
  if (route == null || route.path != '/ativar' || !route.hasQuery) return null;

  final query = Map<String, String>.from(route.queryParameters)
    ..remove('token');
  if (query.length == route.queryParameters.length) return null;

  final cleanRoute = route.replace(
    queryParameters: query.isEmpty ? <String, String>{} : query,
  );
  return location.replace(fragment: cleanRoute.toString());
}
