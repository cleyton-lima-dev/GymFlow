import 'package:flutter/material.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class CreateOrAssignWorkoutPage extends StatelessWidget {
  const CreateOrAssignWorkoutPage({
    required this.studentName,
    required this.onUseTemplateTap,
    required this.onCreateManualTap,
    super.key,
  });

  final String studentName;

  final Future<bool> Function() onUseTemplateTap;
  final Future<bool> Function() onCreateManualTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
      body: SafeArea(
        bottom: false,
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            20,
            12,
            20,
            32,
          ),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(
                maxWidth: 680,
              ),
              child: Column(
                crossAxisAlignment:
                CrossAxisAlignment.stretch,
                children: [
                  const ProfessorAdminPageHeader(),

                  const SizedBox(height: 24),

                  Text(
                    'Criar treino',
                    style:
                    theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 6),

                  Text(
                    'Escolha como deseja montar o treino deste aluno.',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                      height: 1.4,
                    ),
                  ),

                  const SizedBox(height: 20),

                  _StudentCard(
                    studentName: studentName,
                  ),

                  const SizedBox(height: 28),

                  Text(
                    'Como deseja criar o treino?',
                    style:
                    theme.textTheme.titleLarge?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 14),

                  _OptionCard(
                    icon: Icons.content_copy_rounded,
                    title: 'Usar modelo de treino',
                    description:
                    'Escolha um modelo ativo já cadastrado e use sua estrutura para criar o treino.',
                    buttonLabel: 'Escolher modelo',
                    onTap: () => _runFlow(
                      context,
                      onUseTemplateTap,
                    ),
                  ),

                  const SizedBox(height: 14),

                  _OptionCard(
                    icon:
                    Icons.edit_note_rounded,
                    title:
                    'Criar treino manualmente',
                    description:
                    'Monte o treino do zero, configurando suas divisões e exercícios.',
                    buttonLabel:
                    'Criar manualmente',
                    onTap: () => _runFlow(
                      context,
                      onCreateManualTap,
                    ),
                  ),

                  const SizedBox(height: 22),

                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color:
                      colorScheme.primary.withAlpha(14),
                      borderRadius:
                      BorderRadius.circular(14),
                    ),
                    child: Row(
                      crossAxisAlignment:
                      CrossAxisAlignment.start,
                      children: [
                        Icon(
                          Icons.info_outline_rounded,
                          size: 20,
                          color: colorScheme.primary,
                        ),

                        const SizedBox(width: 10),

                        Expanded(
                          child: Text(
                            'Os dois caminhos criam um treino vinculado diretamente a este aluno.',
                            style: theme
                                .textTheme.bodySmall
                                ?.copyWith(
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
          ),
        ),
      ),
    );
  }

  Future<void> _runFlow(
      BuildContext context,
      Future<bool> Function() action,
      ) async {
    final created = await action();

    if (!context.mounted || !created) {
      return;
    }

    Navigator.of(context).pop(true);
  }
}

class _StudentCard extends StatelessWidget {
  const _StudentCard({
    required this.studentName,
  });

  final String studentName;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color:
          colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color:
              colorScheme.primary.withAlpha(18),
            ),
            child: Text(
              _initials(studentName),
              style:
              theme.textTheme.titleMedium?.copyWith(
                color: colorScheme.primary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment:
              CrossAxisAlignment.start,
              children: [
                Text(
                  studentName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style:
                  theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),

                const SizedBox(height: 3),

                Text(
                  'Aluno',
                  style:
                  theme.textTheme.bodySmall?.copyWith(
                    color:
                    colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _OptionCard extends StatelessWidget {
  const _OptionCard({
    required this.icon,
    required this.title,
    required this.description,
    required this.buttonLabel,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String description;
  final String buttonLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color:
          colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        crossAxisAlignment:
        CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment:
            CrossAxisAlignment.start,
            children: [
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color:
                  colorScheme.primary.withAlpha(
                    18,
                  ),
                ),
                child: Icon(
                  icon,
                  color: colorScheme.primary,
                ),
              ),

              const SizedBox(width: 14),

              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: theme
                          .textTheme.titleMedium
                          ?.copyWith(
                        fontWeight:
                        FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 5),

                    Text(
                      description,
                      style: theme
                          .textTheme.bodyMedium
                          ?.copyWith(
                        color: colorScheme
                            .onSurfaceVariant,
                        height: 1.4,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),

          const SizedBox(height: 18),

          FilledButton(
            onPressed: onTap,
            child: Text(
              buttonLabel,
            ),
          ),
        ],
      ),
    );
  }
}

String _initials(String name) {
  final parts = name
      .trim()
      .split(RegExp(r'\s+'))
      .where((part) => part.isNotEmpty)
      .toList();

  if (parts.isEmpty) {
    return '?';
  }

  if (parts.length == 1) {
    return parts.first
        .substring(0, 1)
        .toUpperCase();
  }

  return '${parts.first.substring(0, 1)}'
      '${parts.last.substring(0, 1)}'
      .toUpperCase();
}
