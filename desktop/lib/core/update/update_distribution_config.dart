import 'dart:convert';

/// Compile-time distribution settings. Missing or inconsistent settings keep
/// the updater disabled; no network request is made in that state.
final class UpdateDistributionConfig {
  UpdateDistributionConfig._({
    required this.manifestUrl,
    required this.signatureUrl,
    required this.downloadOrigin,
    required this.appId,
    required this.channel,
    required this.keyId,
    required List<int> publicKey,
  }) : publicKey = List<int>.unmodifiable(publicKey);

  static const productionAppId = 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837';
  static const homologationAppId = 'D68C6C5D-2D87-4CBA-9918-3AD08C0D1C69';

  final Uri manifestUrl;
  final Uri signatureUrl;
  final String downloadOrigin;
  final String appId;
  final String channel;
  final String keyId;
  final List<int> publicKey;

  static UpdateDistributionConfig? fromEnvironment() {
    return tryCreate(
      enabled: const bool.fromEnvironment('AVELRI_UPDATES_ENABLED'),
      appEnvironment: const String.fromEnvironment('APP_ENV'),
      appId: const String.fromEnvironment('AVELRI_UPDATE_APP_ID'),
      channel: const String.fromEnvironment('AVELRI_UPDATE_CHANNEL'),
      manifestUrl: const String.fromEnvironment('AVELRI_UPDATE_MANIFEST_URL'),
      signatureUrl: const String.fromEnvironment('AVELRI_UPDATE_SIGNATURE_URL'),
      downloadOrigin: const String.fromEnvironment(
        'AVELRI_UPDATE_DOWNLOAD_ORIGIN',
      ),
      keyId: const String.fromEnvironment('AVELRI_UPDATE_KEY_ID'),
      publicKeyBase64: const String.fromEnvironment(
        'AVELRI_UPDATE_PUBLIC_KEY_BASE64',
      ),
    );
  }

  static UpdateDistributionConfig? tryCreate({
    required bool enabled,
    required String appEnvironment,
    required String appId,
    required String channel,
    required String manifestUrl,
    required String signatureUrl,
    required String downloadOrigin,
    required String keyId,
    required String publicKeyBase64,
  }) {
    if (!enabled ||
        keyId.isEmpty ||
        !RegExp(r'^[A-Za-z0-9._-]{1,64}$').hasMatch(keyId) ||
        !((appEnvironment == 'production' &&
                channel == 'production' &&
                appId == productionAppId) ||
            (appEnvironment == 'development' &&
                channel == 'homologation' &&
                appId == homologationAppId))) {
      return null;
    }
    final manifest = Uri.tryParse(manifestUrl);
    final signature = Uri.tryParse(signatureUrl);
    final download = Uri.tryParse(downloadOrigin);
    if (!_safeUrl(manifest) ||
        !_safeUrl(signature) ||
        !_safeUrl(download) ||
        manifest!.origin != signature!.origin ||
        download!.origin != downloadOrigin) {
      return null;
    }

    try {
      final key = base64.decode(publicKeyBase64);
      if (key.length != 32 || base64.encode(key) != publicKeyBase64) {
        return null;
      }
      return UpdateDistributionConfig._(
        manifestUrl: manifest,
        signatureUrl: signature,
        downloadOrigin: downloadOrigin,
        appId: appId,
        channel: channel,
        keyId: keyId,
        publicKey: key,
      );
    } on FormatException {
      return null;
    }
  }

  static bool _safeUrl(Uri? uri) =>
      uri != null &&
      uri.scheme == 'https' &&
      uri.host.isNotEmpty &&
      uri.userInfo.isEmpty &&
      !uri.hasFragment;
}
