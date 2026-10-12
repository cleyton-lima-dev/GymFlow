import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../data/reports_service.dart';
import 'report_presentation.dart';

/// Independent scroll axes keep the column navigation visible on long pages.
class ReportTable extends StatefulWidget {
  const ReportTable({super.key, required this.data});
  final ReportResult data;

  @override
  State<ReportTable> createState() => _ReportTableState();
}

class _ReportTableState extends State<ReportTable> {
  final horizontal = ScrollController();
  final vertical = ScrollController();

  @override
  void dispose() {
    horizontal.dispose();
    vertical.dispose();
    super.dispose();
  }

  double columnWidth(String column) {
    if (column == 'Aluno' || column.startsWith('Plano')) return 220;
    if (column.contains(r'(R$)')) return 125;
    return 180;
  }

  void move(int direction) {
    final position = horizontal.position;
    horizontal.animateTo(
      (position.pixels + direction * position.viewportDimension * .75).clamp(
        0.0,
        position.maxScrollExtent,
      ),
      duration: const Duration(milliseconds: 200),
      curve: Curves.easeOut,
    );
  }

  @override
  Widget build(BuildContext context) {
    final columns = visibleReportColumns(widget.data);
    return LayoutBuilder(
      builder: (context, constraints) {
        final width =
            columns.fold<double>(
              0,
              (sum, i) => sum + columnWidth(widget.data.columns[i]),
            ) +
            math.max(0, columns.length - 1) * 20 +
            32;
        final overflow = width > constraints.maxWidth;
        return Column(
          children: [
            Expanded(
              child: Scrollbar(
                controller: horizontal,
                thumbVisibility: true,
                trackVisibility: true,
                notificationPredicate: (n) => n.metrics.axis == Axis.horizontal,
                child: Scrollbar(
                  controller: vertical,
                  thumbVisibility: true,
                  trackVisibility: true,
                  notificationPredicate: (n) => n.metrics.axis == Axis.vertical,
                  child: SingleChildScrollView(
                    key: const ValueKey('reports-table-horizontal'),
                    controller: horizontal,
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.only(bottom: 16),
                    child: SizedBox(
                      width: math.max(width, constraints.maxWidth),
                      child: SingleChildScrollView(
                        controller: vertical,
                        child: DataTable(
                          horizontalMargin: 16,
                          columnSpacing: 20,
                          headingRowHeight: 56,
                          dataRowMinHeight: 48,
                          dataRowMaxHeight: 64,
                          headingRowColor: WidgetStatePropertyAll(
                            Theme.of(context)
                                .colorScheme
                                .surfaceContainerHighest,
                          ),
                          columns: columns
                              .map(
                                (i) => DataColumn(
                                  numeric: widget.data.columns[i].contains(
                                    r'(R$)',
                                  ),
                                  label: SizedBox(
                                    width: columnWidth(widget.data.columns[i]),
                                    child: Text(
                                      reportColumnLabel(widget.data.columns[i]),
                                      maxLines: 2,
                                    ),
                                  ),
                                ),
                              )
                              .toList(),
                          rows: widget.data.rows
                              .map(
                                (row) => DataRow(
                                  cells: columns.map((i) {
                                    final value = reportCellLabel(
                                      widget.data.columns[i],
                                      row[i],
                                    );
                                    return DataCell(
                                      SizedBox(
                                        width: columnWidth(
                                          widget.data.columns[i],
                                        ),
                                        child: Tooltip(
                                          message: value,
                                          child: SelectableText(
                                            value,
                                            maxLines: 2,
                                          ),
                                        ),
                                      ),
                                    );
                                  }).toList(),
                                ),
                              )
                              .toList(),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
            if (overflow)
              Row(
                children: [
                  IconButton(
                    tooltip: 'Colunas anteriores',
                    onPressed: () => move(-1),
                    icon: const Icon(Icons.arrow_back),
                  ),
                  const Expanded(
                    child: Text(
                      'Deslize a barra para ver mais colunas',
                      textAlign: TextAlign.center,
                    ),
                  ),
                  IconButton(
                    tooltip: 'Próximas colunas',
                    onPressed: () => move(1),
                    icon: const Icon(Icons.arrow_forward),
                  ),
                ],
              ),
          ],
        );
      },
    );
  }
}
