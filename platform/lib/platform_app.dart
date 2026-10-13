import 'package:flutter/material.dart';

import 'platform_api.dart';
import 'platform_home.dart';
import 'platform_flows.dart';
import 'platform_ui.dart';
export 'platform_flows.dart'
    show ProvisionDialog, AcademyDialog, ActivationPage;

class PlatformApp extends StatelessWidget {
  const PlatformApp({super.key, required this.api, this.activationToken});
  final PlatformApi api;
  final String? activationToken;
  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Avelri Platform',
    debugShowCheckedModeBanner: false,
    theme: platformTheme(),
    home: activationToken == null
        ? PlatformHome(api: api)
        : ActivationPage(api: api, token: activationToken!),
  );
}
