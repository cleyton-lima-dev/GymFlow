import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';

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
    _NavigationItem(
      path: '/plans',
      label: 'Planos',
      icon: Icons.sell_outlined,
    ),
    _NavigationItem(
      path: '/finance',
      label: 'Financeiro',
      icon: Icons.account_balance_wallet_outlined,
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

    final primaryColor = branding.primaryColor;

    return Scaffold(
      backgroundColor: const Color(0xFFF5F6FA),
      body: Row(
        children: [
          Container(
            width: 254,
            color: const Color(0xFF111425),
            child: SafeArea(
              child: Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(
                      22,
                      28,
                      22,
                      28,
                    ),
                    child: Row(
                      children: [
                        Container(
                          width: 44,
                          height: 44,
                          padding: const EdgeInsets.all(6),
                          decoration: BoxDecoration(
                            color: primaryColor,
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: branding.logoAsset == null
                              ? const Icon(
                            Icons.fitness_center_rounded,
                            color: Colors.white,
                            size: 23,
                          )
                              : Image.asset(
                            branding.logoAsset!,
                            fit: BoxFit.contain,
                          ),
                        ),
                        const SizedBox(width: 13),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'AVELRI',
                                style: TextStyle(
                                  color: Colors.white,
                                  fontWeight: FontWeight.w900,
                                  fontSize: 17,
                                  letterSpacing: 1.2,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                branding.displayName,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: Color(0xFF9499AD),
                                  fontSize: 11,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 22),
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        'GESTÃO',
                        style: TextStyle(
                          color: Color(0xFF666C82),
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
                      itemCount: _navigationItems.length,
                      separatorBuilder: (_, _) =>
                      const SizedBox(height: 4),
                      itemBuilder: (context, index) {
                        final item = _navigationItems[index];
                        final selected = _isSelected(item.path);

                        return _SidebarItem(
                          item: item,
                          selected: selected,
                          accentColor: primaryColor,
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
                      color: const Color(0xFF191D31),
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(
                        color: const Color(0xFF292E45),
                      ),
                    ),
                    child: Row(
                      children: [
                        CircleAvatar(
                          radius: 18,
                          backgroundColor:
                          primaryColor.withValues(alpha: 0.18),
                          child: Text(
                            _initials(session.user?.name),
                            style: TextStyle(
                              color: primaryColor,
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
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 12,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              const SizedBox(height: 2),
                              const Text(
                                'Administrador',
                                style: TextStyle(
                                  color: Color(0xFF8E94A9),
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
                          icon: const Icon(
                            Icons.logout_rounded,
                            color: Color(0xFF9BA1B5),
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
                  decoration: const BoxDecoration(
                    color: Colors.white,
                    border: Border(
                      bottom: BorderSide(
                        color: Color(0xFFE8EAF1),
                      ),
                    ),
                  ),
                  child: Row(
                    children: [
                      Text(
                        _pageTitle,
                        style: const TextStyle(
                          color: Color(0xFF171A2C),
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
                          color: const Color(0xFFF4F5F9),
                          borderRadius: BorderRadius.circular(30),
                          border: Border.all(
                            color: const Color(0xFFE5E7EF),
                          ),
                        ),
                        child: Row(
                          children: [
                            Container(
                              width: 7,
                              height: 7,
                              decoration: const BoxDecoration(
                                color: Color(0xFF2CB67D),
                                shape: BoxShape.circle,
                              ),
                            ),
                            const SizedBox(width: 7),
                            const Text(
                              'Online',
                              style: TextStyle(
                                color: Color(0xFF656A7E),
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
    required this.onTap,
  });

  final _NavigationItem item;
  final bool selected;
  final Color accentColor;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected
          ? accentColor.withValues(alpha: 0.14)
          : Colors.transparent,
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
              left: BorderSide(
                color: accentColor,
                width: 3,
              ),
            ),
          )
              : null,
          child: Row(
            children: [
              Icon(
                item.icon,
                size: 20,
                color: selected
                    ? accentColor
                    : const Color(0xFF8D93A8),
              ),
              const SizedBox(width: 13),
              Text(
                item.label,
                style: TextStyle(
                  color: selected
                      ? Colors.white
                      : const Color(0xFFB0B5C5),
                  fontSize: 13,
                  fontWeight:
                  selected ? FontWeight.w700 : FontWeight.w500,
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