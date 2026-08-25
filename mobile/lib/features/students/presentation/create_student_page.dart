import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/presentation/create_student_view_model.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class CreateStudentPage extends StatelessWidget {
  const CreateStudentPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => CreateStudentViewModel(
        StudentsService(
          context.read<ApiClient>(),
        ),
      ),
      child: const _CreateStudentView(),
    );
  }
}

class _CreateStudentView extends StatefulWidget {
  const _CreateStudentView();

  @override
  State<_CreateStudentView> createState() => _CreateStudentViewState();
}

class _CreateStudentViewState extends State<_CreateStudentView> {
  final _formKey = GlobalKey<FormState>();

  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _phoneController = TextEditingController();
  final _birthDateController = TextEditingController();

  DateTime? _birthDate;

  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    _phoneController.dispose();
    _birthDateController.dispose();

    super.dispose();
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

    context.read<CreateStudentViewModel>().clearError();
  }

  Future<void> _submit() async {
    FocusManager.instance.primaryFocus?.unfocus();

    final isValid = _formKey.currentState?.validate() ?? false;

    if (!isValid) {
      return;
    }

    final viewModel = context.read<CreateStudentViewModel>();

    final created = await viewModel.createStudent(
      name: _nameController.text,
      email: _emailController.text,
      password: _passwordController.text,
      confirmPassword: _confirmPasswordController.text,
      phone: _phoneController.text,
      birthDate: _birthDate,
    );

    if (!mounted || !created) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Aluno cadastrado com sucesso.'),
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
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final viewModel = context.watch<CreateStudentViewModel>();

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
                      'Cadastrar aluno',
                      style: theme.textTheme.headlineLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 8),

                    Text(
                      'Preencha os dados do aluno para criar o acesso '
                          'e o cadastro no app.',
                      style: theme.textTheme.bodyLarge?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                        height: 1.45,
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

                          const SizedBox(height: 26),

                          const _FieldLabel(
                            text: 'Nome completo',
                            requiredField: true,
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller: _nameController,
                            textCapitalization: TextCapitalization.words,
                            textInputAction: TextInputAction.next,
                            autofillHints: const [
                              AutofillHints.name,
                            ],
                            onChanged: (_) {
                              viewModel.clearError();
                            },
                            validator: (value) {
                              if (value == null ||
                                  value.trim().isEmpty) {
                                return 'Informe o nome completo.';
                              }

                              if (value.trim().length < 2) {
                                return 'Informe um nome válido.';
                              }

                              return null;
                            },
                            decoration: _inputDecoration(
                              context,
                              hintText:
                              'Digite o nome completo do aluno',
                              prefixIcon:
                              Icons.person_outline_rounded,
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
                            keyboardType:
                            TextInputType.emailAddress,
                            textInputAction: TextInputAction.next,
                            autofillHints: const [
                              AutofillHints.email,
                            ],
                            autocorrect: false,
                            enableSuggestions: false,
                            onChanged: (_) {
                              viewModel.clearError();
                            },
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
                              hintText: 'Digite o e-mail do aluno',
                              prefixIcon: Icons.mail_outline_rounded,
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'Senha',
                            requiredField: true,
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller: _passwordController,
                            obscureText: _obscurePassword,
                            textInputAction: TextInputAction.next,
                            autocorrect: false,
                            enableSuggestions: false,
                            onChanged: (_) {
                              viewModel.clearError();
                            },
                            validator: (value) {
                              if (value == null || value.isEmpty) {
                                return 'Informe uma senha.';
                              }

                              return null;
                            },
                            decoration: _inputDecoration(
                              context,
                              hintText: 'Digite uma senha',
                              prefixIcon: Icons.lock_outline_rounded,
                              suffixIcon: IconButton(
                                tooltip: _obscurePassword
                                    ? 'Mostrar senha'
                                    : 'Ocultar senha',
                                onPressed: () {
                                  setState(() {
                                    _obscurePassword =
                                    !_obscurePassword;
                                  });
                                },
                                icon: Icon(
                                  _obscurePassword
                                      ? Icons.visibility_outlined
                                      : Icons.visibility_off_outlined,
                                ),
                              ),
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'Confirmar senha',
                            requiredField: true,
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller: _confirmPasswordController,
                            obscureText: _obscureConfirmPassword,
                            textInputAction: TextInputAction.next,
                            autocorrect: false,
                            enableSuggestions: false,
                            onChanged: (_) {
                              viewModel.clearError();
                            },
                            validator: (value) {
                              if (value == null || value.isEmpty) {
                                return 'Confirme a senha.';
                              }

                              if (value != _passwordController.text) {
                                return 'As senhas não coincidem.';
                              }

                              return null;
                            },
                            decoration: _inputDecoration(
                              context,
                              hintText: 'Confirme a senha',
                              prefixIcon: Icons.lock_outline_rounded,
                              suffixIcon: IconButton(
                                tooltip: _obscureConfirmPassword
                                    ? 'Mostrar senha'
                                    : 'Ocultar senha',
                                onPressed: () {
                                  setState(() {
                                    _obscureConfirmPassword =
                                    !_obscureConfirmPassword;
                                  });
                                },
                                icon: Icon(
                                  _obscureConfirmPassword
                                      ? Icons.visibility_outlined
                                      : Icons.visibility_off_outlined,
                                ),
                              ),
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
                            autofillHints: const [
                              AutofillHints.telephoneNumber,
                            ],
                            onChanged: (_) {
                              viewModel.clearError();
                            },
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
                                    'A senha cadastrada será utilizada '
                                        'pelo aluno para acessar o GymFlow.',
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
                        viewModel.isLoading ? null : _submit,
                        icon: viewModel.isLoading
                            ? const SizedBox.shrink()
                            : const Icon(
                          Icons.person_add_alt_1_rounded,
                        ),
                        label: viewModel.isLoading
                            ? const SizedBox(
                          width: 22,
                          height: 22,
                          child: CircularProgressIndicator(
                            strokeWidth: 2.5,
                          ),
                        )
                            : const Text(
                          'Cadastrar aluno',
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
                        onPressed: viewModel.isLoading
                            ? null
                            : () => Navigator.of(context).pop(),
                        child: const Text('Cancelar'),
                      ),
                    ),

                    const SizedBox(height: 12),
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
