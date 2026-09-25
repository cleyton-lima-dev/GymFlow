import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/presentation/create_student_view_model.dart';

class CreateStudentPage extends StatelessWidget {
  const CreateStudentPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          CreateStudentViewModel(StudentsService(context.read<ApiClient>())),
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
  final _phoneController = TextEditingController();
  final _birthDateController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();

  DateTime? _birthDate;

  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _birthDateController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();

    super.dispose();
  }

  Future<void> _selectBirthDate() async {
    final now = DateTime.now();

    final selected = await showDatePicker(
      context: context,
      initialDate: _birthDate ?? DateTime(now.year - 18),
      firstDate: DateTime(1900),
      lastDate: now,
      helpText: 'Data de nascimento',
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

    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final created = await context.read<CreateStudentViewModel>().createStudent(
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
      const SnackBar(content: Text('Aluno cadastrado com sucesso.')),
    );

    context.go('/students');
  }

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<CreateStudentViewModel>();
    final branding = context.watch<BrandingController>().branding;
    final primaryColor = branding.primaryColor;
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1050),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  IconButton(
                    tooltip: 'Voltar',
                    onPressed: viewModel.isLoading
                        ? null
                        : () => context.go('/students'),
                    icon: const Icon(Icons.arrow_back_rounded),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Cadastrar aluno',
                          style: TextStyle(
                            color: colorScheme.onSurface,
                            fontSize: 28,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.5,
                          ),
                        ),
                        SizedBox(height: 7),
                        Text(
                          'Crie o cadastro e o acesso do aluno ao aplicativo.',
                          style: TextStyle(
                            color: colorScheme.onSurfaceVariant,
                            fontSize: 14,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 26),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(26),
                decoration: BoxDecoration(
                  color: colorScheme.surface,
                  borderRadius: BorderRadius.circular(18),
                  border: Border.all(color: theme.dividerColor),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _SectionTitle(
                      icon: Icons.person_outline_rounded,
                      title: 'Dados do aluno',
                      color: primaryColor,
                    ),
                    const SizedBox(height: 24),
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: _Field(
                            label: 'Nome completo',
                            requiredField: true,
                            child: TextFormField(
                              controller: _nameController,
                              textCapitalization: TextCapitalization.words,
                              textInputAction: TextInputAction.next,
                              onChanged: (_) => viewModel.clearError(),
                              validator: (value) {
                                final name = value?.trim() ?? '';

                                if (name.isEmpty) {
                                  return 'Informe o nome.';
                                }

                                if (name.length < 2) {
                                  return 'Informe um nome válido.';
                                }

                                return null;
                              },
                              decoration: _decoration(
                                context,
                                hintText: 'Nome completo',
                                icon: Icons.person_outline_rounded,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 18),
                        Expanded(
                          child: _Field(
                            label: 'E-mail',
                            requiredField: true,
                            child: TextFormField(
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
                              decoration: _decoration(
                                context,
                                hintText: 'aluno@email.com',
                                icon: Icons.mail_outline_rounded,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 20),
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: _Field(
                            label: 'Telefone',
                            child: TextFormField(
                              controller: _phoneController,
                              keyboardType: TextInputType.phone,
                              textInputAction: TextInputAction.next,
                              onChanged: (_) => viewModel.clearError(),
                              decoration: _decoration(
                                context,
                                hintText: '(21) 99999-9999',
                                icon: Icons.phone_outlined,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 18),
                        Expanded(
                          child: _Field(
                            label: 'Data de nascimento',
                            requiredField: true,
                            child: TextFormField(
                              controller: _birthDateController,
                              readOnly: true,
                              onTap: _selectBirthDate,
                              validator: (_) {
                                if (_birthDate == null) {
                                  return 'Informe a data de nascimento.';
                                }

                                return null;
                              },
                              decoration: _decoration(
                                context,
                                hintText: 'dd/mm/aaaa',
                                icon: Icons.calendar_month_outlined,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 32),
                    Divider(color: theme.dividerColor),
                    const SizedBox(height: 24),
                    _SectionTitle(
                      icon: Icons.lock_outline_rounded,
                      title: 'Acesso ao aplicativo',
                      color: primaryColor,
                    ),
                    const SizedBox(height: 24),
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: _Field(
                            label: 'Senha',
                            requiredField: true,
                            child: TextFormField(
                              controller: _passwordController,
                              obscureText: _obscurePassword,
                              textInputAction: TextInputAction.next,
                              autocorrect: false,
                              enableSuggestions: false,
                              onChanged: (_) => viewModel.clearError(),
                              validator: (value) {
                                if (value == null || value.isEmpty) {
                                  return 'Informe uma senha.';
                                }

                                if (value.length < 8) {
                                  return 'Use pelo menos 8 caracteres.';
                                }

                                return null;
                              },
                              decoration: _decoration(
                                context,
                                hintText: 'Mínimo de 8 caracteres',
                                icon: Icons.lock_outline_rounded,
                                suffixIcon: IconButton(
                                  tooltip: _obscurePassword
                                      ? 'Mostrar senha'
                                      : 'Ocultar senha',
                                  onPressed: () {
                                    setState(() {
                                      _obscurePassword = !_obscurePassword;
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
                          ),
                        ),
                        const SizedBox(width: 18),
                        Expanded(
                          child: _Field(
                            label: 'Confirmar senha',
                            requiredField: true,
                            child: TextFormField(
                              controller: _confirmPasswordController,
                              obscureText: _obscureConfirmPassword,
                              textInputAction: TextInputAction.done,
                              autocorrect: false,
                              enableSuggestions: false,
                              onFieldSubmitted: (_) {
                                if (!viewModel.isLoading) {
                                  _submit();
                                }
                              },
                              onChanged: (_) => viewModel.clearError(),
                              validator: (value) {
                                if (value == null || value.isEmpty) {
                                  return 'Confirme a senha.';
                                }

                                if (value != _passwordController.text) {
                                  return 'As senhas não coincidem.';
                                }

                                return null;
                              },
                              decoration: _decoration(
                                context,
                                hintText: 'Repita a senha',
                                icon: Icons.lock_outline_rounded,
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
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 22),
                    Container(
                      padding: const EdgeInsets.all(15),
                      decoration: BoxDecoration(
                        color: primaryColor.withValues(alpha: 0.07),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                          color: primaryColor.withValues(alpha: 0.15),
                        ),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            Icons.info_outline_rounded,
                            color: primaryColor,
                            size: 20,
                          ),
                          const SizedBox(width: 11),
                          const Expanded(
                            child: Text(
                              'Essa senha será utilizada pelo aluno para acessar o aplicativo da academia.',
                              style: TextStyle(
                                color: Color(0xFF656A7E),
                                fontSize: 12,
                                height: 1.4,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    if (viewModel.errorMessage != null) ...[
                      const SizedBox(height: 20),
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: const Color(0xFFFCEEEF),
                          borderRadius: BorderRadius.circular(11),
                        ),
                        child: Row(
                          children: [
                            const Icon(
                              Icons.error_outline_rounded,
                              color: Color(0xFFB54752),
                              size: 20,
                            ),
                            const SizedBox(width: 10),
                            Expanded(
                              child: Text(
                                viewModel.errorMessage!,
                                style: const TextStyle(
                                  color: Color(0xFF9E3E48),
                                  fontSize: 12,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                    const SizedBox(height: 26),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        OutlinedButton(
                          onPressed: viewModel.isLoading
                              ? null
                              : () => context.go('/students'),
                          child: const Text('Cancelar'),
                        ),
                        const SizedBox(width: 12),
                        FilledButton.icon(
                          onPressed: viewModel.isLoading ? null : _submit,
                          style: FilledButton.styleFrom(
                            backgroundColor: primaryColor,
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(
                              horizontal: 20,
                              vertical: 16,
                            ),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(11),
                            ),
                          ),
                          icon: viewModel.isLoading
                              ? const SizedBox(
                                  width: 17,
                                  height: 17,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : const Icon(Icons.person_add_alt_1_rounded),
                          label: Text(
                            viewModel.isLoading
                                ? 'Cadastrando...'
                                : 'Cadastrar aluno',
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _formatDate(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }

  InputDecoration _decoration(
      BuildContext context, {
        required String hintText,
        required IconData icon,
        Widget? suffixIcon,
      }) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return InputDecoration(
      hintText: hintText,
      prefixIcon: Icon(icon),
      suffixIcon: suffixIcon,
      filled: true,
      fillColor: colorScheme.surfaceContainerHighest,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: 15,
        vertical: 16,
      ),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(11),
        borderSide: BorderSide(
          color: theme.dividerColor,
        ),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(11),
        borderSide: BorderSide(
          color: theme.dividerColor,
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({
    required this.icon,
    required this.title,
    required this.color,
  });

  final IconData icon;
  final String title;
  final Color color;


  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Row(
      children: [
        Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.10),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Icon(icon, color: color, size: 20),
        ),
        const SizedBox(width: 12),
        Text(
          title,
          style: TextStyle(
            color: colorScheme.onSurface,
            fontSize: 16,
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
  }
}

class _Field extends StatelessWidget {
  const _Field({
    required this.label,
    required this.child,
    this.requiredField = false,
  });

  final String label;
  final Widget child;
  final bool requiredField;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        RichText(
          text: TextSpan(
            style: TextStyle(
              color: colorScheme.onSurfaceVariant,
              fontSize: 12,
              fontWeight: FontWeight.w700,
            ),
            children: [
              TextSpan(text: label),
              if (requiredField)
                const TextSpan(
                  text: ' *',
                  style: TextStyle(color: Color(0xFFB54752)),
                ),
            ],
          ),
        ),
        const SizedBox(height: 8),
        child,
      ],
    );
  }
}
