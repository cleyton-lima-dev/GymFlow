import 'package:avelri_gestao/core/storage/session_restoration_guard.dart';

class TestSessionRestorationGuard implements SessionRestorationGuard {
  bool blocked = false;
  bool failRead = false;
  bool failBlock = false;
  bool failAllow = false;

  @override
  Future<bool> isRestorationBlocked() async {
    if (failRead) throw StateError('Synthetic marker read failure');
    return blocked;
  }

  @override
  Future<void> blockRestoration() async {
    if (failBlock) throw StateError('Synthetic marker write failure');
    blocked = true;
  }

  @override
  Future<void> allowRestoration() async {
    if (failAllow) throw StateError('Synthetic marker removal failure');
    blocked = false;
  }
}
