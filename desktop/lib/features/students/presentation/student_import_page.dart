import 'dart:async';

import 'package:avelri_gestao/app/theme/app_button_styles.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/data/student_import_api_service.dart';
import 'package:avelri_gestao/features/students/models/student_import_preview.dart';
import 'package:avelri_gestao/features/students/presentation/student_import_view_model.dart';
import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

class StudentImportPage extends StatelessWidget {
  const StudentImportPage({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    create: (context) => StudentImportViewModel(
      StudentImportApiService(context.read<ApiClient>()),
    ),
    child: const _StudentImportView(),
  );
}

class _StudentImportView extends StatefulWidget {
  const _StudentImportView();

  @override
  State<_StudentImportView> createState() => _StudentImportViewState();
}

class _StudentImportViewState extends State<_StudentImportView> {
  static const _pageSize = 40;
  int _previewPage = 0;
  int _resultPage = 0;
  bool _selectingFile = false;
  bool _allowPop = false;
  bool _handlingBack = false;

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<StudentImportViewModel>();
    final colors = Theme.of(context).colorScheme;
    final source = viewModel.source;
    final isBusy = _selectingFile || viewModel.isBusy;

    return PopScope<Object?>(
      canPop: _allowPop,
      onPopInvokedWithResult: (didPop, result) {
        if (!didPop) unawaited(_attemptBack(viewModel));
      },
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(30),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1250),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    OutlinedButton.icon(
                      key: const ValueKey('student-import-back'),
                      onPressed: () => _attemptBack(viewModel),
                      icon: const Icon(Icons.arrow_back_rounded),
                      label: const Text('Voltar'),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Importar alunos',
                        style: Theme.of(context).textTheme.headlineMedium,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                const Text(
                  'Importe cadastros de arquivos CSV ou XLSX. A importação não cria contas do aplicativo, matrículas, cobranças ou acesso físico.',
                ),
                const SizedBox(height: 20),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Wrap(
                      crossAxisAlignment: WrapCrossAlignment.center,
                      spacing: 16,
                      runSpacing: 12,
                      children: [
                        FilledButton.icon(
                          onPressed: isBusy ? null : _chooseFile,
                          icon: isBusy
                              ? const SizedBox.square(
                                  dimension: 18,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Icon(Icons.upload_file_outlined),
                          label: const Text('Selecionar CSV/XLSX'),
                        ),
                        if (source != null)
                          Text(
                            '${source.fileName} · ${source.rows.length} registros',
                          ),
                        const Text('Limite: 5 MB e 5.000 registros.'),
                      ],
                    ),
                  ),
                ),
                if (viewModel.errorMessage != null) ...[
                  const SizedBox(height: 12),
                  _MessageCard(message: viewModel.errorMessage!, isError: true),
                ],
                if (source != null) ...[
                  const SizedBox(height: 16),
                  _buildMappingCard(viewModel, colors),
                ],
                if (viewModel.preview != null) ...[
                  const SizedBox(height: 16),
                  _buildPreview(viewModel, colors),
                ],
                if (viewModel.result != null) ...[
                  const SizedBox(height: 16),
                  _buildResults(viewModel),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _chooseFile() async {
    setState(() => _selectingFile = true);
    XFile? selectedFile;
    try {
      final file = await openFile(
        acceptedTypeGroups: const [
          XTypeGroup(label: 'CSV ou XLSX', extensions: ['csv', 'xlsx']),
        ],
      );
      if (file == null || !mounted) return;
      selectedFile = file;
      final viewModel = context.read<StudentImportViewModel>();
      final bytes = await file.readAsBytes();
      if (!mounted) return;
      setState(() {
        _previewPage = 0;
        _resultPage = 0;
      });
      viewModel.loadFile(file.name, bytes);
    } catch (_) {
      if (!mounted || selectedFile == null) return;
      context.read<StudentImportViewModel>().loadFile(selectedFile.name, null);
    } finally {
      if (mounted) setState(() => _selectingFile = false);
    }
  }

  Future<void> _attemptBack(StudentImportViewModel viewModel) async {
    if (_handlingBack || !mounted) return;
    _handlingBack = true;
    try {
      if (_selectingFile || viewModel.isBusy) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Aguarde a operação de importação terminar antes de sair.',
            ),
          ),
        );
        return;
      }

