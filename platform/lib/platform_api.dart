import 'dart:convert';
import 'dart:math';

import 'package:http/http.dart' as http;

class PlatformFailure implements Exception {
  const PlatformFailure(this.message, [this.status]);
  final String message;
  final int? status;
}

class PlatformApi {
  PlatformApi(String baseUrl, {http.Client? client})
    : _base = Uri.parse(baseUrl),
      _client = client ?? http.Client();
  final Uri _base;
  final http.Client _client;
  String? token;
  void Function()? onSessionExpired;
  static String requestKey() {
    final random = Random.secure();
    final bytes = List.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  Future<Map<String, dynamic>> request(
    String method,
    String path, {
    Map<String, dynamic>? body,
    String? requestKey,
  }) async {
    final request = http.Request(method, _base.resolve(path));
    request.headers['Content-Type'] = 'application/json';
    if (token != null) request.headers['Authorization'] = 'Bearer $token';
    if (requestKey != null) request.headers['Idempotency-Key'] = requestKey;
    if (body != null) request.body = jsonEncode(body);
    http.Response response;
    try {
      response = await http.Response.fromStream(await _client.send(request))
          .timeout(const Duration(seconds: 30));
    } catch (_) {
      throw const PlatformFailure(
        'Não foi possível conectar. Verifique a conexão antes de tentar novamente.',
      );
    }
    if (response.statusCode >= 400) {
      if ((response.statusCode == 401 || response.statusCode == 403) &&
          token != null) {
        token = null;
        onSessionExpired?.call();
      }
      throw PlatformFailure(switch (response.statusCode) {
        401 || 403 => 'Acesso não autorizado. Confira suas credenciais e o código do autenticador.',
        409 => 'Os dados já existem ou foram alterados. Atualize antes de continuar.',
        429 => 'Muitas tentativas. Aguarde antes de tentar novamente.',
        400 => 'Verifique os campos e as etapas obrigatórias. O convite pode estar inválido ou expirado.',
        _ => 'Não foi possível concluir a operação.',
      }, response.statusCode);
    }
    if (response.body.isEmpty) return {};
    return jsonDecode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
  }
}
