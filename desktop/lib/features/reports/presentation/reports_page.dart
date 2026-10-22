import 'package:flutter/material.dart';

import 'report_table.dart';

import 'package:intl/intl.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/erp_access_policy.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/features/reports/data/reports_service.dart';
import 'package:avelri_gestao/features/reports/presentation/reports_view_model.dart';
import 'package:avelri_gestao/features/reports/presentation/save_report.dart';
import 'package:avelri_gestao/features/reports/presentation/report_presentation.dart';
import 'package:avelri_gestao/features/check_ins/presentation/student_picker_dialog.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';

class ReportsPage extends StatelessWidget {
  const ReportsPage({super.key});
  @override
  Widget build(BuildContext context) {
    final gymId = context.watch<SessionController>().user?.gymId;
    final erpEnabled =
        gymId != null &&
        context.watch<ErpFeatureController>().isEnabledForGym(gymId);
    return ChangeNotifierProvider(
      key: ValueKey(erpEnabled),
      create: (context) =>
          ReportsViewModel(ReportsService(context.read<ApiClient>()))
            ..load('students', {}),
      child: _ReportsView(erpEnabled: erpEnabled),
    );
  }
}

class _ReportsView extends StatefulWidget {
  const _ReportsView({required this.erpEnabled});
  final bool erpEnabled;
  @override
  State<_ReportsView> createState() => _ReportsViewState();
}

class _ReportsViewState extends State<_ReportsView> {
  String kind = 'students', status = '', enrollment = '', view = 'charges';
  final search = TextEditingController();
  DateTimeRange? period;
  StudentSummary? student;
  bool dirty = false;
  String resultKind = 'students', resultView = 'charges';
  static const kinds = {
    'students': 'Alunos',
    'financial': 'Financeiro',
    'enrollments': 'Matrículas',
    'attendance': 'Frequência',
    'access-events': 'Eventos de acesso',
  };
  static const statuses = {
    'students': {
      'active': 'Ativo',
      'inactive': 'Inativo',
      'archived': 'Arquivado',
    },
    'financial': {
      'pending': 'Pendente',
      'overdue': 'Vencida',
      'paid': 'Paga',
      'cancelled': 'Cancelada',
    },
    'enrollments': {
      'active': 'Vigente',
      'future': 'Futura',
      'expired': 'Expirada',
      'pending-payment': 'Pagamento pendente',
      'cancelled': 'Cancelada',
    },
    'attendance': {'manual': 'Manual', 'physical': 'Confirmação de passagem'},
    'access-events': {'allowed': 'Autorizado', 'denied': 'Negado'},
  };
  Map<String, String> get filters => {
    if (status.isNotEmpty) 'status': status,
    if (search.text.trim().isNotEmpty) 'search': search.text.trim(),
    if (student != null) 'studentId': student!.id,
    if (kind == 'students' && enrollment.isNotEmpty)
      'enrollmentStatus': enrollment,
    if (kind == 'financial') 'view': view,
    if (kind != 'students' && period != null) ...{
      'fromDate': DateFormat('yyyy-MM-dd').format(period!.start),
      'toDate': DateFormat('yyyy-MM-dd').format(period!.end),
    },
  };
  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  Future<void> apply() async {
    final requestedKind = kind;
    final requestedView = view;
    setState(() => dirty = false);
    await context.read<ReportsViewModel>().load(kind, filters);
    if (mounted) {
      setState(() {
        resultKind = requestedKind;
        resultView = requestedView;
      });
    }
  }

