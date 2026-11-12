class StudentInviteShareLinks {
  const StudentInviteShareLinks._();

  static bool isValidEmail(String? value) {
    final email = value?.trim() ?? '';
    return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email);
  }

  static String message({required String email, required String inviteLink}) =>
      'Olá! Seu acesso ao Avelri Gestão está liberado. '
      'Ative sua conta e defina sua própria senha neste link: $inviteLink\n\n'
      'Use $email como e-mail de login. O convite expira em 24 horas.';

  static Uri email({required String email, required String inviteLink}) => Uri(
    scheme: 'mailto',
    path: email,
    query: _encodeQueryParameters({
      'subject': 'Convite para ativar seu acesso ao Avelri Gestão',
      'body': message(email: email, inviteLink: inviteLink),
    }),
  );

  static Uri whatsapp({required String email, required String inviteLink}) =>
      Uri.https('wa.me', '/', {
        'text': message(email: email, inviteLink: inviteLink),
      });

  static String inviteLink({required String baseUrl, required String token}) =>
      Uri.parse(baseUrl)
          .replace(path: '/ativar', queryParameters: {'token': token})
          .toString();

  static String _encodeQueryParameters(
    Map<String, String> parameters,
  ) => parameters.entries
      .map(
        (entry) =>
            '${Uri.encodeComponent(entry.key)}=${Uri.encodeComponent(entry.value)}',
      )
      .join('&');
}
