class LoginResponse {
  const LoginResponse({
    required this.userId,
    required this.name,
    required this.email,
    required this.role,
    required this.token,
  });

  final String userId;
  final String name;
  final String email;
  final String role;
  final String token;

  factory LoginResponse.fromJson(Map<String, dynamic> json) {
    return LoginResponse(
      userId: json['userId'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
      role: json['role'] as String,
      token: json['token'] as String,
    );
  }
}
