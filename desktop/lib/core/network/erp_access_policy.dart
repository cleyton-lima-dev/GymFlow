/// Only commercially confirmed ERP surfaces belong here. Operational access,
/// attendance and traditional student management remain independent.
abstract final class ErpAccessPolicy {
  static bool _under(String path, String root) =>
      path == root || path.startsWith('$root/');

  static bool isErpRoute(String path) =>
      _under(path, '/plans') ||
      _under(path, '/enrollments') ||
      _under(path, '/finance') ||
      path == '/students/import' ||
      path == '/students/migration';

  static bool isErpReport(String kind) =>
      kind == 'financial' || kind == 'enrollments';

  static bool isErpApiPath(String value) {
    var path = Uri.parse(value).path;
    if (!path.startsWith('/')) path = '/$path';
    return _under(path, '/api/plans') ||
        _under(path, '/api/enrollments') ||
        _under(path, '/api/charges') ||
        _under(path, '/api/payments') ||
        _under(path, '/api/students/imports') ||
        _under(path, '/api/migrations') ||
        path == '/api/dashboard' ||
        _under(path, '/api/reports/financial') ||
        _under(path, '/api/reports/enrollments') ||
        RegExp(r'^/api/students/[^/]+/(financial-history|reactivate)$')
            .hasMatch(path);
  }
}
