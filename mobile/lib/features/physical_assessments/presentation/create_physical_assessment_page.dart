import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/presentation/create_physical_assessment_view_model.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class CreatePhysicalAssessmentPage extends StatelessWidget {
  const CreatePhysicalAssessmentPage({
    required this.studentId,
    required this.studentName,
    super.key,
  });

  final String studentId;
  final String studentName;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => CreatePhysicalAssessmentViewModel(
        PhysicalAssessmentsService(
          context.read<ApiClient>(),
        ),
        studentId,
      ),
      child: _CreatePhysicalAssessmentView(
        studentName: studentName,
      ),
    );
  }
}

class _CreatePhysicalAssessmentView extends StatefulWidget {
  const _CreatePhysicalAssessmentView({
    required this.studentName,
  });

  final String studentName;

  @override
  State<_CreatePhysicalAssessmentView> createState() =>
      _CreatePhysicalAssessmentViewState();
}

class _CreatePhysicalAssessmentViewState
    extends State<_CreatePhysicalAssessmentView> {
  final _formKey = GlobalKey<FormState>();

  late DateTime _assessmentDate;

  final _weightController = TextEditingController();
  final _heightController = TextEditingController();
  final _bodyFatController = TextEditingController();

  final _chestController = TextEditingController();
  final _waistController = TextEditingController();
  final _abdomenController = TextEditingController();
  final _hipController = TextEditingController();

  final _rightArmController = TextEditingController();
  final _leftArmController = TextEditingController();

  final _rightThighController = TextEditingController();
  final _leftThighController = TextEditingController();

  final _rightCalfController = TextEditingController();
  final _leftCalfController = TextEditingController();

  final _notesController = TextEditingController();

  @override
  void initState() {
    super.initState();

    final now = DateTime.now();

    _assessmentDate = DateTime(
      now.year,
      now.month,
      now.day,
    );
  }

  @override
  void dispose() {
    _weightController.dispose();
    _heightController.dispose();
    _bodyFatController.dispose();

    _chestController.dispose();
    _waistController.dispose();
    _abdomenController.dispose();
    _hipController.dispose();

    _rightArmController.dispose();
    _leftArmController.dispose();

    _rightThighController.dispose();
    _leftThighController.dispose();

    _rightCalfController.dispose();
    _leftCalfController.dispose();

    _notesController.dispose();

    super.dispose();
  }

  Future<void> _selectAssessmentDate() async {
    final now = DateTime.now();

    final selectedDate = await showDatePicker(
      context: context,
      initialDate: _assessmentDate,
      firstDate: DateTime(2000),
      lastDate: DateTime(
        now.year,
        now.month,
        now.day,
      ),
    );

    if (selectedDate == null || !mounted) {
      return;
    }

    setState(() {
      _assessmentDate = selectedDate;
    });
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();

    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final viewModel =
    context.read<CreatePhysicalAssessmentViewModel>();

    final success = await viewModel.submit(
      assessmentDate: _assessmentDate,
      weightKg: _weightController.text,
      heightCm: _heightController.text,
      bodyFatPercentage: _bodyFatController.text,
      chestCm: _chestController.text,
      waistCm: _waistController.text,
      abdomenCm: _abdomenController.text,
      hipCm: _hipController.text,
      rightArmCm: _rightArmController.text,
      leftArmCm: _leftArmController.text,
      rightThighCm: _rightThighController.text,
      leftThighCm: _leftThighController.text,
      rightCalfCm: _rightCalfController.text,
      leftCalfCm: _leftCalfController.text,
      notes: _notesController.text,
    );

    if (!mounted || !success) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'Avaliação física cadastrada com sucesso.',
        ),
      ),
    );

    Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<CreatePhysicalAssessmentViewModel>();

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: CustomScrollView(
            keyboardDismissBehavior:
            ScrollViewKeyboardDismissBehavior.onDrag,
            slivers: [
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(
                    20,
                    12,
                    20,
                    32,
                  ),
                  child: Column(
                    crossAxisAlignment:
                    CrossAxisAlignment.stretch,
                    children: [
                      const ProfessorAdminPageHeader(),

                      const SizedBox(height: 16),

                      Text(
                        'Nova avaliação física',
                        style: Theme.of(context)
                            .textTheme
                            .headlineSmall
                            ?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),

                      const SizedBox(height: 4),

                      Text(
                        widget.studentName,
                        style: Theme.of(context)
                            .textTheme
                            .bodyLarge
                            ?.copyWith(
                          color: Theme.of(context)
                              .colorScheme
                              .onSurfaceVariant,
                        ),
                      ),

                      const SizedBox(height: 20),

                      const _InformationCard(),

                      const SizedBox(height: 20),

                      _FormSection(
                        title: 'Informações básicas',
                        icon: Icons.assignment_outlined,
                        child: Column(
                          children: [
                            _AssessmentDateField(
                              date: _assessmentDate,
                              enabled:
                              !viewModel.isSubmitting,
                              onTap:
                              _selectAssessmentDate,
                            ),

                            const SizedBox(height: 16),

                            _DecimalField(
                              controller:
                              _weightController,
                              label: 'Peso',
                              suffixText: 'kg',
                              requiredField: true,
                              enabled:
                              !viewModel.isSubmitting,
                            ),

                            const SizedBox(height: 16),

                            _DecimalField(
                              controller:
                              _heightController,
                              label: 'Altura',
                              suffixText: 'cm',
                              requiredField: true,
                              enabled:
                              !viewModel.isSubmitting,
                            ),

                            const SizedBox(height: 16),

                            _DecimalField(
                              controller:
                              _bodyFatController,
                              label: '% de gordura',
                              suffixText: '%',
                              maxValue: 100,
                              enabled:
                              !viewModel.isSubmitting,
                            ),
                          ],
                        ),
                      ),

                      const SizedBox(height: 20),

                      _FormSection(
                        title: 'Medidas corporais',
                        icon: Icons.straighten_rounded,
                        child: Column(
                          crossAxisAlignment:
                          CrossAxisAlignment.stretch,
                          children: [
                            Text(
                              'Preencha somente as medidas '
                                  'realizadas nesta avaliação.',
                              style: Theme.of(context)
                                  .textTheme
                                  .bodySmall
                                  ?.copyWith(
                                color: Theme.of(context)
                                    .colorScheme
                                    .onSurfaceVariant,
                              ),
                            ),

                            const SizedBox(height: 16),

                            _MeasurementRow(
                              left: _DecimalField(
                                controller:
                                _chestController,
                                label: 'Peito',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                              right: _DecimalField(
                                controller:
                                _waistController,
                                label: 'Cintura',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                            ),

                            const SizedBox(height: 14),

                            _MeasurementRow(
                              left: _DecimalField(
                                controller:
                                _abdomenController,
                                label: 'Abdômen',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                              right: _DecimalField(
                                controller:
                                _hipController,
                                label: 'Quadril',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                            ),

                            const SizedBox(height: 14),

                            _MeasurementRow(
                              left: _DecimalField(
                                controller:
                                _rightArmController,
                                label: 'Braço direito',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                              right: _DecimalField(
                                controller:
                                _leftArmController,
                                label: 'Braço esquerdo',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                            ),

                            const SizedBox(height: 14),

                            _MeasurementRow(
                              left: _DecimalField(
                                controller:
                                _rightThighController,
                                label: 'Coxa direita',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                              right: _DecimalField(
                                controller:
                                _leftThighController,
                                label: 'Coxa esquerda',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                            ),

                            const SizedBox(height: 14),

                            _MeasurementRow(
                              left: _DecimalField(
                                controller:
                                _rightCalfController,
                                label:
                                'Panturrilha direita',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                              right: _DecimalField(
                                controller:
                                _leftCalfController,
                                label:
                                'Panturrilha esquerda',
                                suffixText: 'cm',
                                enabled:
                                !viewModel.isSubmitting,
                              ),
                            ),
                          ],
                        ),
                      ),

                      const SizedBox(height: 20),

                      _FormSection(
                        title: 'Observações',
                        icon: Icons.notes_rounded,
                        child: TextFormField(
                          controller: _notesController,
                          enabled: !viewModel.isSubmitting,
                          minLines: 4,
                          maxLines: 6,
                          maxLength: 500,
                          textCapitalization:
                          TextCapitalization.sentences,
                          decoration: const InputDecoration(
                            hintText:
                            'Adicione observações sobre '
                                'a avaliação...',
                            border: OutlineInputBorder(),
                          ),
                        ),
                      ),

                      if (viewModel.errorMessage != null) ...[
                        const SizedBox(height: 20),
                        _ErrorMessage(
                          message:
                          viewModel.errorMessage!,
                        ),
                      ],

                      const SizedBox(height: 24),

                      SizedBox(
                        height: 52,
                        child: FilledButton.icon(
                          onPressed: viewModel.isSubmitting
                              ? null
                              : _submit,
                          icon: viewModel.isSubmitting
                              ? const SizedBox(
                            width: 20,
                            height: 20,
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
                                : 'Salvar avaliação',
                          ),
                        ),
                      ),

                      const SizedBox(height: 10),

                      TextButton(
                        onPressed: viewModel.isSubmitting
                            ? null
                            : () =>
                            Navigator.of(context).pop(),
                        child: const Text('Cancelar'),
                      ),
                    ],
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

class _InformationCard extends StatelessWidget {
  const _InformationCard();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.primary.withAlpha(16),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.info_outline_rounded,
            color: colorScheme.primary,
          ),
          const SizedBox(width: 12),
          const Expanded(
            child: Text(
              'Registre os dados obtidos na avaliação física. '
                  'Peso e altura são obrigatórios; as demais medidas '
                  'podem ser preenchidas conforme a avaliação realizada.',
            ),
          ),
        ],
      ),
    );
  }
}

class _FormSection extends StatelessWidget {
  const _FormSection({
    required this.title,
    required this.icon,
    required this.child,
  });

  final String title;
  final IconData icon;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(
                icon,
                color: colorScheme.primary,
              ),
              const SizedBox(width: 10),
              Text(
                title,
                style: theme.textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
          const SizedBox(height: 18),
          child,
        ],
      ),
    );
  }
}

class _AssessmentDateField extends StatelessWidget {
  const _AssessmentDateField({
    required this.date,
    required this.enabled,
    required this.onTap,
  });

  final DateTime date;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: enabled ? onTap : null,
      borderRadius: BorderRadius.circular(12),
      child: InputDecorator(
        decoration: const InputDecoration(
          labelText: 'Data da avaliação *',
          prefixIcon:
          Icon(Icons.calendar_month_outlined),
          border: OutlineInputBorder(),
        ),
        child: Text(
          _formatDate(date),
          style: const TextStyle(
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

class _MeasurementRow extends StatelessWidget {
  const _MeasurementRow({
    required this.left,
    required this.right,
  });

  final Widget left;
  final Widget right;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth < 300) {
          return Column(
            children: [
              left,
              const SizedBox(height: 14),
              right,
            ],
          );
        }

        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: left),
            const SizedBox(width: 12),
            Expanded(child: right),
          ],
        );
      },
    );
  }
}

class _DecimalField extends StatelessWidget {
  const _DecimalField({
    required this.controller,
    required this.label,
    required this.enabled,
    this.suffixText,
    this.maxValue,
    this.requiredField = false,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final String? suffixText;
  final double? maxValue;
  final bool requiredField;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      enabled: enabled,
      keyboardType: const TextInputType.numberWithOptions(
        decimal: true,
      ),
      inputFormatters: [
        FilteringTextInputFormatter.allow(
          RegExp(r'[0-9,.]'),
        ),
      ],
      decoration: InputDecoration(
        labelText: requiredField
            ? '$label *'
            : label,
        suffixText: suffixText,
        border: const OutlineInputBorder(),
      ),
      validator: (value) {
        final normalized =
            value?.trim().replaceAll(',', '.') ?? '';

        if (normalized.isEmpty) {
          if (requiredField) {
            return 'Campo obrigatório';
          }

          return null;
        }

        final parsed = double.tryParse(normalized);

        if (parsed == null || parsed <= 0) {
          return 'Valor inválido';
        }
        if (maxValue != null && parsed > maxValue!) {
          return 'Máximo ${maxValue!.toStringAsFixed(0)}';
        }

        return null;
      },
    );
  }
}

class _ErrorMessage extends StatelessWidget {
  const _ErrorMessage({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.error_outline_rounded,
            color: colorScheme.onErrorContainer,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                color: colorScheme.onErrorContainer,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

String _formatDate(DateTime date) {
  final day = date.day.toString().padLeft(2, '0');
  final month = date.month.toString().padLeft(2, '0');

  return '$day/$month/${date.year}';
}
