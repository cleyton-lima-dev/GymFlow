import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_draft.dart';
import 'package:gymflow/features/workout_templates/presentation/configure_workout_template_day_page.dart';
import 'package:gymflow/features/workout_templates/presentation/edit_workout_template_view_model.dart';
import 'package:provider/provider.dart';

class EditWorkoutTemplatePage extends StatelessWidget {
  const EditWorkoutTemplatePage({
    required this.templateId,
    this.onUpdated,
    super.key,
  });

  final String templateId;
  final VoidCallback? onUpdated;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => EditWorkoutTemplateViewModel(
        WorkoutTemplatesService(
          context.read<ApiClient>(),
        ),
        templateId,
      )..load(),
      child: _EditWorkoutTemplateView(
        onUpdated: onUpdated,
      ),
    );
  }
}

class _EditWorkoutTemplateView extends StatelessWidget {
  const _EditWorkoutTemplateView({
    required this.onUpdated,
  });

  final VoidCallback? onUpdated;

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<EditWorkoutTemplateViewModel>();

    return Scaffold(
      body: SafeArea(
        child: viewModel.isLoading && !viewModel.hasLoaded
            ? const _LoadingView()
            : !viewModel.hasLoaded
            ? _LoadErrorView(
          message: viewModel.errorMessage ??
              'Não foi possível carregar o modelo.',
          onRetry: viewModel.retryLoad,
        )
            : _EditForm(
          viewModel: viewModel,
          onUpdated: onUpdated,
        ),
      ),
    );
  }
}

class _EditForm extends StatelessWidget {
  const _EditForm({
    required this.viewModel,
    required this.onUpdated,
  });

