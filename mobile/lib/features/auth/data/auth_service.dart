import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/auth/models/current_user_response.dart';
import 'package:gymflow/features/auth/models/login_response.dart';

class AuthService {
  const AuthService(this._apiClient);

  final ApiClient _apiClient;

  Future<CurrentUserResponse> getCurrentUser(String token) async {
    final response = await _apiClient.get(
      'api/auth/me',
      headers: {
        'Authorization': 'Bearer $token',
      },
    );

    if (response is! Map<String, dynamic>) {
      throw const FormatException('Invalid current user response.');
    }

    return CurrentUserResponse.fromJson(response);
  }

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
