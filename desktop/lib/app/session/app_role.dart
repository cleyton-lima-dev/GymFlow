enum AppRole {
  admin,
  professor,
  student;

  static AppRole fromApiValue(String value) {
    return switch (value) {
      'Admin' => AppRole.admin,
      'Professor' => AppRole.professor,
      'Student' => AppRole.student,
      _ => throw FormatException('Unknown role: $value'),
    };
  }
}
