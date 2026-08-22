class CurrentUserResponse {
  const CurrentUserResponse({
    required this.userId,
    required this.gymId,
    required this.name,
    required this.email,
    required this.role,
  });

  final String userId;
  final String gymId;
  final String name;
  final String email;
  final String role;

  factory CurrentUserResponse.fromJson(Map<String, dynamic> json) {
    return CurrentUserResponse(
      userId: json['userId'] as String,
      gymId: json['gymId'] as String,
      name: json['name'] as String,
      email: json['email'] as String,
      role: json['role'] as String,
    );
  }
}
