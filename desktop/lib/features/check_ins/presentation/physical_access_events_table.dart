import 'package:avelri_gestao/features/check_ins/models/physical_access_event.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

class PhysicalAccessEventsTable extends StatelessWidget {
  const PhysicalAccessEventsTable({required this.items, super.key});

  final List<PhysicalAccessEvent> items;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;

    return LayoutBuilder(
      builder: (context, constraints) {
        return ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: ConstrainedBox(
              constraints: BoxConstraints(minWidth: constraints.maxWidth),
              child: DataTable(
                columnSpacing: 34,
                horizontalMargin: 16,
                headingRowColor: WidgetStateProperty.all(
                  colors.surfaceContainerHighest,
                ),
                headingTextStyle: TextStyle(
                  color: colors.onSurfaceVariant,
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                ),
                columns: const [
                  DataColumn(label: Text('ALUNO')),
                  DataColumn(label: Text('DATA / HORA')),
                  DataColumn(label: Text('DECISÃO')),
                  DataColumn(label: Text('MOTIVO')),
                  DataColumn(label: Text('ORIGEM')),
                ],
                rows: items.map((event) {
                  final isAllowed = event.decision == 1;
                  final decisionColor = isAllowed
                      ? AvelriSemanticColors.success
                      : colors.error;

                  return DataRow(
                    cells: [
                      DataCell(
                        Text(
                          event.studentName?.trim().isNotEmpty == true
                              ? event.studentName!
                              : 'Não identificado',
                        ),
                      ),
                      DataCell(
                        Text(
                          DateFormat('dd/MM/yyyy HH:mm')
                              .format(event.occurredAt.toLocal()),
                        ),
                      ),
                      DataCell(
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 10,
                            vertical: 6,
                          ),
                          decoration: BoxDecoration(
                            color: decisionColor.withValues(alpha: 0.10),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            _decisionLabel(event.decision),
                            style: TextStyle(
                              color: colors.onSurface,
                              fontWeight: FontWeight.w700,
                              fontSize: 12,
                            ),
                          ),
                        ),
                      ),
                      DataCell(Text(_reasonLabel(event.reason))),
                      DataCell(Text(_sourceLabel(event.source))),
                    ],
                  );
                }).toList(),
              ),
            ),
          ),
        );
      },
    );
  }

  String _decisionLabel(int decision) => switch (decision) {
    1 => 'Permitido',
    2 => 'Negado',
    _ => 'Desconhecido',
  };

  String _reasonLabel(int reason) => switch (reason) {
    1 => 'Matrícula elegível',
    2 => 'Liberação manual',
    100 => 'Credencial não encontrada',
    101 => 'Credencial inativa',
    102 => 'Aluno inativo',
    103 => 'Aluno arquivado',
    104 => 'Sem matrícula válida',
    105 => 'Restrição financeira',
    106 => 'Bloqueio manual',
    107 => 'Restrição do plano',
    108 => 'Sem autorização offline',
    _ => 'Motivo desconhecido',
  };

  String _sourceLabel(int source) => switch (source) {
    1 => 'Online',
    2 => 'Cache offline',
    3 => 'Negado offline',
    _ => 'Origem desconhecida',
  };
}