  Widget select(
    String label,
    String value,
    Map<String, String> options,
    void Function(String) changed,
    bool busy,
  ) => SizedBox(
    width: 210,
    child: DropdownButtonFormField<String>(
      key: ValueKey('$label-$kind-$value'),
      initialValue: value,
      decoration: InputDecoration(labelText: label, isDense: true),
      isExpanded: true,
      items: options.entries
          .map(
            (e) => DropdownMenuItem(
              value: e.key,
              child: Text(e.value, overflow: TextOverflow.ellipsis),
            ),
          )
          .toList(),
      onChanged: busy
          ? null
          : (v) => setState(() {
              changed(v!);
              dirty = true;
            }),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final model = context.watch<ReportsViewModel>();
    final data = model.result;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Relatórios',
            style: Theme.of(context).textTheme.headlineMedium
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 12),
          Text(
            'Tipo de relatório',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: kinds.entries
                .where(
                  (entry) =>
                      widget.erpEnabled ||
                      !ErpAccessPolicy.isErpReport(entry.key),
                )
                .map(
                  (entry) => ChoiceChip(
                    label: Text(entry.value),
                    selected: kind == entry.key,
                    onSelected: model.busy
                        ? null
                        : (_) => setState(() {
                            kind = entry.key;
                            status = '';
                            enrollment = '';
                            view = 'charges';
                            dirty = true;
                          }),
                  ),
                )
                .toList(),
          ),
          const SizedBox(height: 16),
          Text('Filtros', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Wrap(
                spacing: 12,
                runSpacing: 10,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  if (kind != 'financial' || view == 'charges')
                    select(
                      kind == 'attendance' ? 'Origem' : 'Situação',
                      status,
                      {'': 'Todas', ...statuses[kind]!},
                      (v) => status = v,
                      model.busy,
                    ),
                  if (kind == 'students' && widget.erpEnabled)
                    select(
                      'Matrícula',
                      enrollment,
                      {
                        '': 'Todas',
                        'valid': 'Com matrícula vigente',
                        'none': 'Sem matrícula vigente',
                      },
                      (v) => enrollment = v,
                      model.busy,
                    ),
                  if (kind == 'financial')
                    select(
                      'Visão financeira',
                      view,
                      {
                        'charges': 'Cobranças por vencimento',
                        'payments': 'Pagamentos',
                        'overdue': 'Inadimplência',
                        'revenue': 'Receita recebida',
                      },
                      (v) {
                        view = v;
                        status = '';
                      },
                      model.busy,
                    ),
                  SizedBox(
                    width: 260,
                    child: TextField(
                      controller: search,
                      enabled: !model.busy,
                      maxLength: 100,
                      decoration: const InputDecoration(
                        labelText: 'Buscar aluno por nome',
                        counterText: '',
                        isDense: true,
                      ),
                      onChanged: (_) => setState(() => dirty = true),
                      onSubmitted: (_) => apply(),
                    ),
                  ),
                  OutlinedButton.icon(
                    icon: const Icon(Icons.person_search),
                    label: SizedBox(
                      width: 170,
                      child: Text(
                        student?.name ?? 'Todos os alunos',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    onPressed: model.busy
                        ? null
                        : () async {
                            final chosen = await showDialog<StudentSummary>(
                              context: context,
                              builder: (_) => StudentPickerDialog(
                                service: StudentsService(
                                  context.read<ApiClient>(),
                                ),
                              ),
                            );
                            if (chosen != null && mounted) {
                              setState(() {
                                student = chosen;
                                dirty = true;
                              });
                            }
                          },
                  ),
                  if (student != null)
                    IconButton(
                      tooltip: 'Limpar aluno',
                      onPressed: model.busy
                          ? null
                          : () => setState(() {
                              student = null;
                              dirty = true;
                            }),
                      icon: const Icon(Icons.person_remove_outlined),
                    ),
                  if (kind != 'students')
                    OutlinedButton.icon(
                      icon: const Icon(Icons.date_range),
                      label: Text(
                        period == null
                            ? 'Período: mês da academia'
                            : '${DateFormat('dd/MM/yyyy').format(period!.start)} a ${DateFormat('dd/MM/yyyy').format(period!.end)}',
                      ),
                      onPressed: model.busy
                          ? null
                          : () async {
                              final range = await showDateRangePicker(
                                context: context,
                                firstDate: DateTime(2000),
                                lastDate: DateTime(2100),
                                initialDateRange: period,
                                helpText: 'Período inclusivo (máximo 366 dias)',
                              );
                              if (range != null && mounted) {
                                setState(() {
                                  period = range;
                                  dirty = true;
                                });
                              }
                            },
                    ),
                  if (period != null && kind != 'students')
                    IconButton(
                      tooltip: 'Restaurar mês da academia',
                      onPressed: model.busy
                          ? null
                          : () => setState(() {
                              period = null;
                              dirty = true;
                            }),
                      icon: const Icon(Icons.event_repeat),
                    ),
                  FilledButton.icon(
                    onPressed: model.busy ? null : apply,
                    icon: const Icon(Icons.search),
                    label: const Text('Consultar'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          if (dirty)
            const Text(
              'Filtros alterados. Clique em Consultar antes de exportar.',
            ),
          if (model.busy) const LinearProgressIndicator(),
          if (model.error != null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 16),
              child: Text(
                model.error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          const SizedBox(height: 12),
          Text('Resultados', style: Theme.of(context).textTheme.titleMedium),
          if (data != null) ...[
            const SizedBox(height: 8),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Text(data.title, style: Theme.of(context).textTheme.titleLarge),
                Tooltip(
                  message: reportExplanation(resultKind, resultView),
                  child: const Icon(Icons.info_outline, size: 20),
                ),
                OutlinedButton.icon(
                  onPressed:
                      model.busy ||
                          dirty ||
                          data.total == 0 ||
                          data.total > 10000
                      ? null
                      : () async {
                          final bytes = await model.export();
                          if (bytes == null || !context.mounted) return;
                          try {
                            final path = await saveReport(
                              context,
                              bytes,
                              'avelri-$kind.csv',
                            );
                            if (path != null && context.mounted) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                SnackBar(
                                  content: Text('Arquivo salvo em $path'),
                                ),
                              );
                            }
                          } catch (_) {
                            if (context.mounted) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text(
                                    'Não foi possível salvar. Verifique o caminho e a permissão da pasta.',
                                  ),
                                ),
                              );
                            }
                          }
                        },
                  icon: const Icon(Icons.download),
                  label: Text(
                    model.isExporting ? 'Exportando…' : 'Exportar CSV',
                  ),
                ),
              ],
            ),

            Text(reportPeriodLabel(data)),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 8,
              children: data.totals.entries
                  .map(
                    (e) => Chip(
                      label: Text('${reportColumnLabel(e.key)}: ${e.value}'),
                    ),
                  )
                  .toList(),
            ),
            if (data.total > 10000)
              const Text(
                'Restrinja os filtros para exportar no máximo 10.000 registros.',
              ),
            if (data.rows.isEmpty)
              const Padding(
                padding: EdgeInsets.all(20),
                child: Text(
                  'Nenhum registro encontrado para os filtros aplicados.',
                ),
              )
            else
              Card(
                clipBehavior: Clip.antiAlias,
                child: SizedBox(
                  height: (MediaQuery.sizeOf(context).height * .43).clamp(
                    240.0,
                    500.0,
                  ),
                  child: ReportTable(data: data),
                ),
              ),
            Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Text(
                  '${data.total} registros | Página ${data.page} de ${data.pages == 0 ? 1 : data.pages}',
                ),
                IconButton(
                  tooltip: 'Página anterior',
                  onPressed: model.busy || dirty || data.page <= 1
                      ? null
                      : () => model.goTo(data.page - 1),
                  icon: const Icon(Icons.chevron_left),
                ),
                IconButton(
                  tooltip: 'Próxima página',
                  onPressed: model.busy || dirty || data.page >= data.pages
                      ? null
                      : () => model.goTo(data.page + 1),
                  icon: const Icon(Icons.chevron_right),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
