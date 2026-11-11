import 'dart:convert';
import 'dart:io';

import 'package:path_provider/path_provider.dart';

import 'release_manifest_validator.dart';
import 'signed_manifest_reader.dart';

/// Stores only public, signed release metadata and the last check timestamp.
/// Cached manifests are always revalidated before they can be displayed.
abstract interface class UpdateStateStore {
  Future<DateTime?> readLastCheckAt();
  Future<void> writeLastCheckAt(DateTime value);
  Future<SignedManifestPayload?> readPendingManifest();
  Future<void> writePendingManifest(SignedManifestPayload payload);
  Future<void> clearPendingManifest();
}

final class InMemoryUpdateStateStore implements UpdateStateStore {
  DateTime? _lastCheckAt;
  SignedManifestPayload? _pendingManifest;

  @override
  Future<DateTime?> readLastCheckAt() async => _lastCheckAt;

  @override
  Future<void> writeLastCheckAt(DateTime value) async {
    _lastCheckAt = value.toUtc();
  }

  @override
  Future<SignedManifestPayload?> readPendingManifest() async {
    final payload = _pendingManifest;
    if (payload == null) return null;
    return SignedManifestPayload(
      bytes: List<int>.unmodifiable(payload.bytes),
      signatureBase64: payload.signatureBase64,
    );
  }

  @override
  Future<void> writePendingManifest(SignedManifestPayload payload) async {
    _pendingManifest = SignedManifestPayload(
      bytes: List<int>.unmodifiable(payload.bytes),
      signatureBase64: payload.signatureBase64,
    );
  }

  @override
  Future<void> clearPendingManifest() async {
    _pendingManifest = null;
  }
}

final class FileUpdateStateStore implements UpdateStateStore {
  FileUpdateStateStore.applicationSupport()
    : _directoryProvider = getApplicationSupportDirectory;

  FileUpdateStateStore.inDirectory(Directory directory)
    : _directoryProvider = (() async => directory);

  static const _schemaVersion = 1;
  static const _maximumStateBytes = 32 * 1024;

  final Future<Directory> Function() _directoryProvider;

  Future<File> _stateFile() async => File(
    '${(await _directoryProvider()).path}${Platform.pathSeparator}'
    'update-state-v1.json',
  );

  Future<Map<String, dynamic>> _readState() async {
    final file = await _stateFile();
    try {
      if (await file.length() > _maximumStateBytes) return {};
      final decoded = jsonDecode(await file.readAsString());
      if (decoded is! Map<String, dynamic> ||
          decoded['schemaVersion'] != _schemaVersion) {
        return {};
      }
      return Map<String, dynamic>.from(decoded);
    } on FileSystemException {
      return {};
    } on FormatException {
      return {};
    }
  }

  Future<void> _writeState(Map<String, dynamic> state) async {
    final file = await _stateFile();
    await file.parent.create(recursive: true);
    await file.writeAsString(
      jsonEncode({'schemaVersion': _schemaVersion, ...state}),
      encoding: utf8,
      flush: true,
    );
  }

  @override
  Future<DateTime?> readLastCheckAt() async {
    final value = (await _readState())['lastCheckAtUtc'];
    if (value is! String) return null;
    try {
      return DateTime.parse(value).toUtc();
    } on FormatException {
      return null;
    }
  }

  @override
  Future<void> writeLastCheckAt(DateTime value) async {
    final state = await _readState();
    state['lastCheckAtUtc'] = value.toUtc().toIso8601String();
    await _writeState(state);
  }

  @override
  Future<SignedManifestPayload?> readPendingManifest() async {
    final state = await _readState();
    final encodedBytes = state['manifestBytesBase64'];
    final signature = state['signatureBase64'];
    if (encodedBytes == null && signature == null) return null;
    if (encodedBytes is! String ||
        signature is! String ||
        signature.length > 128) {
      await clearPendingManifest();
      return null;
    }
    try {
      final bytes = base64.decode(encodedBytes);
      if (bytes.isEmpty ||
          bytes.length > ReleaseManifestValidator.maximumManifestBytes ||
          base64.encode(bytes) != encodedBytes) {
        await clearPendingManifest();
        return null;
      }
      return SignedManifestPayload(
        bytes: List<int>.unmodifiable(bytes),
        signatureBase64: signature,
      );
    } on FormatException {
      await clearPendingManifest();
      return null;
    }
  }

  @override
  Future<void> writePendingManifest(SignedManifestPayload payload) async {
    if (payload.bytes.isEmpty ||
        payload.bytes.length > ReleaseManifestValidator.maximumManifestBytes ||
        payload.signatureBase64.length > 128) {
      throw const FormatException('Invalid cached release metadata.');
    }
    final state = await _readState();
    state['manifestBytesBase64'] = base64.encode(payload.bytes);
    state['signatureBase64'] = payload.signatureBase64;
    await _writeState(state);
  }

  @override
  Future<void> clearPendingManifest() async {
    final state = await _readState();
    state.remove('manifestBytesBase64');
    state.remove('signatureBase64');
    await _writeState(state);
  }
}
