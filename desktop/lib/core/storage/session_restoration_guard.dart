import 'dart:io';

import 'package:path_provider/path_provider.dart';

/// A nonsecret logout intent, independent of the Windows credential store.
/// Presence (including an interrupted/empty write) always blocks restoration.
abstract interface class SessionRestorationGuard {
  Future<bool> isRestorationBlocked();
  Future<void> blockRestoration();
  Future<void> allowRestoration();
}

class FileSessionRestorationGuard implements SessionRestorationGuard {
  FileSessionRestorationGuard.inDirectory(Directory directory)
    : _directory = (() async => directory);

  FileSessionRestorationGuard.applicationSupport()
    : _directory = getApplicationSupportDirectory;

  final Future<Directory> Function() _directory;

  Future<File> _marker() async => File(
    '${(await _directory()).path}${Platform.pathSeparator}session.signed-out',
  );

  bool _isMissing(FileSystemException error) =>
      error.osError?.errorCode == 2 ||
      (Platform.isWindows && error.osError?.errorCode == 3);

  @override
  Future<bool> isRestorationBlocked() async {
    final marker = await _marker();
    try {
      final handle = await marker.open();
      await handle.close();
      return true;
    } on FileSystemException catch (error) {
      if (_isMissing(error)) return false; // Includes legacy installations.
      rethrow; // An unreadable guard must never grant restoration.
    }
  }

  @override
  Future<void> blockRestoration() async {
    final marker = await _marker();
    await marker.parent.create(recursive: true);
    await marker.writeAsString('signed-out\n', flush: true);
  }

  @override
  Future<void> allowRestoration() async {
    try {
      await (await _marker()).delete();
    } on FileSystemException catch (error) {
      if (!_isMissing(error)) rethrow;
    }
  }
}
