import 'package:avelri_gestao/app/config/app_environment.dart';

class AppConfig {
  const AppConfig({
    required this.environment,
    required this.apiBaseUrl,
  });

  factory AppConfig.fromEnvironment() {
    const environmentValue = String.fromEnvironment('APP_ENV');
    const apiBaseUrl = String.fromEnvironment('API_BASE_URL');

    if (environmentValue.isEmpty) {
      throw StateError('APP_ENV was not provided.');
    }

    final environment = switch (environmentValue) {
      'development' => AppEnvironment.development,
      'production' => AppEnvironment.production,
      _ => throw StateError(
        'Invalid APP_ENV: $environmentValue',
      ),
    };

    if (apiBaseUrl.isEmpty) {
      throw StateError('API_BASE_URL was not provided.');
    }

    final apiUri = Uri.tryParse(apiBaseUrl);

    if (apiUri == null || !apiUri.hasScheme || !apiUri.hasAuthority) {
      throw StateError('API_BASE_URL is invalid.');
    }

    if (environment == AppEnvironment.production &&
        apiUri.scheme != 'https') {
      throw StateError('Production API_BASE_URL must use HTTPS.');
    }

    return AppConfig(
      environment: environment,
      apiBaseUrl: apiBaseUrl,
    );
  }

  final AppEnvironment environment;
  final String apiBaseUrl;

  bool get isDevelopment =>
      environment == AppEnvironment.development;
}
