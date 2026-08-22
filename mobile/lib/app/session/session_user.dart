import 'package:gymflow/app/session/app_role.dart';

class SessionUser {
  const SessionUser({
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
  final AppRole role;
}
