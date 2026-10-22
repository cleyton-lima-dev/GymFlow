import 'dart:convert';
import 'dart:typed_data';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/core/network/erp_access_policy.dart';
import 'package:http/http.dart' as http;

class SessionChangedException implements Exception {
  const SessionChangedException();
}

class ApiClient {
  ApiClient({
    required String baseUrl,
    http.Client? client,
    this.requestTimeout = const Duration(seconds: 15),
  }) : _baseUri = Uri.parse(baseUrl),
       _client = client ?? http.Client();

  static const Map<String, String> _jsonHeaders = {
    'Accept': 'application/json',
    'Content-Type': 'application/json',
  };

  final Uri _baseUri;
  final http.Client _client;
  final Duration requestTimeout;

  String? _accessToken;
  int _sessionRevision = 0;
  Future<void> Function()? _onUnauthorized;
  void Function()? _onErpAccessDenied;

  int get sessionRevision => _sessionRevision;

  Uri buildUri(String path) {
    return _baseUri.resolve(path);
  }

  void setAccessToken(String token) {
    _sessionRevision++;
    _accessToken = token;
  }

  void clearAccessToken() {
    _sessionRevision++;
    _accessToken = null;
  }

  void setUnauthorizedHandler(Future<void> Function() handler) {
    _onUnauthorized = handler;
  }

  void setErpAccessDeniedHandler(void Function() handler) {
    _onErpAccessDenied = handler;
  }

  Future<Object?> get(
    String path, {
    Map<String, String>? headers,
    bool authenticated = true,
  }) async {
    final revision = _sessionRevision;
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
      sessionRevision: revision,
      path: path,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> delete(
    String path, {
    Map<String, String>? headers,
    bool authenticated = true,
  }) async {
    final revision = _sessionRevision;
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
      sessionRevision: revision,
      path: path,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> post(
    String path, {
    Map<String, String>? headers,
    required Map<String, dynamic> body,
    bool authenticated = true,
  }) async {
    final revision = _sessionRevision;
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
      sessionRevision: revision,
      path: path,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> put(
    String path, {
    Map<String, String>? headers,
    required Map<String, dynamic> body,
    bool authenticated = true,
  }) async {
    final revision = _sessionRevision;
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
      sessionRevision: revision,
      path: path,
    );

    return decodeJson(validatedResponse);
  }

  Future<Object?> patch(
    String path, {
    Map<String, String>? headers,
    required Map<String, dynamic> body,
    bool authenticated = true,
  }) async {
    final revision = _sessionRevision;
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
      sessionRevision: revision,
      path: path,
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
    required int sessionRevision,
    required String path,
  }) async {
    if (authenticated && sessionRevision != _sessionRevision) {
      throw const SessionChangedException();
    }
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return response;
    }

    if (authenticated && response.statusCode == 401) {
      await _onUnauthorized?.call();
    }

    if (authenticated &&
        response.statusCode == 403 &&
        ErpAccessPolicy.isErpApiPath(path)) {
      _onErpAccessDenied?.call();
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

  Future<Uint8List> getBytes(String path) async {
    final revision = _sessionRevision;
    final response = await _client
        .get(
          buildUri(path),
          headers: _buildHeaders(
            headers: {'Accept': 'text/csv'},
            authenticated: true,
          ),
        )
        .timeout(requestTimeout);
    return (await _validateResponse(
      response,
      authenticated: true,
      sessionRevision: revision,
      path: path,
    )).bodyBytes;
  }
}
