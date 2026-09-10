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

  String? _accessToken;
  Future<void> Function()? _onUnauthorized;

  Uri buildUri(String path) {
    return _baseUri.resolve(path);
  }

  void setAccessToken(String token) {
    _accessToken = token;
  }

  void clearAccessToken() {
    _accessToken = null;
  }

  void setUnauthorizedHandler(Future<void> Function() handler) {
    _onUnauthorized = handler;
  }

  Future<Object?> get(
      String path, {
        Map<String, String>? headers,
        bool authenticated = true,
      }) async {
    final response = await _client
        .get(
      buildUri(path),
      headers: _buildHeaders(
        headers: headers,
        authenticated: authenticated,
      ),
    )
        .timeout(requestTimeout);

    final validatedResponse = await _validateResponse(
      response,
      authenticated: authenticated,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> delete(
      String path, {
        Map<String, String>? headers,
        bool authenticated = true,
      }) async {
    final response = await _client
        .delete(
      buildUri(path),
      headers: _buildHeaders(
        headers: headers,
        authenticated: authenticated,
      ),
    )
        .timeout(requestTimeout);

    final validatedResponse = await _validateResponse(
      response,
      authenticated: authenticated,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> post(
      String path, {
        Map<String, String>? headers,
        required Map<String, dynamic> body,
        bool authenticated = true,
      }) async {
    final response = await _client
        .post(
      buildUri(path),
      headers: _buildHeaders(
        headers: headers,
        authenticated: authenticated,
      ),
      body: jsonEncode(body),
    )
        .timeout(requestTimeout);

    final validatedResponse = await _validateResponse(
      response,
      authenticated: authenticated,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> put(
      String path, {
        Map<String, String>? headers,
        required Map<String, dynamic> body,
        bool authenticated = true,
      }) async {
    final response = await _client
        .put(
      buildUri(path),
      headers: _buildHeaders(
        headers: headers,
        authenticated: authenticated,
      ),
      body: jsonEncode(body),
    )
        .timeout(requestTimeout);

    final validatedResponse = await _validateResponse(
      response,
      authenticated: authenticated,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> patch(
      String path, {
        Map<String, String>? headers,
        required Map<String, dynamic> body,
        bool authenticated = true,
      }) async {
    final response = await _client
        .patch(
      buildUri(path),
      headers: _buildHeaders(
        headers: headers,
        authenticated: authenticated,
      ),
      body: jsonEncode(body),
    )
        .timeout(requestTimeout);

    final validatedResponse = await _validateResponse(
      response,
      authenticated: authenticated,
    );

    return decodeJson(validatedResponse);
  }

  Map<String, String> _buildHeaders({
    Map<String, String>? headers,
    required bool authenticated,
  }) {
    return {
      ..._jsonHeaders,
      if (authenticated && _accessToken != null)
        'Authorization': 'Bearer $_accessToken',
      ...?headers,
    };
  }

  Future<http.Response> _validateResponse(
      http.Response response, {
        required bool authenticated,
      }) async {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return response;
    }

    if (authenticated && response.statusCode == 401) {
      await _onUnauthorized?.call();
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
