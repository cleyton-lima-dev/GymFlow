import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/enrollments/data/enrollments_service.dart';
import 'package:avelri_gestao/features/enrollments/presentation/create_enrollment_view_model.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';


class CreateEnrollmentPage extends StatelessWidget {
  const CreateEnrollmentPage({super.key});

  @override
  Widget build(BuildContext context) {
    final apiClient = context.read<ApiClient>();

    return ChangeNotifierProvider(
      create: (_) => CreateEnrollmentViewModel(
        enrollmentsService: EnrollmentsService(apiClient),
        studentsService: StudentsService(apiClient),
        plansService: PlansService(apiClient),
      )..loadInitial(),
      child: const _CreateEnrollmentView(),
    );
  }
}

class _CreateEnrollmentView extends StatefulWidget {
  const _CreateEnrollmentView();

  @override
  State<_CreateEnrollmentView> createState() =>
      _CreateEnrollmentViewState();
}

class _CreateEnrollmentViewState
    extends State<_CreateEnrollmentView> {
  final _formKey = GlobalKey<FormState>();

  String? _studentId;
  String? _planId;

  DateTime _startDate =
  DateUtils.dateOnly(DateTime.now());

  Future<void> _selectStartDate() async {
    final selectedDate = await showDatePicker(
      context: context,
      initialDate: _startDate,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
    );

    if (selectedDate == null || !mounted) {
      return;
    }

    setState(() {
      _startDate = DateUtils.dateOnly(selectedDate);
    });

    context
        .read<CreateEnrollmentViewModel>()
        .clearError();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final studentId = _studentId;
    final planId = _planId;

    if (studentId == null || planId == null) {
      return;
    }

    final created = await context
        .read<CreateEnrollmentViewModel>()
        .createEnrollment(
      studentId: studentId,
      planId: planId,
      startDate: _startDate,
    );

    if (!mounted || !created) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'Matrícula cadastrada com sucesso.',
        ),
      ),
    );

    context.go('/enrollments');
  }

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<CreateEnrollmentViewModel>();

    final branding =
        context.watch<BrandingController>().branding;

    final primaryColor = branding.primaryColor;
    final dateFormat = DateFormat('dd/MM/yyyy');

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints:
        const BoxConstraints(maxWidth: 900),
        child: Column(
          crossAxisAlignment:
          CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                IconButton(
                  tooltip: 'Voltar',
                  onPressed: () =>
                      context.go('/enrollments'),
                  icon: const Icon(
                    Icons.arrow_back_rounded,
                  ),
                ),
                const SizedBox(width: 8),
                const Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Nova matrícula',
                      style: TextStyle(
                        color: Color(0xFF171A2C),
                        fontSize: 28,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(height: 7),
                    Text(
                      'Vincule um aluno a um plano da academia.',
                      style: TextStyle(
                        color: Color(0xFF74798D),
                        fontSize: 14,
                      ),
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
                borderRadius:
                BorderRadius.circular(18),
                border: Border.all(
                  color:
                  const Color(0xFFE5E7EF),
                ),
              ),
              child: viewModel.isLoading
                  ? const Padding(
                padding:
                EdgeInsets.symmetric(
                  vertical: 60,
                ),
                child: Center(
                  child:
                  CircularProgressIndicator(),
                ),
              )
                  : Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [Autocomplete<StudentSummary>(
                    displayStringForOption: (student) => student.name,
                    optionsBuilder: (textEditingValue) {
                      final search =
                      textEditingValue.text.trim().toLowerCase();

                      if (search.isEmpty) {
                        return const Iterable<StudentSummary>.empty();
                      }

                      return viewModel.students.where(
                            (student) =>
                            student.name.toLowerCase().contains(search),
                      );
                    },
                    onSelected: (student) {
                      setState(() {
                        _studentId = student.id;
                      });

                      viewModel.clearError();
                    },
                    fieldViewBuilder: (
                        context,
                        controller,
                        focusNode,
                        onFieldSubmitted,
                        ) {
                      return TextFormField(
                        controller: controller,
                        focusNode: focusNode,
                        enabled: !viewModel.isSubmitting,
                        decoration: const InputDecoration(
                          labelText: 'Aluno',
                          hintText: 'Digite o nome do aluno',
                          prefixIcon: Icon(Icons.search_rounded),
                        ),
                        validator: (_) {
                          if (_studentId == null) {
                            return 'Selecione um aluno.';
                          }

                          return null;
                        },
                        onChanged: (_) {
                          setState(() {
                            _studentId = null;
                          });

                          viewModel.clearError();
                        },
                      );
                    },
                  ),
                    const SizedBox(height: 20),
                    DropdownButtonFormField<String>(
                      decoration:
                      const InputDecoration(
                        labelText: 'Plano',
                        prefixIcon: Icon(
                          Icons.sell_outlined,
                        ),
                      ),
                      items: viewModel.plans
                          .map(
                            (plan) =>
                            DropdownMenuItem(
                              value: plan.id,
                              child: Text(
                                plan.name,
                              ),
                            ),
                      )
                          .toList(),
                      validator: (value) {
                        if (value == null) {
                          return 'Selecione um plano.';
                        }

                        return null;
                      },
                      onChanged:
                      viewModel.isSubmitting
                          ? null
                          : (value) {
                        setState(() {
                          _planId = value;
                        });

                        viewModel
                            .clearError();
                      },
                    ),
                    const SizedBox(height: 20),
                    InkWell(
                      onTap:
                      viewModel.isSubmitting
                          ? null
                          : _selectStartDate,
                      child: InputDecorator(
                        decoration:
                        const InputDecoration(
                          labelText:
                          'Data de início',
                          prefixIcon: Icon(
                            Icons
                                .calendar_today_outlined,
                          ),
                        ),
                        child: Text(
                          dateFormat.format(
                            _startDate,
                          ),
                        ),
                      ),
                    ),
                    if (viewModel.errorMessage !=
                        null) ...[
                      const SizedBox(height: 20),
                      Text(
                        viewModel.errorMessage!,
                        style: const TextStyle(
                          color: Colors.red,
                          fontWeight:
                          FontWeight.w600,
                        ),
                      ),
                    ],
                    const SizedBox(height: 28),
                    Align(
                      alignment:
                      Alignment.centerRight,
                      child: FilledButton.icon(
                        onPressed:
                        viewModel.isSubmitting
                            ? null
                            : _submit,
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
                          Icons
                              .check_rounded,
                        ),
                        label: const Text(
                          'Cadastrar matrícula',
                        ),
                        style:
                        FilledButton.styleFrom(
                          backgroundColor:
                          primaryColor,
                          foregroundColor:
                          Colors.white,
                          padding:
                          const EdgeInsets
                              .symmetric(
                            horizontal: 20,
                            vertical: 17,
                          ),
                        ),
                      ),
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
}