  final EditWorkoutTemplateViewModel viewModel;
  final VoidCallback? onUpdated;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
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
                'Editar modelo',
                style:
                theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),

              const SizedBox(height: 6),

              Text(
                'Atualize os dados, dias e exercícios deste modelo de treino.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                  height: 1.4,
                ),
              ),

              const SizedBox(height: 26),

              TextFormField(
                initialValue: viewModel.name,
                textCapitalization:
                TextCapitalization.sentences,
                onChanged: viewModel.setName,
                decoration: const InputDecoration(
                  labelText: 'Nome do modelo',
                ),
              ),

              const SizedBox(height: 16),

              TextFormField(
                initialValue: viewModel.description,
                minLines: 3,
                maxLines: 5,
                textCapitalization:
                TextCapitalization.sentences,
                onChanged: viewModel.setDescription,
                decoration: const InputDecoration(
                  labelText: 'Descrição',
                  hintText:
                  'Adicione uma descrição opcional',
                  alignLabelWithHint: true,
                ),
              ),

              const SizedBox(height: 18),

              Container(
                decoration: BoxDecoration(
                  color: colorScheme.surface,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(
                    color:
                    colorScheme.outlineVariant.withAlpha(120),
                  ),
                ),
                child: SwitchListTile(
                  value: viewModel.isActive,
                  onChanged: viewModel.setIsActive,
                  secondary: Icon(
                    viewModel.isActive
                        ? Icons.check_circle_outline_rounded
                        : Icons.pause_circle_outline_rounded,
                    color: viewModel.isActive
                        ? colorScheme.primary
                        : colorScheme.onSurfaceVariant,
                  ),
                  title: Text(
                    viewModel.isActive
                        ? 'Modelo ativo'
                        : 'Modelo inativo',
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  subtitle: Text(
                    viewModel.isActive
                        ? 'O modelo está disponível para uso.'
                        : 'O modelo ficará indisponível para novos usos.',
                  ),
                ),
              ),

              const SizedBox(height: 30),

              Row(
                children: [
                  Expanded(
                    child: Text(
                      'Dias de treino',
                      style:
                      theme.textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  Text(
                    _daysLabel(viewModel.days.length),
                    style:
                    theme.textTheme.bodyMedium?.copyWith(
                      color:
                      colorScheme.onSurfaceVariant,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),

              const SizedBox(height: 6),

              Text(
                'Toque em um dia para editar sua configuração.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
              ),

              const SizedBox(height: 16),

              if (viewModel.days.isEmpty)
                const _EmptyDaysCard()
              else
                for (var index = 0;
                index < viewModel.days.length;
                index++) ...[
                  _DraftDayCard(
                    day: viewModel.days[index],
                    position: index + 1,
                    onTap: () => _editDay(
                      context,
                      index,
                    ),
                    onRemove: () =>
                        viewModel.removeDay(index),
                  ),
                  if (index <
                      viewModel.days.length - 1)
                    const SizedBox(height: 12),
                ],

              const SizedBox(height: 14),

              OutlinedButton.icon(
                onPressed: () => _addDay(context),
                icon: const Icon(
                  Icons.add_rounded,
                ),
                label: const Text(
                  'Adicionar dia',
                ),
              ),

              if (viewModel.errorMessage != null) ...[
                const SizedBox(height: 18),
                _InlineError(
                  message: viewModel.errorMessage!,
                ),
              ],

              const SizedBox(height: 30),

              FilledButton.icon(
                onPressed: viewModel.isSubmitting
                    ? null
                    : () => _submit(context),
                icon: viewModel.isSubmitting
                    ? const SizedBox(
                  width: 18,
                  height: 18,
                  child:
                  CircularProgressIndicator(
                    strokeWidth: 2,
                  ),
                )
                    : const Icon(
                  Icons.check_rounded,
                ),
                label: Text(
                  viewModel.isSubmitting
                      ? 'Salvando...'
                      : 'Salvar alterações',
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _addDay(
      BuildContext context,
      ) async {
    final day =
    await Navigator.of(context)
        .push<WorkoutTemplateDraftDay>(
      MaterialPageRoute(
        builder: (_) =>
            ConfigureWorkoutTemplateDayPage(
              dayNumber: viewModel.days.length + 1,
            ),
      ),
    );

    if (day == null) {
      return;
    }

    viewModel.addDay(day);
  }

  Future<void> _editDay(
      BuildContext context,
      int dayIndex,
      ) async {
    final currentDay = viewModel.days[dayIndex];

    final editedDay =
    await Navigator.of(context)
        .push<WorkoutTemplateDraftDay>(
      MaterialPageRoute(
        builder: (_) =>
            ConfigureWorkoutTemplateDayPage(
              dayNumber: dayIndex + 1,
              initialDay: currentDay,
            ),
      ),
    );

    if (editedDay == null) {
      return;
    }

    viewModel.replaceDay(
      dayIndex,
      editedDay,
    );
  }

  Future<void> _submit(
      BuildContext context,
      ) async {
    final updated = await viewModel.submit();

    if (!context.mounted || !updated) {
      return;
    }

    onUpdated?.call();
  }
}

class _DraftDayCard extends StatelessWidget {
  const _DraftDayCard({
    required this.day,
    required this.position,
    required this.onTap,
    required this.onRemove,
  });

  final WorkoutTemplateDraftDay day;
  final int position;
  final VoidCallback onTap;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Material(
      color: colorScheme.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            borderRadius:
            BorderRadius.circular(16),
            border: Border.all(
              color: colorScheme.outlineVariant
                  .withAlpha(120),
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 40,
                height: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color:
                  colorScheme.primary.withAlpha(
                    18,
                  ),
                ),
                child: Text(
                  '$position',
                  style: TextStyle(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),

              const SizedBox(width: 12),

              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Text(
                      day.name,
                      style: theme
                          .textTheme.titleMedium
                          ?.copyWith(
                        fontWeight:
                        FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      _exerciseLabel(
                        day.exercises.length,
                      ),
                      style: theme
                          .textTheme.bodySmall
                          ?.copyWith(
                        color: colorScheme
                            .onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),

              IconButton(
                tooltip: 'Remover dia',
                onPressed: onRemove,
                icon: const Icon(
                  Icons.delete_outline_rounded,
                ),
              ),

              Icon(
                Icons.chevron_right_rounded,
                color:
                colorScheme.onSurfaceVariant,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmptyDaysCard extends StatelessWidget {
  const _EmptyDaysCard();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 20,
        vertical: 24,
      ),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color:
          colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color:
              colorScheme.primary.withAlpha(18),
            ),
            child: Icon(
              Icons.calendar_view_day_outlined,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(height: 12),

          Text(
            'Nenhum dia configurado',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 5),

          Text(
            'Adicione um dia para montar este modelo.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.error.withAlpha(15),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: colorScheme.error.withAlpha(70),
        ),
      ),
      child: Row(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.error_outline_rounded,
            color: colorScheme.error,
            size: 20,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                color: colorScheme.error,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _LoadingView extends StatelessWidget {
  const _LoadingView();

  @override
  Widget build(BuildContext context) {
    return const Column(
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            12,
            20,
            0,
          ),
          child: ProfessorAdminPageHeader(),
        ),
        Expanded(
          child: Center(
            child: CircularProgressIndicator(),
          ),
        ),
      ],
    );
  }
}

class _LoadErrorView extends StatelessWidget {
  const _LoadErrorView({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return Column(
      children: [
        const Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            12,
            20,
            0,
          ),
          child: ProfessorAdminPageHeader(),
        ),
        Expanded(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisAlignment:
              MainAxisAlignment.center,
              children: [
                Icon(
                  Icons.error_outline_rounded,
                  size: 42,
                  color: colorScheme.error,
                ),
                const SizedBox(height: 14),
                Text(
                  message,
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 18),
                OutlinedButton(
                  onPressed: onRetry,
                  child: const Text(
                    'Tentar novamente',
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

String _daysLabel(int count) {
  return count == 1
      ? '1 dia'
      : '$count dias';
}

String _exerciseLabel(int count) {
  return count == 1
      ? '1 exercício'
      : '$count exercícios';
}
