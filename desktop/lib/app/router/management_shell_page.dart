import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/brand_contrast.dart';
import 'package:avelri_gestao/core/network/erp_access_policy.dart';

class ManagementShellPage extends StatelessWidget {
  const ManagementShellPage({
    required this.location,
    required this.child,
    super.key,
  });

  final String location;
  final Widget child;

  static const _navigationItems = [
    _NavigationItem(
      path: '/dashboard',
      label: 'Visão geral',
      icon: Icons.grid_view_rounded,
    ),
    _NavigationItem(
      path: '/students',
      label: 'Alunos',
      icon: Icons.people_outline_rounded,
    ),
    _NavigationItem(path: '/plans', label: 'Planos', icon: Icons.sell_outlined),
    _NavigationItem(
      path: '/enrollments',
      label: 'Matrículas',
      icon: Icons.assignment_ind_outlined,
    ),
    _NavigationItem(
      path: '/finance',
      label: 'Financeiro',
      icon: Icons.account_balance_wallet_outlined,
    ),
    _NavigationItem(
      path: '/access-control',
      label: 'Controle de acesso',
      icon: Icons.door_front_door_outlined,
    ),
    _NavigationItem(
      path: '/check-in',
      label: 'Check-in',
      icon: Icons.how_to_reg_outlined,
    ),
    _NavigationItem(
      path: '/reports',
      label: 'Relatórios',
      icon: Icons.bar_chart_rounded,
    ),
    _NavigationItem(
      path: '/settings',
      label: 'Configurações',
      icon: Icons.settings_outlined,
    ),
  ];

  bool _isSelected(String path) {
    return location == path || location.startsWith('$path/');
  }

  String get _pageTitle {
    for (final item in _navigationItems) {
      if (_isSelected(item.path)) {
        return item.label;
      }
    }

    return 'Avelri Gestão';
  }

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    final branding = context.watch<BrandingController>().branding;
    final features = context.watch<ErpFeatureController>();
    final gymId = session.user?.gymId;
    final erpEnabled = gymId != null && features.isEnabledForGym(gymId);
    final navigationItems = _navigationItems
        .where((item) => erpEnabled || !ErpAccessPolicy.isErpRoute(item.path))
        .toList();

    final primaryColor = branding.primaryColor;
    final sidebarColor = branding.secondaryColor;
    final sidebarForeground = BrandContrast.foregroundFor(sidebarColor);
    final selectedForeground = BrandContrast.foregroundFor(primaryColor);
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      backgroundColor: theme.scaffoldBackgroundColor,
      body: Row(
        children: [
          Container(
            width: 254,
            color: sidebarColor,
            child: SafeArea(
              child: Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(22, 28, 22, 28),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 48,
                          height: 48,
                          child: branding.logoAsset == null
                              ? Icon(
                                  Icons.fitness_center_rounded,
                                  color: sidebarForeground,
                                  size: 28,
                                )
                              : Image.asset(
                                  branding.logoAsset!,
                                  fit: BoxFit.contain,
                                ),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Text(
                            branding.displayName,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              color: sidebarForeground,
                              fontWeight: FontWeight.w800,
                              fontSize: 16,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 22),
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        'GESTÃO',
                        style: TextStyle(
                          color: sidebarForeground.withValues(alpha: 0.68),
                          fontSize: 10,
                          fontWeight: FontWeight.w800,
                          letterSpacing: 1.6,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Expanded(
                    child: ListView.separated(
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      itemCount: navigationItems.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 4),
                      itemBuilder: (context, index) {
                        final item = navigationItems[index];
                        final selected = _isSelected(item.path);

                        return _SidebarItem(
                          item: item,
                          selected: selected,
                          accentColor: primaryColor,
                          sidebarForeground: sidebarForeground,
                          selectedForeground: selectedForeground,
                          onTap: () {
                            context.go(item.path);
                          },
                        );
                      },
                    ),
                  ),
                  Container(
                    margin: const EdgeInsets.all(14),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: colorScheme.surfaceContainerHighest,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: theme.dividerColor),
                    ),
                    child: Row(
                      children: [
                        CircleAvatar(
                          radius: 18,
                          backgroundColor: sidebarForeground.withValues(
                            alpha: 0.12,
                          ),
                          child: Text(
                            _initials(session.user?.name),
                            style: TextStyle(
                              color: sidebarForeground,
                              fontWeight: FontWeight.w800,
                              fontSize: 12,
                            ),
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                session.user?.name ?? '',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  color: colorScheme.onSurface,
                                  fontSize: 12,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                'Administrador',
                                style: TextStyle(
                                  color: colorScheme.onSurfaceVariant,
                                  fontSize: 10,
                                ),
                              ),
                            ],
                          ),
                        ),
                        IconButton(
                          tooltip: 'Sair',
                          visualDensity: VisualDensity.compact,
                          onPressed: session.logout,
                          icon: Icon(
                            Icons.logout_rounded,
                            color: colorScheme.onSurfaceVariant,
                            size: 19,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          Expanded(
            child: Column(
              children: [
                Container(
                  height: 74,
                  padding: const EdgeInsets.symmetric(horizontal: 30),
                  decoration: BoxDecoration(
                    color: colorScheme.surface,
                    border: Border(
                      bottom: BorderSide(color: theme.dividerColor),
                    ),
                  ),
                  child: Row(
                    children: [
                      Text(
                        _pageTitle,
                        style: TextStyle(
                          color: colorScheme.onSurface,
                          fontSize: 19,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const Spacer(),
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 7,
                        ),
                        decoration: BoxDecoration(
                          color: colorScheme.surfaceContainerHighest,
                          borderRadius: BorderRadius.circular(30),
                          border: Border.all(color: theme.dividerColor),
                        ),
                        child: Row(
                          children: [
                            Text(
                              'Área administrativa',
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
                Expanded(child: child),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _initials(String? name) {
    if (name == null || name.trim().isEmpty) {
      return 'A';
    }

    final parts = name.trim().split(RegExp(r'\s+'));

    if (parts.length == 1) {
      return parts.first.substring(0, 1).toUpperCase();
    }

    return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
  }
}

class _SidebarItem extends StatelessWidget {
  const _SidebarItem({
    required this.item,
    required this.selected,
    required this.accentColor,
    required this.sidebarForeground,
    required this.selectedForeground,
    required this.onTap,
  });

  final _NavigationItem item;
  final bool selected;
  final Color accentColor;
  final Color sidebarForeground;
  final Color selectedForeground;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? accentColor : Colors.transparent,
      borderRadius: BorderRadius.circular(11),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(11),
        child: Container(
          height: 46,
          padding: const EdgeInsets.symmetric(horizontal: 13),
          decoration: selected
              ? BoxDecoration(
                  borderRadius: BorderRadius.circular(11),
                  border: Border(
                    left: BorderSide(color: accentColor, width: 3),
                  ),
                )
              : null,
          child: Row(
            children: [
              Icon(
                item.icon,
                size: 20,
                color: selected
                    ? selectedForeground
                    : sidebarForeground.withValues(alpha: 0.72),
              ),
              const SizedBox(width: 13),
              Expanded(
                child: Text(
                  item.label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: selected
                        ? selectedForeground
                        : sidebarForeground.withValues(alpha: 0.82),
                    fontSize: 13,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NavigationItem {
  const _NavigationItem({
    required this.path,
    required this.label,
    required this.icon,
  });

  final String path;
  final String label;
  final IconData icon;
}
