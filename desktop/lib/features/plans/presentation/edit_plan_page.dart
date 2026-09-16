import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';
import 'package:avelri_gestao/features/plans/presentation/edit_plan_view_model.dart';

class EditPlanPage extends StatelessWidget {
  const EditPlanPage({
    super.key,
    required this.plan,
  });

  final PlanSummary plan;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          EditPlanViewModel(PlansService(context.read<ApiClient>())),
      child: _EditPlanView(plan: plan),
    );
  }
}

class _EditPlanView extends StatefulWidget {
  const _EditPlanView({
    required this.plan,
  });

  final PlanSummary plan;

  @override
  State<_EditPlanView> createState() => _EditPlanViewState();
}

class _EditPlanViewState extends State<_EditPlanView> {
  final _formKey = GlobalKey<FormState>();

  final _nameController = TextEditingController();
  final _priceController = TextEditingController();
  final _durationController = TextEditingController();

  PlanBillingCycle _billingCycle = PlanBillingCycle.monthly;

  @override
  void initState() {
    super.initState();

    _nameController.text = widget.plan.name;
    _priceController.text = widget.plan.price
        .toStringAsFixed(2)
        .replaceAll('.', ',');
    _durationController.text = widget.plan.durationMonths.toString();
    _billingCycle = widget.plan.billingCycle;
  }

  @override
  void dispose() {
    _nameController.dispose();
    _priceController.dispose();
    _durationController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusManager.instance.primaryFocus?.unfocus();

    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final price = double.parse(
      _priceController.text.trim().replaceAll(',', '.'),
    );

    final durationMonths = int.parse(
      _durationController.text.trim(),
    );

    final updated =
    await context.read<EditPlanViewModel>().updatePlan(
      planId: widget.plan.id,
      name: _nameController.text,
      price: price,
      durationMonths: durationMonths,
      billingCycle: _billingCycle,
    );

    if (!mounted || !updated) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Plano atualizado com sucesso.'),
      ),
    );

    context.go('/plans');
  }

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<EditPlanViewModel>();
    final branding = context.watch<BrandingController>().branding;
    final primaryColor = branding.primaryColor;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 900),
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
                        : () => context.go('/plans'),
                    icon: const Icon(Icons.arrow_back_rounded),
                  ),
                  const SizedBox(width: 8),
                  const Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Editar plano',
                        style: TextStyle(
                          color: Color(0xFF171A2C),
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      SizedBox(height: 7),
                      Text(
                        'Atualize as condições comerciais do plano.',
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
                  borderRadius: BorderRadius.circular(18),
                  border: Border.all(
                    color: const Color(0xFFE5E7EF),
                  ),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    TextFormField(
                      controller: _nameController,
                      textCapitalization: TextCapitalization.words,
                      onChanged: (_) => viewModel.clearError(),
                      validator: (value) {
                        final name = value?.trim() ?? '';

                        if (name.isEmpty) {
                          return 'Informe o nome do plano.';
                        }

                        return null;
                      },
                      decoration: const InputDecoration(
                        labelText: 'Nome do plano',
                        hintText: 'Ex.: Mensal',
                        prefixIcon: Icon(Icons.sell_outlined),
                      ),
                    ),
                    const SizedBox(height: 20),
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: TextFormField(
                            controller: _priceController,
                            keyboardType:
                            const TextInputType.numberWithOptions(
                              decimal: true,
                            ),
                            onChanged: (_) => viewModel.clearError(),
                            validator: (value) {
                              final price = double.tryParse(
                                (value ?? '')
                                    .trim()
                                    .replaceAll(',', '.'),
                              );

                              if (price == null || price <= 0) {
                                return 'Informe um valor válido.';
                              }

                              return null;
                            },
                            decoration: const InputDecoration(
                              labelText: 'Valor da cobrança',
                              hintText: '100,00',
                              prefixText: 'R\$ ',
                            ),
                          ),
                        ),
                        const SizedBox(width: 18),
                        Expanded(
                          child: TextFormField(
                            controller: _durationController,
                            keyboardType: TextInputType.number,
                            onChanged: (_) => viewModel.clearError(),
                            validator: (value) {
                              final duration =
                              int.tryParse(value?.trim() ?? '');

                              if (duration == null || duration <= 0) {
                                return 'Informe a duração.';
                              }

                              return null;
                            },
                            decoration: const InputDecoration(
                              labelText: 'Duração do contrato',
                              hintText: '12',
                              suffixText: 'meses',
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 20),
                    DropdownButtonFormField<PlanBillingCycle>(
                      initialValue: _billingCycle,
                      decoration: const InputDecoration(
                        labelText: 'Recorrência da cobrança',
                        prefixIcon:
                        Icon(Icons.autorenew_rounded),
                      ),
                      items: const [
                        DropdownMenuItem(
                          value: PlanBillingCycle.monthly,
                          child: Text('Mensal'),
                        ),
                        DropdownMenuItem(
                          value: PlanBillingCycle.quarterly,
                          child: Text('Trimestral'),
                        ),
                        DropdownMenuItem(
                          value: PlanBillingCycle.semiannual,
                          child: Text('Semestral'),
                        ),
                        DropdownMenuItem(
                          value: PlanBillingCycle.annual,
                          child: Text('Anual'),
                        ),
                      ],
                      onChanged: viewModel.isLoading
                          ? null
                          : (value) {
                        if (value != null) {
                          _billingCycle = value;
                          viewModel.clearError();
                        }
                      },
                    ),
                    if (viewModel.errorMessage != null) ...[
                      const SizedBox(height: 20),
                      Text(
                        viewModel.errorMessage!,
                        style: const TextStyle(
                          color: Colors.red,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                    const SizedBox(height: 28),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        OutlinedButton(
                          onPressed: viewModel.isLoading
                              ? null
                              : () => context.go('/plans'),
                          child: const Text('Cancelar'),
                        ),
                        const SizedBox(width: 12),
                        FilledButton.icon(
                          onPressed:
                          viewModel.isLoading ? null : _submit,
                          style: FilledButton.styleFrom(
                            backgroundColor: primaryColor,
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(
                              horizontal: 22,
                              vertical: 17,
                            ),
                          ),
                          icon: viewModel.isLoading
                              ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                              : const Icon(Icons.save_rounded),
                          label: const Text('Salvar alterações'),
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
}

