import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/presentation/edit_student_view_model.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class EditStudentPage extends StatelessWidget {
  const EditStudentPage({
    required this.studentId,
    super.key,
  });

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => EditStudentViewModel(
        StudentsService(
          context.read<ApiClient>(),
        ),
        studentId,
      )..load(),
      child: const _EditStudentView(),
    );
  }
}

class _EditStudentView extends StatefulWidget {
  const _EditStudentView();

  @override
  State<_EditStudentView> createState() => _EditStudentViewState();
}

class _EditStudentViewState extends State<_EditStudentView> {
  final _formKey = GlobalKey<FormState>();

  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _birthDateController = TextEditingController();

  DateTime? _birthDate;
  bool _initialized = false;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _birthDateController.dispose();

    super.dispose();
  }

  void _initializeFields(EditStudentViewModel viewModel) {
    if (_initialized || viewModel.student == null) {
      return;
    }

    final student = viewModel.student!;

    _nameController.text = student.name;
    _emailController.text = student.email;
    _phoneController.text = student.phone ?? '';

    _birthDate = student.birthDate;

    if (_birthDate != null) {
      _birthDateController.text = _formatDate(_birthDate!);
    }

    _initialized = true;
  }

  Future<void> _selectBirthDate() async {
    FocusManager.instance.primaryFocus?.unfocus();

    final now = DateTime.now();

    final selected = await showDatePicker(
      context: context,
      initialDate: _birthDate ??
          DateTime(
            now.year - 18,
            now.month,
            now.day,
          ),
      firstDate: DateTime(1900),
      lastDate: now,
      helpText: 'Selecione a data de nascimento',
      cancelText: 'Cancelar',
      confirmText: 'Confirmar',
    );

    if (selected == null || !mounted) {
      return;
    }

    setState(() {
      _birthDate = selected;
      _birthDateController.text = _formatDate(selected);
    });

    context.read<EditStudentViewModel>().clearError();
  }

  Future<void> _submit() async {
    FocusManager.instance.primaryFocus?.unfocus();

    final isValid = _formKey.currentState?.validate() ?? false;

    if (!isValid) {
      return;
    }

    final saved = await context.read<EditStudentViewModel>().save(
      name: _nameController.text,
      email: _emailController.text,
      phone: _phoneController.text,
      birthDate: _birthDate,
    );

    if (!mounted || !saved) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Dados do aluno atualizados com sucesso.'),
      ),
    );

    Navigator.of(context).pop(true);
  }

  String _formatDate(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<EditStudentViewModel>();

    _initializeFields(viewModel);

    if (viewModel.isLoading && viewModel.student == null) {
      return const Scaffold(
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

    if (viewModel.student == null) {
      return Scaffold(
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  viewModel.errorMessage ??
                      'Não foi possível carregar o aluno.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: viewModel.load,
                  child: const Text('Tentar novamente'),
                ),
              ],
            ),
          ),
        ),
      );
    }

    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final student = viewModel.student!;

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: SingleChildScrollView(
          keyboardDismissBehavior:
          ScrollViewKeyboardDismissBehavior.onDrag,
          padding: const EdgeInsets.fromLTRB(
            24,
            18,
            24,
            32,
          ),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(
                maxWidth: 680,
              ),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const ProfessorAdminPageHeader(),

                    const SizedBox(height: 24),

                    Text(
                      'Editar aluno',
                      style: theme.textTheme.headlineLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 8),

                    Text(
                      'Atualize os dados do aluno abaixo.',
                      style: theme.textTheme.bodyLarge?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                      ),
                    ),

                    const SizedBox(height: 28),

                    _FormSection(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Row(
                            children: [
                              _SectionIcon(
                                icon: Icons.person_outline_rounded,
                              ),
                              const SizedBox(width: 12),
                              Text(
                                'Dados do aluno',
                                style: theme.textTheme.titleLarge?.copyWith(
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            ],
                          ),

                          const SizedBox(height: 22),

                          Row(
                            children: [
                              Text(
                                'Status',
                                style: theme.textTheme.titleSmall?.copyWith(
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              const SizedBox(width: 12),
                              _StatusBadge(
                                isActive: student.isActive,
                              ),
                            ],
                          ),

                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 20),
                            child: Divider(),
                          ),

                          const _FieldLabel(
                            text: 'Nome completo',
                            requiredField: true,
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller: _nameController,
                            textCapitalization: TextCapitalization.words,
                            textInputAction: TextInputAction.next,
                            onChanged: (_) => viewModel.clearError(),
                            validator: (value) {
                              if (value == null ||
                                  value.trim().isEmpty) {
                                return 'Informe o nome completo.';
                              }

                              return null;
                            },
                            decoration: _inputDecoration(
                              context,
                              hintText: 'Nome completo',
                              prefixIcon: Icons.person_outline_rounded,
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'E-mail',
                            requiredField: true,
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller: _emailController,
                            keyboardType: TextInputType.emailAddress,
                            textInputAction: TextInputAction.next,
                            autocorrect: false,
                            enableSuggestions: false,
                            onChanged: (_) => viewModel.clearError(),
                            validator: (value) {
                              final email = value?.trim() ?? '';

                              if (email.isEmpty) {
                                return 'Informe o e-mail.';
                              }

                              final valid = RegExp(
                                r'^[^@\s]+@[^@\s]+\.[^@\s]+$',
                              ).hasMatch(email);

                              if (!valid) {
                                return 'Informe um e-mail válido.';
                              }

                              return null;
                            },
                            decoration: _inputDecoration(
                              context,
                              hintText: 'E-mail',
                              prefixIcon: Icons.mail_outline_rounded,
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'Telefone',
                            suffix: ' (opcional)',
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller: _phoneController,
                            keyboardType: TextInputType.phone,
                            textInputAction: TextInputAction.next,
                            onChanged: (_) => viewModel.clearError(),
                            decoration: _inputDecoration(
                              context,
                              hintText: '(21) 99999-9999',
                              prefixIcon: Icons.phone_outlined,
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'Data de nascimento',
                            requiredField: true,
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller: _birthDateController,
                            readOnly: true,
                            onTap: _selectBirthDate,
                            validator: (_) {
                              if (_birthDate == null) {
                                return 'Informe a data de nascimento.';
                              }

                              return null;
                            },
                            decoration: _inputDecoration(
                              context,
                              hintText: 'dd/mm/aaaa',
                              prefixIcon:
                              Icons.calendar_month_outlined,
                              suffixIcon: IconButton(
                                tooltip:
                                'Selecionar data de nascimento',
                                onPressed: _selectBirthDate,
                                icon: const Icon(
                                  Icons.calendar_today_outlined,
                                ),
                              ),
                            ),
                          ),

                          const SizedBox(height: 24),

                          Container(
                            padding: const EdgeInsets.all(16),
                            decoration: BoxDecoration(
                              color:
                              colorScheme.primary.withAlpha(18),
                              borderRadius: BorderRadius.circular(14),
                            ),
                            child: Row(
                              crossAxisAlignment:
                              CrossAxisAlignment.start,
                              children: [
                                Icon(
                                  Icons.info_outline_rounded,
                                  color: colorScheme.primary,
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Text(
                                    'As alterações serão aplicadas ao '
                                        'cadastro do aluno no Avelri.',
                                    style: theme.textTheme.bodyMedium
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

                    if (viewModel.errorMessage != null) ...[
                      const SizedBox(height: 18),
                      _ErrorMessage(
                        message: viewModel.errorMessage!,
                      ),
                    ],

                    const SizedBox(height: 24),

                    SizedBox(
                      height: 54,
                      child: FilledButton.icon(
                        onPressed:
                        viewModel.isSaving ? null : _submit,
                        icon: viewModel.isSaving
                            ? const SizedBox.shrink()
                            : const Icon(
                          Icons.save_outlined,
                        ),
                        label: viewModel.isSaving
                            ? const SizedBox(
                          width: 22,
                          height: 22,
                          child: CircularProgressIndicator(
                            strokeWidth: 2.5,
                          ),
                        )
                            : const Text(
                          'Salvar alterações',
                          style: TextStyle(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ),

                    const SizedBox(height: 12),

                    SizedBox(
                      height: 52,
                      child: OutlinedButton(
                        onPressed: viewModel.isSaving
                            ? null
                            : () => Navigator.of(context).pop(),
                        child: const Text('Cancelar'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
    );
  }

  InputDecoration _inputDecoration(
      BuildContext context, {
        required String hintText,
        required IconData prefixIcon,
        Widget? suffixIcon,
      }) {
    final colorScheme = Theme.of(context).colorScheme;

    return InputDecoration(
      hintText: hintText,
      prefixIcon: Icon(
        prefixIcon,
        color: colorScheme.primary,
      ),
      suffixIcon: suffixIcon,
      filled: true,
      fillColor: colorScheme.surface,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: 16,
        vertical: 17,
      ),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: BorderSide(
          color: colorScheme.outlineVariant,
        ),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: BorderSide(
          color: colorScheme.primary,
          width: 1.5,
        ),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: BorderSide(
          color: colorScheme.error,
        ),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: BorderSide(
          color: colorScheme.error,
          width: 1.5,
        ),
      ),
    );
  }
}

class _FormSection extends StatelessWidget {
  const _FormSection({
    required this.child,
  });

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: child,
    );
  }
}

class _SectionIcon extends StatelessWidget {
  const _SectionIcon({
    required this.icon,
  });

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      width: 46,
      height: 46,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: colorScheme.primary.withAlpha(22),
      ),
      child: Icon(
        icon,
        color: colorScheme.primary,
      ),
    );
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel({
    required this.text,
    this.requiredField = false,
    this.suffix = '',
  });

  final String text;
  final bool requiredField;
  final String suffix;

  @override
  Widget build(BuildContext context) {
    return Text.rich(
      TextSpan(
        children: [
          TextSpan(text: text),
          TextSpan(
            text: suffix,
            style: TextStyle(
              color: Theme.of(context)
                  .colorScheme
                  .onSurfaceVariant,
              fontWeight: FontWeight.w400,
            ),
          ),
          if (requiredField)
            TextSpan(
              text: ' *',
              style: TextStyle(
                color: Theme.of(context).colorScheme.error,
              ),
            ),
        ],
      ),
      style: Theme.of(context).textTheme.labelLarge?.copyWith(
        fontWeight: FontWeight.w700,
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({
    required this.isActive,
  });

  final bool isActive;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    final color = isActive
        ? const Color(0xFF22C55E)
        : colorScheme.error;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 6,
      ),
      decoration: BoxDecoration(
        color: color.withAlpha(24),
        borderRadius: BorderRadius.circular(9),
      ),
      child: Text(
        isActive ? 'Ativo' : 'Inativo',
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.w800,
        ),
      ),
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
