import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';

import 'dashboard_card_grid.dart';
import 'dashboard_metrics.dart';

class DashboardPage extends StatelessWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context) {
    final user = context.watch<SessionController>().user;
    final erpEnabled =
        user != null &&
        context.watch<ErpFeatureController>().isEnabledForGym(user.gymId);
    final branding = context.watch<BrandingController>().branding;
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final shortcuts = [
      _QuickAccessCard(
        title: 'Alunos',
        description: 'Cadastros e informações dos alunos',
        icon: Icons.people_outline_rounded,
        accentColor: branding.primaryColor,
        onTap: () => context.go('/students'),
      ),
      if (erpEnabled) ...[
        _QuickAccessCard(
          title: 'Planos',
          description: 'Planos e matrículas da academia',
          icon: Icons.sell_outlined,
          accentColor: branding.primaryColor,
          onTap: () => context.go('/plans'),
        ),
        _QuickAccessCard(
          title: 'Financeiro',
          description: 'Mensalidades, pagamentos e histórico',
          icon: Icons.account_balance_wallet_outlined,
          accentColor: branding.primaryColor,
          onTap: () => context.go('/finance'),
        ),
      ],
      _QuickAccessCard(
        title: 'Check-in',
        description: 'Frequência e presença dos alunos',
        icon: Icons.how_to_reg_outlined,
        accentColor: branding.primaryColor,
        onTap: () => context.go('/check-in'),
      ),
    ];

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1250),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Olá, ${_firstName(user?.name)}.',
              style: TextStyle(
                color: colorScheme.onSurface,
                fontSize: 28,
                fontWeight: FontWeight.w800,
                letterSpacing: -0.5,
              ),
            ),
            const SizedBox(height: 7),
            Text(
              'Bem-vindo ao painel de gestão da ${branding.displayName}.',
              style: TextStyle(
                color: colorScheme.onSurfaceVariant,
                fontSize: 14,
              ),
            ),
            const SizedBox(height: 28),
            DashboardMetrics(erpEnabled: erpEnabled),
            const SizedBox(height: 20),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(26),
              decoration: BoxDecoration(
                color: colorScheme.surface,
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: theme.dividerColor),
              ),
              child: Row(
                children: [
                  Container(
                    width: 58,
                    height: 58,
                    padding: const EdgeInsets.all(9),
                    decoration: BoxDecoration(
                      color: branding.primaryColor.withValues(alpha: 0.10),
                      borderRadius: BorderRadius.circular(15),
                    ),
                    child: branding.logoAsset != null
                        ? Image.asset(branding.logoAsset!, fit: BoxFit.contain)
                        : Icon(
                            Icons.business_rounded,
                            color: branding.primaryColor,
                          ),
                  ),
                  const SizedBox(width: 18),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          branding.displayName,
                          style: TextStyle(
                            color: colorScheme.onSurface,
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 5),
                        Text(
                          'Gerencie as rotinas da academia pelo Avelri.',
                          style: TextStyle(
                            color: colorScheme.onSurfaceVariant,
                            fontSize: 13,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 8,
                    ),
                    decoration: BoxDecoration(
                      color: colorScheme.surfaceContainerHighest,
                      borderRadius: BorderRadius.circular(30),
                    ),
                    child: Row(
                      children: [
                        Icon(
                          Icons.dashboard_outlined,
                          color: colorScheme.onSurfaceVariant,
                          size: 16,
                        ),
                        const SizedBox(width: 6),
                        Text(
                          'Painel de gestão',
                          style: TextStyle(
                            color: colorScheme.onSurfaceVariant,
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 32),
            Text(
              'Acesso rápido',
              style: TextStyle(
                color: colorScheme.onSurface,
                fontSize: 17,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 15),
            DashboardCardGrid(
              key: const ValueKey('dashboard-shortcut-grid'),
              itemCount: shortcuts.length,
              minItemWidth: 268,
              itemHeight: 150,
              itemBuilder: (context, index) => shortcuts[index],
            ),
            const SizedBox(height: 32),
            Container(
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: branding.primaryColor.withValues(alpha: 0.10),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: branding.primaryColor.withValues(alpha: 0.22),
                ),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.info_outline_rounded,
                    color: branding.primaryColor,
                    size: 21,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      'Novos módulos de gestão serão liberados gradualmente conforme a evolução do sistema.',
                      style: TextStyle(
                        color: colorScheme.onSurfaceVariant,
                        fontSize: 13,
                        height: 1.4,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _firstName(String? name) {
    if (name == null || name.trim().isEmpty) {
      return 'Administrador';
    }

    return name.trim().split(RegExp(r'\s+')).first;
  }
}

class _QuickAccessCard extends StatefulWidget {
  const _QuickAccessCard({
    required this.title,
    required this.description,
    required this.icon,
    required this.accentColor,
    required this.onTap,
  });

  final String title;
  final String description;
  final IconData icon;
  final Color accentColor;
  final VoidCallback onTap;

  @override
  State<_QuickAccessCard> createState() => _QuickAccessCardState();
}

class _QuickAccessCardState extends State<_QuickAccessCard> {
  bool _hovering = false;
  bool _showFocusHighlight = false;
  late final FocusNode _focusNode = FocusNode(
    debugLabel: 'Dashboard shortcut: ${widget.title}',
  )..addListener(_updateFocusHighlight);

  void _updateFocusHighlight() {
    if (_showFocusHighlight != _focusNode.hasFocus) {
      setState(() => _showFocusHighlight = _focusNode.hasFocus);
    }
  }

  @override
  void dispose() {
    _focusNode.removeListener(_updateFocusHighlight);
    _focusNode.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        key: ValueKey('dashboard-shortcut-${widget.title}'),
        focusNode: _focusNode,
        onTap: widget.onTap,
        onHover: (hovering) {
          if (_hovering != hovering) setState(() => _hovering = hovering);
        },
        mouseCursor: SystemMouseCursors.click,
        borderRadius: BorderRadius.circular(16),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 140),
          padding: const EdgeInsets.all(20),
          transform: Matrix4.translationValues(0, _hovering ? -3 : 0, 0),
          decoration: BoxDecoration(
            color: colorScheme.surface,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: _showFocusHighlight
                  ? widget.accentColor
                  : _hovering
                  ? widget.accentColor.withValues(alpha: 0.45)
                  : theme.dividerColor,
              width: _showFocusHighlight ? 2 : 1,
            ),
            boxShadow: _hovering
                ? const [
                    BoxShadow(
                      color: Color(0x0D111425),
                      blurRadius: 18,
                      offset: Offset(0, 8),
                    ),
                  ]
                : const [],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: widget.accentColor.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(widget.icon, color: widget.accentColor, size: 20),
              ),
              const Spacer(),
              Text(
                widget.title,
                style: TextStyle(
                  color: colorScheme.onSurface,
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                widget.description,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: colorScheme.onSurfaceVariant,
                  fontSize: 11,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
