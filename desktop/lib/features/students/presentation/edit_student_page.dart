import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/presentation/edit_student_view_model.dart';

class EditStudentPage extends StatelessWidget {
  const EditStudentPage({required this.studentId, super.key});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => EditStudentViewModel(
        StudentsService(context.read<ApiClient>()),
        studentId,
      )..load(),
      child: _EditStudentView(studentId: studentId),
    );
  }
}

class _EditStudentView extends StatefulWidget {
  const _EditStudentView({required this.studentId});

  final String studentId;

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

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<EditStudentViewModel>();
    final primaryColor = context
        .watch<BrandingController>()
        .branding
        .primaryColor;

    if (viewModel.isLoading && viewModel.student == null) {
      return const Center(child: CircularProgressIndicator());
    }

    if (viewModel.errorMessage != null && viewModel.student == null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(viewModel.errorMessage!),
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: viewModel.load,
              child: const Text('Tentar novamente'),
            ),
          ],
        ),
      );
    }

    final student = viewModel.student;

    if (student == null) {
      return const SizedBox.shrink();
    }

    if (!_initialized) {
      _initialized = true;
      _nameController.text = student.name;
      _emailController.text = student.email;
      _phoneController.text = student.phone ?? '';
      _birthDate = student.birthDate;

      if (_birthDate != null) {
        _birthDateController.text = _formatDate(_birthDate!);
      }
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1050),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                IconButton(
                  tooltip: 'Voltar',
                  onPressed: () => context.go('/students/${widget.studentId}'),
                  icon: const Icon(Icons.arrow_back_rounded),
                ),
                const SizedBox(width: 8),
                const Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Editar aluno',
                      style: TextStyle(
                        color: Color(0xFF171A2C),
                        fontSize: 28,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(height: 7),
                    Text(
                      'Atualize os dados cadastrais do aluno.',
                      style: TextStyle(color: Color(0xFF74798D), fontSize: 14),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 26),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(26),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: const Color(0xFFE5E7EF)),
              ),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Container(
                          width: 38,
                          height: 38,
                          decoration: BoxDecoration(
                            color: primaryColor.withValues(alpha: 0.10),
                            borderRadius: BorderRadius.circular(10),
                          ),
                          child: Icon(
                            Icons.person_outline_rounded,
                            color: primaryColor,
                          ),
                        ),
                        const SizedBox(width: 12),
                        const Text(
                          'Dados do aluno',
                          style: TextStyle(
                            color: Color(0xFF24273A),
                            fontSize: 16,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 24),
                    Row(
                      children: [
                        Expanded(
                          child: _Field(
                            label: 'Nome completo *',
                            controller: _nameController,
                            icon: Icons.person_outline_rounded,
                            validator: (value) {
                              if (value == null || value.trim().isEmpty) {
                                return 'Informe o nome.';
                              }
                              return null;
                            },
                          ),
                        ),
                        const SizedBox(width: 20),
                        Expanded(
                          child: _Field(
                            label: 'E-mail *',
                            controller: _emailController,
                            icon: Icons.mail_outline_rounded,
                            validator: (value) {
                              if (value == null || value.trim().isEmpty) {
                                return 'Informe o e-mail.';
                              }

                              if (!value.contains('@')) {
                                return 'Informe um e-mail válido.';
                              }

                              return null;
                            },
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 20),
                    Row(
                      children: [
                        Expanded(
                          child: _Field(
                            label: 'Telefone',
                            controller: _phoneController,
                            icon: Icons.phone_outlined,
                          ),
                        ),
                        const SizedBox(width: 20),
                        Expanded(
                          child: _Field(
                            label: 'Data de nascimento',
                            controller: _birthDateController,
                            icon: Icons.calendar_month_outlined,
                            readOnly: true,
                            onTap: _selectBirthDate,
                          ),
                        ),
                      ],
                    ),
                    if (viewModel.errorMessage != null) ...[
                      const SizedBox(height: 20),
                      Text(
                        viewModel.errorMessage!,
                        style: const TextStyle(
                          color: Color(0xFFB54752),
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                    const SizedBox(height: 28),
                    const Divider(color: Color(0xFFE8EAF1)),
                    const SizedBox(height: 20),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        OutlinedButton(
                          onPressed: viewModel.isSaving
                              ? null
                              : () =>
                                    context.go('/students/${widget.studentId}'),
                          child: const Text('Cancelar'),
                        ),
                        const SizedBox(width: 12),
                        FilledButton.icon(
                          style: FilledButton.styleFrom(
                            backgroundColor: primaryColor,
                            padding: const EdgeInsets.symmetric(
                              horizontal: 22,
                              vertical: 16,
                            ),
                          ),
                          onPressed: viewModel.isSaving ? null : _save,
                          icon: viewModel.isSaving
                              ? const SizedBox(
                                  width: 16,
                                  height: 16,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : const Icon(Icons.save_outlined),
                          label: Text(
                            viewModel.isSaving
                                ? 'Salvando...'
                                : 'Salvar alterações',
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _selectBirthDate() async {
    final selected = await showDatePicker(
      context: context,
      initialDate: _birthDate ?? DateTime(2000),
      firstDate: DateTime(1900),
      lastDate: DateTime.now(),
    );

    if (selected == null) {
      return;
    }

    setState(() {
      _birthDate = selected;
      _birthDateController.text = _formatDate(selected);
    });
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final viewModel = context.read<EditStudentViewModel>();

    final success = await viewModel.save(
      name: _nameController.text.trim(),
      email: _emailController.text.trim(),
      phone: _phoneController.text.trim().isEmpty
          ? null
          : _phoneController.text.trim(),
      birthDate: _birthDate,
    );

    if (!mounted || !success) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Aluno atualizado com sucesso.')),
    );

    context.go('/students/${widget.studentId}');
  }

  static String _formatDate(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }
}

class _Field extends StatelessWidget {
  const _Field({
    required this.label,
    required this.controller,
    required this.icon,
    this.validator,
    this.readOnly = false,
    this.onTap,
  });

  final String label;
  final TextEditingController controller;
  final IconData icon;
  final String? Function(String?)? validator;
  final bool readOnly;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            color: Color(0xFF303348),
            fontSize: 12,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 8),
        TextFormField(
          controller: controller,
          validator: validator,
          readOnly: readOnly,
          onTap: onTap,
          decoration: InputDecoration(
            prefixIcon: Icon(icon),
            filled: true,
            fillColor: const Color(0xFFF7F8FB),
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Color(0xFFE3E5ED)),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Color(0xFFE3E5ED)),
            ),
          ),
        ),
      ],
    );
  }
}
