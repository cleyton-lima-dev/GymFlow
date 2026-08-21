import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/auth/models/login_response.dart';

class AuthService {
  const AuthService(this._apiClient);

  final ApiClient _apiClient;

  Future<LoginResponse> login({
    required String email,
    required String password,
  }) async {
    final response = await _apiClient.post(
      'api/auth/login',
      body: {
        'email': email,
        'password': password,
      },
    );

    if (response is! Map<String, dynamic>) {
      throw const FormatException('Invalid login response.');
    }

    return LoginResponse.fromJson(response);
  }
}
