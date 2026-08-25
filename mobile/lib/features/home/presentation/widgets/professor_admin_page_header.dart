import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:provider/provider.dart';

class ProfessorAdminPageHeader extends StatelessWidget {
  const ProfessorAdminPageHeader({
    this.height = 82,
    this.onBack,
    super.key,
  });

  final double height;
  final VoidCallback? onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: height,
      width: double.infinity,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Positioned.fill(
            child: Center(
              child: ProfessorAdminBrandHeader(
                height: height,
              ),
            ),
          ),

          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: onBack ?? () => _goBack(context),
              icon: const Icon(
                Icons.chevron_left_rounded,
              ),
              label: const Text('Voltar'),
            ),
          ),
        ],
      ),
    );
  }

  void _goBack(BuildContext context) {
    if (context.canPop()) {
      context.pop();
      return;
    }

    final user = context.read<SessionController>().user;

    if (user == null) {
      return;
    }

    final homePath = switch (user.role) {
      AppRole.admin => '/admin',
      AppRole.professor => '/professor',
      AppRole.student => null,
    };

    if (homePath != null) {
      context.go(homePath);
    }
  }
}
