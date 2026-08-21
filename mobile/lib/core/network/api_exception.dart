class ApiException implements Exception {
  const ApiException({
    required this.statusCode,
    required this.message,
  });

  final int statusCode;
  final String message;

  @override
  String toString() {
    return 'ApiException(statusCode: $statusCode, message: $message)';
  }
}
