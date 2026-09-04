import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:gymflow/features/auth/presentation/create_professor_view_model.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:provider/provider.dart';

class CreateProfessorPage extends StatelessWidget {
  const CreateProfessorPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => CreateProfessorViewModel(
        AuthService(
          context.read<ApiClient>(),
        ),
      ),
      child: const _CreateProfessorView(),
    );
  }
}

class _CreateProfessorView extends StatefulWidget {
  const _CreateProfessorView();

  @override
  State<_CreateProfessorView> createState() =>
      _CreateProfessorViewState();
}

class _CreateProfessorViewState
    extends State<_CreateProfessorView> {
  final _formKey = GlobalKey<FormState>();

  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();

  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();

    super.dispose();
  }

  Future<void> _submit() async {
    FocusManager.instance.primaryFocus?.unfocus();

    final isValid =
        _formKey.currentState?.validate() ?? false;

    if (!isValid) {
      return;
    }

    final viewModel =
    context.read<CreateProfessorViewModel>();

    final created = await viewModel.createProfessor(
      name: _nameController.text,
      email: _emailController.text,
      password: _passwordController.text,
      confirmPassword: _confirmPasswordController.text,
    );

    if (!mounted || !created) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'Professor cadastrado com sucesso.',
        ),
      ),
    );

    Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final viewModel =
    context.watch<CreateProfessorViewModel>();

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
                  crossAxisAlignment:
                  CrossAxisAlignment.stretch,
                  children: [
                    const ProfessorAdminPageHeader(),

                    const SizedBox(height: 24),

                    Text(
                      'Cadastrar professor',
                      style:
                      theme.textTheme.headlineLarge
                          ?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 8),

                    Text(
                      'Crie o acesso de um professor para esta academia.',
                      style:
                      theme.textTheme.bodyLarge
                          ?.copyWith(
                        color:
                        colorScheme
                            .onSurfaceVariant,
                        height: 1.45,
                      ),
                    ),

                    const SizedBox(height: 28),

                    Container(
                      padding: const EdgeInsets.all(20),
                      decoration: BoxDecoration(
                        color: colorScheme.surface,
                        borderRadius:
                        BorderRadius.circular(18),
                        border: Border.all(
                          color: colorScheme
                              .outlineVariant
                              .withAlpha(120),
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment:
                        CrossAxisAlignment.stretch,
                        children: [
                          Row(
                            children: [
                              Container(
                                width: 44,
                                height: 44,
                                decoration: BoxDecoration(
                                  shape: BoxShape.circle,
                                  color: colorScheme
                                      .primary
                                      .withAlpha(18),
                                ),
                                child: Icon(
                                  Icons
                                      .badge_outlined,
                                  color:
                                  colorScheme
                                      .primary,
                                ),
                              ),
                              const SizedBox(width: 12),
                              Text(
                                'Dados do professor',
                                style: theme
                                    .textTheme
                                    .titleLarge
                                    ?.copyWith(
                                  fontWeight:
                                  FontWeight.w800,
                                ),
                              ),
                            ],
                          ),

                          const SizedBox(height: 26),

                          const _FieldLabel(
                            text: 'Nome completo',
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller:
                            _nameController,
                            textCapitalization:
                            TextCapitalization
                                .words,
                            textInputAction:
                            TextInputAction.next,
                            onChanged: (_) =>
                                viewModel.clearError(),
                            validator: (value) {
                              final name =
                                  value?.trim() ?? '';

                              if (name.isEmpty) {
                                return 'Informe o nome completo.';
                              }

                              if (name.length < 2) {
                                return 'Informe um nome válido.';
                              }

                              return null;
                            },
                            decoration:
                            _inputDecoration(
                              context,
                              hintText:
                              'Digite o nome do professor',
                              prefixIcon: Icons
                                  .person_outline_rounded,
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'E-mail',
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller:
                            _emailController,
                            keyboardType:
                            TextInputType
                                .emailAddress,
                            textInputAction:
                            TextInputAction.next,
                            autocorrect: false,
                            enableSuggestions: false,
                            onChanged: (_) =>
                                viewModel.clearError(),
                            validator: (value) {
                              final email =
                                  value?.trim() ?? '';

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
                            decoration:
                            _inputDecoration(
                              context,
                              hintText:
                              'Digite o e-mail do professor',
                              prefixIcon:
                              Icons.mail_outline_rounded,
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'Senha',
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller:
                            _passwordController,
                            obscureText:
                            _obscurePassword,
                            textInputAction:
                            TextInputAction.next,
                            autocorrect: false,
                            enableSuggestions: false,
                            onChanged: (_) =>
                                viewModel.clearError(),
                            validator: (value) {
                              if (value == null ||
                                  value.isEmpty) {
                                return 'Informe uma senha.';
                              }

                              if (value.length < 8) {
                                return 'A senha deve ter pelo menos 8 caracteres.';
                              }

                              return null;
                            },
                            decoration:
                            _inputDecoration(
                              context,
                              hintText:
                              'Digite uma senha',
                              prefixIcon:
                              Icons.lock_outline_rounded,
                              suffixIcon: IconButton(
                                tooltip:
                                _obscurePassword
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
                                      ? Icons
                                      .visibility_outlined
                                      : Icons
                                      .visibility_off_outlined,
                                ),
                              ),
                            ),
                          ),

                          const SizedBox(height: 20),

                          const _FieldLabel(
                            text: 'Confirmar senha',
                          ),

                          const SizedBox(height: 8),

                          TextFormField(
                            controller:
                            _confirmPasswordController,
                            obscureText:
                            _obscureConfirmPassword,
                            textInputAction:
                            TextInputAction.done,
                            autocorrect: false,
                            enableSuggestions: false,
                            onChanged: (_) =>
                                viewModel.clearError(),
                            validator: (value) {
                              if (value == null ||
                                  value.isEmpty) {
                                return 'Confirme a senha.';
                              }

                              if (value !=
                                  _passwordController.text) {
                                return 'As senhas não coincidem.';
                              }

                              return null;
                            },
                            decoration:
                            _inputDecoration(
                              context,
                              hintText:
                              'Confirme a senha',
                              prefixIcon:
                              Icons.lock_outline_rounded,
                              suffixIcon: IconButton(
                                tooltip:
                                _obscureConfirmPassword
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
                                      ? Icons
                                      .visibility_outlined
                                      : Icons
                                      .visibility_off_outlined,
                                ),
                              ),
                            ),
                          ),

                          const SizedBox(height: 24),

                          Container(
                            padding:
                            const EdgeInsets.all(16),
                            decoration: BoxDecoration(
                              color: colorScheme.primary
                                  .withAlpha(18),
                              borderRadius:
                              BorderRadius.circular(14),
                            ),
                            child: Row(
                              crossAxisAlignment:
                              CrossAxisAlignment.start,
                              children: [
                                Icon(
                                  Icons
                                      .info_outline_rounded,
                                  color:
                                  colorScheme.primary,
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Text(
                                    'O professor será vinculado automaticamente a esta academia.',
                                    style: theme
                                        .textTheme
                                        .bodyMedium
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

                    if (viewModel.errorMessage !=
                        null) ...[
                      const SizedBox(height: 18),
                      Container(
                        padding:
                        const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: colorScheme.error
                              .withAlpha(18),
                          borderRadius:
                          BorderRadius.circular(12),
                        ),
                        child: Text(
                          viewModel.errorMessage!,
                          style: TextStyle(
                            color: colorScheme.error,
                          ),
                        ),
                      ),
                    ],

                    const SizedBox(height: 24),

                    SizedBox(
                      height: 54,
                      child: FilledButton.icon(
                        onPressed:
                        viewModel.isLoading
                            ? null
                            : _submit,
                        icon: viewModel.isLoading
                            ? const SizedBox.shrink()
                            : const Icon(
                          Icons
                              .person_add_alt_1_rounded,
                        ),
                        label: viewModel.isLoading
                            ? const SizedBox(
                          width: 22,
                          height: 22,
                          child:
                          CircularProgressIndicator(
                            strokeWidth: 2.5,
                          ),
                        )
                            : const Text(
                          'Cadastrar professor',
                          style: TextStyle(
                            fontWeight:
                            FontWeight.w700,
                          ),
                        ),
                      ),
                    ),

                    const SizedBox(height: 12),

                    SizedBox(
                      height: 52,
                      child: OutlinedButton(
                        onPressed:
                        viewModel.isLoading
                            ? null
                            : () =>
                            Navigator.of(context)
                                .pop(),
                        child:
                        const Text('Cancelar'),
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
        currentItem: ProfessorAdminNavItem.more,
      ),
    );
  }

  InputDecoration _inputDecoration(
      BuildContext context, {
        required String hintText,
        required IconData prefixIcon,
        Widget? suffixIcon,
      }) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return InputDecoration(
      hintText: hintText,
      prefixIcon: Icon(
        prefixIcon,
        color: colorScheme.primary,
      ),
      suffixIcon: suffixIcon,
      filled: true,
      fillColor: colorScheme.surface,
      contentPadding:
      const EdgeInsets.symmetric(
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

class _FieldLabel extends StatelessWidget {
  const _FieldLabel({
    required this.text,
  });

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text.rich(
      TextSpan(
        text: text,
        children: const [
          TextSpan(
            text: ' *',
            style: TextStyle(
              color: Colors.red,
            ),
          ),
        ],
      ),
      style: Theme.of(context)
          .textTheme
          .labelLarge
          ?.copyWith(
        fontWeight: FontWeight.w700,
      ),
    );
  }
}