      if (viewModel.source != null) {
        final confirmed = await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('Sair da importação?'),
            content: Text(
              viewModel.result == null
                  ? 'O arquivo selecionado, o mapeamento e a prévia desta tela serão descartados. Nenhuma importação será iniciada ao sair.'
                  : 'O resultado desta tela será fechado. A importação já concluída não será desfeita.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext, false),
                child: const Text('Continuar importando'),
              ),
              OutlinedButton(
                style: AppButtonStyles.destructiveOutlined(dialogContext),
                onPressed: () => Navigator.pop(dialogContext, true),
                child: const Text('Sair e descartar'),
              ),
            ],
          ),
        );
        if (confirmed != true || !mounted) return;
      }

      _navigateBack();
    } finally {
      _handlingBack = false;
    }
  }

  void _navigateBack() {
    if (!mounted) return;
    if (!context.canPop()) {
      context.go('/students');
      return;
    }
    setState(() => _allowPop = true);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.pop();
    });
  }

  Widget _buildMappingCard(
    StudentImportViewModel viewModel,
    ColorScheme colors,
  ) {
    final source = viewModel.source!;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Mapeamento de colunas',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 6),
            const Text(
              'Confirme qual coluna contém cada dado. No modelo Actuar, Cliente, Telefones, Aniversário e E-mail são sugeridos automaticamente. Assinaturas, condições, situação e responsável não alteram matrículas, cobranças ou estados do aluno.',
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 16,
              runSpacing: 12,
              children: [
                for (final field in StudentImportField.values)
                  SizedBox(
                    width: 270,
                    child: DropdownButtonFormField<int>(
                      initialValue: viewModel.mapping[field] ?? -1,
                      decoration: InputDecoration(
                        labelText: _fieldLabel(field),
                      ),
                      items: [
                        const DropdownMenuItem(
                          value: -1,
                          child: Text('Não mapear'),
                        ),
                        for (
                          var index = 0;
                          index < source.headers.length;
                          index++
                        )
                          DropdownMenuItem(
                            value: index,
                            child: Text(
                              source.headers[index].isEmpty
                                  ? 'Coluna ${index + 1} (sem título)'
                                  : source.headers[index],
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: viewModel.isBusy
                          ? null
                          : (value) => viewModel.setMapping(
                              field,
                              value == null || value < 0 ? null : value,
                            ),
                    ),
                  ),
              ],
            ),
            if (!viewModel.mappingIsUnique) ...[
              const SizedBox(height: 10),
              Text(
                'Use uma coluna diferente para cada campo.',
                style: TextStyle(color: colors.error),
              ),
            ],
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed:
                  viewModel.isBusy ||
                      !viewModel.hasRequiredMapping ||
                      !viewModel.mappingIsUnique
                  ? null
                  : () async {
                      await viewModel.createPreview();
                      if (mounted) setState(() => _previewPage = 0);
                    },
              icon: viewModel.isBusy
                  ? const SizedBox.square(
                      dimension: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.fact_check_outlined),
              label: const Text('Gerar prévia'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPreview(StudentImportViewModel viewModel, ColorScheme colors) {
    final preview = viewModel.preview!;
    if (preview.alreadyCompleted) {
      return const _MessageCard(
        message: 'Este conjunto de registros já foi importado. Nenhum aluno foi criado novamente.',
      );
    }
    final rows = preview.rows;
    final start = _previewPage * _pageSize;
    final visible = rows.skip(start).take(_pageSize).toList();
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Revisar prévia',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 12,
              runSpacing: 8,
              children: [
                _CountChip(label: 'Total', count: rows.length),
                _CountChip(label: 'Válidos', count: viewModel.validCount),
                _CountChip(label: 'Com erro', count: viewModel.errorCount),
                _CountChip(
                  label: 'Possíveis duplicados',
                  count: viewModel.duplicateCount,
                ),
              ],
            ),
            const SizedBox(height: 12),
            const Text(
              'Nome sozinho não identifica duplicidade. Para cada candidato por e-mail ou telefone, escolha criar outro cadastro ou ignorar. A coluna Situação do arquivo é ignorada; escolha abaixo a situação administrativa dos novos alunos.',
            ),
            const SizedBox(height: 14),
            for (final row in visible) _buildPreviewRow(viewModel, row, colors),
            if (rows.length > _pageSize)
              _PageControls(
                currentPage: _previewPage,
                pageSize: _pageSize,
                total: rows.length,
                onChanged: (page) => setState(() => _previewPage = page),
              ),
            const Divider(height: 28),
            Text(
              'Situação administrativa dos novos alunos',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            const Text(
              'Essa escolha vale apenas para os novos cadastros desta importação.',
            ),
            Wrap(
              spacing: 10,
              children: [
                ChoiceChip(
                  label: const Text('Criar como ativo'),
                  selected: viewModel.newStudentsActive == true,
                  onSelected: (_) => viewModel.chooseNewStudentsStatus(true),
                ),
                ChoiceChip(
                  label: const Text('Criar como inativo'),
                  selected: viewModel.newStudentsActive == false,
                  onSelected: (_) => viewModel.chooseNewStudentsStatus(false),
                ),
              ],
            ),
            const SizedBox(height: 8),
            FilledButton.icon(
              onPressed: viewModel.canConfirm
                  ? () => _confirm(viewModel)
                  : null,
              icon: viewModel.isBusy
                  ? const SizedBox.square(
                      dimension: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.playlist_add_check),
              label: const Text('Confirmar importação'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPreviewRow(
    StudentImportViewModel viewModel,
    StudentImportPreviewRow row,
    ColorScheme colors,
  ) {
    final parsed = viewModel.source!.rows.firstWhere(
      (item) => item.rowNumber == row.rowNumber,
    );
    String display(StudentImportField field) {
      final index = viewModel.mapping[field];
      return index == null || index >= parsed.cells.length
          ? '—'
          : _display(parsed.cells[index]);
    }

    final decision = viewModel.decisions[row.rowNumber];
    final options = row.candidates.isEmpty
        ? const {'Import': 'Importar', 'Ignore': 'Ignorar'}
        : const {'ImportAsNew': 'Criar separado', 'Ignore': 'Ignorar'};
    return Card(
      margin: const EdgeInsets.only(bottom: 9),
      color: row.isValid ? null : colors.errorContainer.withValues(alpha: .4),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 14,
              runSpacing: 6,
              children: [
                Text(
                  'Linha ${row.rowNumber}',
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                Text(display(StudentImportField.name)),
                Text(display(StudentImportField.email)),
                Text(display(StudentImportField.phone)),
                Text(display(StudentImportField.birthDate)),
              ],
            ),
            if (!row.isValid)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  row.errors.join(' '),
                  style: TextStyle(color: colors.error),
                ),
              ),
            for (final candidate in row.candidates)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(_candidateLabel(candidate)),
              ),
            if (row.isValid)
              Align(
                alignment: Alignment.centerRight,
                child: SizedBox(
                  width: 190,
                  child: DropdownButtonFormField<String>(
                    initialValue: decision,
                    decoration: const InputDecoration(labelText: 'Decisão'),
                    hint: const Text('Escolha'),
                    items: [
                      for (final option in options.entries)
                        DropdownMenuItem(
                          value: option.key,
                          child: Text(option.value),
                        ),
                    ],
                    onChanged: viewModel.isBusy
                        ? null
                        : (value) {
                            if (value != null) {
                              viewModel.chooseDecision(row.rowNumber, value);
                            }
                          },
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildResults(StudentImportViewModel viewModel) {
    final result = viewModel.result!;
    final rows = result.rows;
    final start = _resultPage * _pageSize;
    final visible = rows.skip(start).take(_pageSize).toList();
    final namesByRow = {
      for (final row in viewModel.source!.rows)
        row.rowNumber: row.cells[viewModel.mapping[StudentImportField.name]!],
    };
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Resultado da importação',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 12,
              children: [
                _CountChip(
                  label: 'Importados',
                  count: rows.where((row) => row.status == 'Imported').length,
                ),
                _CountChip(
                  label: 'Ignorados',
                  count: rows.where((row) => row.status == 'Ignored').length,
                ),
                _CountChip(
                  label: 'Erros',
                  count: rows.where((row) => row.status == 'Error').length,
                ),
              ],
            ),
            if (result.alreadyCompleted)
              const Padding(
                padding: EdgeInsets.only(top: 10),
                child: Text(
                  'Resultado recuperado de uma importação anterior; a operação não foi repetida.',
                ),
              ),
            const SizedBox(height: 12),
            for (final row in visible)
              ListTile(
                dense: true,
                leading: Icon(
                  _resultIcon(row.status),
                  color: _resultColor(row.status),
                ),
                title: Text(
                  'Linha ${row.rowNumber} · ${namesByRow[row.rowNumber] ?? ''}',
                ),
                subtitle: Text(row.message),
              ),
            if (rows.length > _pageSize)
              _PageControls(
                currentPage: _resultPage,
                pageSize: _pageSize,
                total: rows.length,
                onChanged: (page) => setState(() => _resultPage = page),
              ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirm(StudentImportViewModel viewModel) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Confirmar importação'),
        content: const Text(
          'Somente os novos cadastros serão criados. Dados existentes, contas, convites, matrículas, cobranças e acessos não serão alterados.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Revisar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Importar'),
          ),
        ],
      ),
    );
    if (confirmed == true && mounted) {
      await viewModel.confirmImport();
      if (mounted) setState(() => _resultPage = 0);
    }
  }

  String _fieldLabel(StudentImportField field) => switch (field) {
    StudentImportField.name => 'Nome *',
    StudentImportField.email => 'E-mail (opcional)',
    StudentImportField.phone => 'Telefone (opcional)',
    StudentImportField.birthDate => 'Nascimento (opcional)',
  };

  String _display(String value) => value.isEmpty ? '—' : value;

  String _candidateLabel(StudentImportDuplicateCandidate candidate) {
    final source = candidate.sourceRowNumber == null
        ? 'Aluno existente${candidate.isArchived ? ' (arquivado)' : ''}'
        : 'Possível repetição na linha ${candidate.sourceRowNumber}';
    final contact = [
      candidate.email,
      candidate.phone,
    ].where((value) => value != null && value.isNotEmpty).join(' · ');
    return '$source: ${candidate.name}${contact.isEmpty ? '' : ' · $contact'} · coincidência por ${candidate.matchBy}';
  }

  IconData _resultIcon(String status) => switch (status) {
    'Imported' => Icons.check_circle_outline,
    'Ignored' => Icons.remove_circle_outline,
    _ => Icons.error_outline,
  };

  Color _resultColor(String status) => switch (status) {
    'Imported' => AvelriSemanticColors.success,
    'Ignored' => AvelriSemanticColors.inactive,
    _ => Theme.of(context).colorScheme.error,
  };
}

class _CountChip extends StatelessWidget {
  const _CountChip({required this.label, required this.count});

  final String label;
  final int count;

  @override
  Widget build(BuildContext context) => Chip(label: Text('$label: $count'));
}

class _PageControls extends StatelessWidget {
  const _PageControls({
    required this.currentPage,
    required this.pageSize,
    required this.total,
    required this.onChanged,
  });

  final int currentPage;
  final int pageSize;
  final int total;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    final pages = (total / pageSize).ceil();
    return Row(
      mainAxisAlignment: MainAxisAlignment.end,
      children: [
        Text(
          '${currentPage * pageSize + 1}–${(currentPage * pageSize + pageSize).clamp(1, total)} de $total',
        ),
        IconButton(
          onPressed: currentPage > 0 ? () => onChanged(currentPage - 1) : null,
          icon: const Icon(Icons.chevron_left),
        ),
        Text('${currentPage + 1} / $pages'),
        IconButton(
          onPressed: currentPage + 1 < pages
              ? () => onChanged(currentPage + 1)
              : null,
          icon: const Icon(Icons.chevron_right),
        ),
      ],
    );
  }
}

class _MessageCard extends StatelessWidget {
  const _MessageCard({required this.message, this.isError = false});

  final String message;
  final bool isError;

  @override
  Widget build(BuildContext context) => Card(
    color: isError ? Theme.of(context).colorScheme.errorContainer : null,
    child: Padding(padding: const EdgeInsets.all(14), child: Text(message)),
  );
}
