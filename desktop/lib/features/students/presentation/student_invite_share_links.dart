class StudentInviteShareLinks {
  const StudentInviteShareLinks._();

  static String message({
    required String email,
    required String inviteLink,
  }) =>
      'Olá! Seu acesso ao Avelri Gestão está liberado. '
      'Ative sua conta e defina sua senha neste link: $inviteLink\n\n'
      'Use $email como e-mail de login. O convite expira em 24 horas.';

  static Uri email({
    required String email,
    required String inviteLink,
  }) =>
      Uri(
        scheme: 'mailto',
        path: email,
        query: _encodeQueryParameters({
          'subject': 'Convite para ativar seu acesso ao Avelri Gestão',
          'body': message(email: email, inviteLink: inviteLink),
        }),
      );

  static Uri whatsapp({
    required String email,
    required String inviteLink,
  }) =>
      Uri.https(
        'wa.me',
        '/',
        {'text': message(email: email, inviteLink: inviteLink)},
      );

  static String _encodeQueryParameters(Map<String, String> parameters) =>
      parameters.entries
          .map(
            (entry) =>
                '${Uri.encodeComponent(entry.key)}=${Uri.encodeComponent(entry.value)}',
          )
          .join('&');
}
