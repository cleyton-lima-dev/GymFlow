import 'dart:convert';

import 'package:gymflow/core/network/api_exception.dart';
import 'package:http/http.dart' as http;

class ApiClient {
  ApiClient({
    required String baseUrl,
    http.Client? client,
    this.requestTimeout = const Duration(seconds: 15),
  })  : _baseUri = Uri.parse(baseUrl),
        _client = client ?? http.Client();

  static const Map<String, String> _jsonHeaders = {
    'Accept': 'application/json',
    'Content-Type': 'application/json',
  };

  final Uri _baseUri;
  final http.Client _client;
  final Duration requestTimeout;

  Uri buildUri(String path) {
    return _baseUri.resolve(path);
  }

  Future<Object?> get(
      String path, {
        Map<String, String>? headers,
      }) async {
    final response = await _client
        .get(
      buildUri(path),
      headers: {
        ..._jsonHeaders,
        ...?headers,
      },
    )
        .timeout(requestTimeout);

    final validatedResponse = _validateResponse(response);

    return decodeJson(validatedResponse);
  }

  Future<Object?> post(
      String path, {
        Map<String, String>? headers,
        required Map<String, dynamic> body,
      }) async {
    final response = await _client
        .post(
      buildUri(path),
      headers: {
        ..._jsonHeaders,
        ...?headers,
      },
      body: jsonEncode(body),
    )
        .timeout(requestTimeout);

    final validatedResponse = _validateResponse(response);

    return decodeJson(validatedResponse);
  }

  http.Response _validateResponse(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return response;
    }

    throw ApiException(
      statusCode: response.statusCode,
      message: response.body.isEmpty
          ? 'Request failed with status ${response.statusCode}.'
          : response.body,
    );
  }

  Object? decodeJson(http.Response response) {
    if (response.body.isEmpty) {
      return null;
    }

    return jsonDecode(response.body);
  }

  void close() {
    _client.close();
  }
}
