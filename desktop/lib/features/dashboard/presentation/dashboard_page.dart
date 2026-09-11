import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';

class DashboardPage extends StatelessWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context) {
    final user = context.watch<SessionController>().user;
    final branding = context.watch<BrandingController>().branding;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1250),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Olá, ${_firstName(user?.name)}.',
              style: const TextStyle(
                color: Color(0xFF171A2C),
                fontSize: 28,
                fontWeight: FontWeight.w800,
                letterSpacing: -0.5,
              ),
            ),
            const SizedBox(height: 7),
            Text(
              'Bem-vindo ao painel de gestão da ${branding.displayName}.',
              style: const TextStyle(
                color: Color(0xFF74798D),
                fontSize: 14,
              ),
            ),
            const SizedBox(height: 28),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(26),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(18),
                border: Border.all(
                  color: const Color(0xFFE5E7EF),
                ),
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
                        ? Image.asset(
                      branding.logoAsset!,
                      fit: BoxFit.contain,
                    )
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
                          style: const TextStyle(
                            color: Color(0xFF171A2C),
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 5),
                        const Text(
                          'Sua área de gestão está conectada e pronta para uso.',
                          style: TextStyle(
                            color: Color(0xFF74798D),
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
                      color: const Color(0xFFEAF8F2),
                      borderRadius: BorderRadius.circular(30),
                    ),
                    child: const Row(
                      children: [
                        Icon(
                          Icons.check_circle_rounded,
                          color: Color(0xFF218C63),
                          size: 16,
                        ),
                        SizedBox(width: 6),
                        Text(
                          'Sistema ativo',
                          style: TextStyle(
                            color: Color(0xFF218C63),
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
            const Text(
              'Acesso rápido',
              style: TextStyle(
                color: Color(0xFF24273A),
                fontSize: 17,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 15),
            Wrap(
              spacing: 16,
              runSpacing: 16,
              children: [
                _QuickAccessCard(
                  title: 'Alunos',
                  description: 'Cadastros e informações dos alunos',
                  icon: Icons.people_outline_rounded,
                  accentColor: branding.primaryColor,
                  onTap: () => context.go('/students'),
                ),
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
                _QuickAccessCard(
                  title: 'Check-in',
                  description: 'Frequência e presença dos alunos',
                  icon: Icons.how_to_reg_outlined,
                  accentColor: branding.primaryColor,
                  onTap: () => context.go('/check-in'),
                ),
              ],
            ),
            const SizedBox(height: 32),
            Container(
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: const Color(0xFFF0EEFA),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: const Color(0xFFE1DCF5),
                ),
              ),
              child: const Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.info_outline_rounded,
                    color: Color(0xFF5B42AF),
                    size: 21,
                  ),
                  SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      'Os módulos de gestão serão liberados gradualmente conforme a evolução do Avelri Gestão.',
                      style: TextStyle(
                        color: Color(0xFF574F73),
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

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) {
        setState(() {
          _hovering = true;
        });
      },
      onExit: (_) {
        setState(() {
          _hovering = false;
        });
      },
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 140),
          width: 268,
          height: 150,
          padding: const EdgeInsets.all(20),
          transform: Matrix4.translationValues(
            0,
            _hovering ? -3 : 0,
            0,
          ),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: _hovering
                  ? widget.accentColor.withValues(alpha: 0.35)
                  : const Color(0xFFE5E7EF),
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
                  color:
                  widget.accentColor.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(
                  widget.icon,
                  color: widget.accentColor,
                  size: 20,
                ),
              ),
              const Spacer(),
              Text(
                widget.title,
                style: const TextStyle(
                  color: Color(0xFF202336),
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                widget.description,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Color(0xFF7B8093),
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