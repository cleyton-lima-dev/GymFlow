import 'dart:async';

import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/features/updates/presentation/update_controller.dart';

/// Schedules checks only for an authenticated Admin session. It delegates all
/// network and signature work to the shared UpdateController.
final class UpdateCheckScheduler {
  UpdateCheckScheduler({
    required this.controller,
    this.interval = const Duration(hours: 12),
  }) {
    controller.addListener(_controllerChanged);
  }

  final UpdateController controller;
  final Duration interval;

  Timer? _timer;
  bool _authenticated = false;
  bool _disposed = false;
  bool _running = false;
  bool _waitForControllerIdle = false;

  Future<void> handleSessionStatus(SessionStatus status) async {
    if (_disposed) return;
    if (status != SessionStatus.authenticated) {
      _authenticated = false;
      _timer?.cancel();
      _timer = null;
      _waitForControllerIdle = false;
      return;
    }
    if (_authenticated) return;
    _authenticated = true;
    await controller.restoreCachedAvailability();
    await controller.expirePendingAvailability();
    await _checkIfDue();
  }

  Future<void> onAppResumed() async {
    if (_disposed || !_authenticated) return;
    await controller.expirePendingAvailability();
    await _checkIfDue();
  }

  Future<void> _checkIfDue() async {
    if (_disposed || !_authenticated || controller.config == null) {
      _timer?.cancel();
      _timer = null;
      return;
    }
    if (_running) return;
    if (controller.shouldDeferAutomaticCheck) {
      _waitForControllerIdle = true;
      return;
    }

    _running = true;
    try {
      await controller.checkIfDue(interval: interval);
    } finally {
      _running = false;
      await _scheduleNext();
    }
  }

  Future<void> _scheduleNext() async {
    _timer?.cancel();
    if (_disposed || !_authenticated || controller.config == null) return;

    final untilCheck = await controller.timeUntilCheckDue(interval: interval);
    if (_disposed || !_authenticated) return;

    Duration? delay = untilCheck;
    final untilExpiry = controller.timeUntilPendingReleaseExpires;
    if (untilExpiry != null && (delay == null || untilExpiry < delay)) {
      delay = untilExpiry;
    }
    if (delay == null) return;

    _timer = Timer(delay, () => unawaited(_onTimer()));
  }

  Future<void> _onTimer() async {
    _timer = null;
    if (_disposed || !_authenticated) return;
    await controller.expirePendingAvailability();
    await _checkIfDue();
  }

  void _controllerChanged() {
    if (_disposed || !_authenticated || !_waitForControllerIdle) return;
    if (controller.shouldDeferAutomaticCheck) return;
    _waitForControllerIdle = false;
    unawaited(_checkIfDue());
  }

  void dispose() {
    if (_disposed) return;
    _disposed = true;
    _timer?.cancel();
    _timer = null;
    controller.removeListener(_controllerChanged);
  }
}
