import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/session/session_user.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:provider/provider.dart';

import 'test_session_storage.dart';

/// Standalone layout-test providers; authorization is exercised separately with
/// the real session, feature controller and router.
class ManagementTestScope extends StatelessWidget {
  const ManagementTestScope({
    required this.api,
    required this.child,
    this.erpEnabled = true,
    super.key,
  });
  final ApiClient api;
  final Widget child;
  final bool erpEnabled;

  @override
  Widget build(BuildContext context) => MultiProvider(
    providers: [
      Provider.value(value: api),
      ChangeNotifierProvider<SessionController>(
        create: (_) => _LayoutSession(api),
      ),
      ChangeNotifierProvider<ErpFeatureController>(
        create: (_) => _LayoutFeatures(api, erpEnabled),
      ),
    ],
    child: child,
  );
}

class _LayoutSession extends SessionController {
  _LayoutSession(ApiClient api)
    : super(
        TokenStorage(
          const FlutterSecureStorage(),
          restorationGuard: TestSessionRestorationGuard(),
        ),
        AuthService(api),
        api,
      );
  @override
  SessionStatus get status => SessionStatus.authenticated;
  @override
  SessionUser get user => const SessionUser(
    userId: 'layout-user',
    gymId: 'layout-gym',
    name: 'Layout Admin',
    email: 'admin@example.invalid',
    role: AppRole.admin,
  );
}

class _LayoutFeatures extends ErpFeatureController {
  _LayoutFeatures(super.apiClient, this.enabled);
  final bool enabled;
  @override
  bool isEnabledForGym(String gymId) => gymId == 'layout-gym' && enabled;
}
