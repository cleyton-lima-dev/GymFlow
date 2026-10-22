import 'dart:io';
import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/data/column_header_mapper.dart';
import 'package:avelri_gestao/features/students/data/migration_import_templates.dart';
import 'package:avelri_gestao/features/students/data/student_import_parser.dart';
import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

Map<String, dynamic> buildMigrationRowPayload(
  int rowNumber,
  List<String> fields,
  Map<String, int?> mapping,
  List<String> cells,
) {
  String? value(String field) {
    final index = mapping[field];
    if (index == null || index >= cells.length) return null;
    final cell = cells[index].trim();
    return cell.isEmpty ? null : cell;
  }

  return {
    'rowNumber': rowNumber,
    for (final field in fields) field: value(field),
  };
}

Future<void> saveMigrationTemplateFile(
  MigrationImportTemplate template,
  String path,
) async {
  final data = await rootBundle.load(template.assetPath);
  await File(path).writeAsBytes(
    data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes),
    flush: true,
  );
}

String migrationPreviewRevision({
  required String sessionId,
  required int entityType,
  required String fileName,
  required List<String> fields,
  required Map<String, int?> mapping,
  required List<Map<String, dynamic>> rows,
}) => jsonEncode({
  'sessionId': sessionId,
  'entityType': entityType,
  'fileName': fileName,
  'mapping': [
    for (final field in fields) [field, mapping[field]],
  ],
  'rows': rows,
});

class MigrationWizardPage extends StatefulWidget {
  const MigrationWizardPage({super.key});
  @override
  State<MigrationWizardPage> createState() => _MigrationWizardPageState();
}

class _MigrationWizardPageState extends State<MigrationWizardPage> {
  final _name = TextEditingController();
  final _source = TextEditingController();
  bool _busy = false;
  int _type = 0;
  String? _sessionId;
  String? _error;
  String? _info;
  ParsedStudentImportFile? _file;
  Map<String, dynamic>? _preview;
  Map<String, dynamic>? _session;
  List<Map<String, dynamic>>? _sessions;
  final Map<String, int?> _mapping = {};
  final Set<String> _ambiguousFields = {};
  final Map<int, String> _actions = {};
  final Map<int, String?> _targets = {};
  final Map<String, String> _selectedStudentLabels = {};
  final Map<int, String?> _financialStateResolutions = {};
  String? _previewRevision;
  ApiClient get _api => context.read<ApiClient>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _run(_loadSessions);
    });
  }

  @override
  void dispose() {
    _name.dispose();
    _source.dispose();
    super.dispose();
  }

  Future<void> _run(Future<void> Function() task) async {
    setState(() {
      _busy = true;
      _error = null;
      _info = null;
    });
    try {
      await task();
    } catch (e) {
      if (mounted) {
        setState(
          () => _error = e.toString().length < 500
              ? e.toString()
              : 'Operação não concluída. Revise os dados.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _load() async {
    _session = Map<String, dynamic>.from(
      await _api.get('api/migrations/${_sessionId!}') as Map,
    );
    if (mounted) setState(() {});
  }

  Future<void> _loadSessions() async {
    final response = await _api.get('api/migrations') as List;
    _sessions = response
        .map((item) => Map<String, dynamic>.from(item as Map))
        .toList();
    if (mounted) setState(() {});
  }

  Future<void> _resumeSession(String sessionId) => _run(() async {
    _sessionId = sessionId;
    _preview = null;
    _previewRevision = null;
    _file = null;
    _mapping.clear();
    _ambiguousFields.clear();
    _actions.clear();
    _targets.clear();
    _financialStateResolutions.clear();
    await _load();
  });

  Future<void> _backToSessions() => _run(() async {
    _sessionId = null;
    _session = null;
    _preview = null;
    _previewRevision = null;
    _file = null;
    _mapping.clear();
    _ambiguousFields.clear();
    _actions.clear();
    _targets.clear();
    await _loadSessions();
  });

  String _sessionStatusLabel(Map<String, dynamic> session) {
    final expiresAt = DateTime.tryParse(session['expiresAt'].toString());
    if (session['status'] != 3 &&
        expiresAt != null &&
        expiresAt.isBefore(DateTime.now())) {
      return 'Expirada';
    }
    return switch (session['status']) {
      1 => 'Em preparação',
      2 => 'Em liberação',
      3 => 'Liberada',
      _ => 'Status desconhecido',
    };
  }

  Future<void> _create() => _run(() async {
    final x = await _api.post(
      'api/migrations',
      body: {'name': _name.text.trim(), 'sourceNamespace': _source.text.trim()},
    ) as Map;
    _sessionId = x['id'] as String;
    await _load();
  });
  Future<void> _pick() async {
    final file = await openFile(
      acceptedTypeGroups: const [
        XTypeGroup(label: 'CSV ou XLSX', extensions: ['csv', 'xlsx']),
      ],
    );
    if (file == null) return;
    try {
      _file = parseStudentImportFile(file.name, await file.readAsBytes());
      _preview = null;
      _previewRevision = null;
      _mapping.clear();
      _ambiguousFields.clear();
      for (final f in migrationImportTemplates[_type].fields) {
        final suggestion = ColumnHeaderMapper.suggest(_file!.headers, f);
        _mapping[f] = suggestion.index;
        if (suggestion.isAmbiguous) _ambiguousFields.add(f);
      }
      setState(() {});
    } catch (e) {
      setState(() => _error = e.toString());
    }
  }

  Future<void> _downloadTemplate() => _run(() async {
    final template = migrationImportTemplates[_type];
    final location = await getSaveLocation(
      suggestedName: template.fileName,
      acceptedTypeGroups: const [
        XTypeGroup(label: 'CSV', extensions: ['csv']),
      ],
    );
    if (location == null) return;
    await saveMigrationTemplateFile(template, location.path);
    _info = 'Modelo salvo. Substitua os dados fictícios antes de importar.';
  });

  static const _fieldLabels = {
    'externalId': 'ID externo',
    'name': 'Nome',
    'email': 'E-mail',
    'phone': 'Telefone',
    'birthDate': 'Data de nascimento',
    'price': 'Preço contratado',
    'durationMonths': 'Duração (meses)',
    'billingCycle': 'Ciclo de cobrança',
    'studentExternalId': 'ID externo do aluno',
    'planExternalId': 'ID externo do plano',
    'enrollmentExternalId': 'ID externo da matrícula',
    'chargeExternalId': 'ID externo da cobrança',
    'enrollmentChargeState': 'Situação informada na matrícula',
    'startDate': 'Data de início',
    'endDate': 'Data final',
    'chargeState': 'Situação da cobrança',
    'chargeDueDate': 'Vencimento da cobrança',
  };

  String _fieldLabel(String field) => _fieldLabels[field] ?? field;

  Future<void> _selectExistingStudent(int rowNumber, String? name) async {
    final selected = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) =>
          _MigrationStudentPicker(api: _api, initialSearch: name ?? ''),
    );
    if (!mounted || selected == null) return;
    final id = selected['id'] as String;
    setState(() {
      _targets[rowNumber] = id;
      _selectedStudentLabels[id] = selected['name'] as String;
    });
  }

  Future<void> _resumeBatch(String batchId) => _run(() async {
    final batch = Map<String, dynamic>.from(
      await _api.get('api/migrations/${_sessionId!}/batches/$batchId/review')
          as Map,
    );
    _type = (batch['entityType'] as int) - 1;
    _preview = batch;
    _previewRevision = null;
    _file = null;
    _mapping.clear();
    _ambiguousFields.clear();
    _actions.clear();
    _targets.clear();
    _financialStateResolutions.clear();
    final savedDecisions = (batch['decisions'] as List? ?? const [])
        .cast<Map>();
    for (final raw in (batch['rows'] as List).cast<Map>()) {
      final row = Map<String, dynamic>.from(raw);
      final number = row['rowNumber'] as int;
      Map<String, dynamic>? saved;
      for (final decision in savedDecisions) {
        if (decision['rowNumber'] == number) {
          saved = Map<String, dynamic>.from(decision);
          break;
        }
      }
      _actions[number] =
          saved?['action'] as String? ??
          (row['isValid'] == true && (row['candidates'] as List).isEmpty
              ? 'ImportNew'
              : 'Ignore');
      _targets[number] = saved?['targetId'] as String?;
      _financialStateResolutions[number] =
          saved?['financialStateResolution'] as String?;
    }
  });

  Future<void> _previewFile() => _run(() async {
    final rows = _file!.rows
        .map(
          (row) => buildMigrationRowPayload(
            row.rowNumber,
            migrationImportTemplates[_type].fields,
            _mapping,
            row.cells,
          ),
        )
        .toList();
    _preview = Map<String, dynamic>.from(
      await _api.post(
        'api/migrations/${_sessionId!}/preview',
        body: {'entityType': _type + 1, 'rows': rows},
      ) as Map,
    );
    _previewRevision = migrationPreviewRevision(
      sessionId: _sessionId!,
      entityType: _type + 1,
      fileName: _file!.fileName,
      fields: migrationImportTemplates[_type].fields,
      mapping: _mapping,
      rows: rows,
    );
    _actions.clear();
    _targets.clear();
    _financialStateResolutions.clear();
    final savedDecisions = (_preview!['decisions'] as List? ?? const [])
        .cast<Map>();
    for (final x in (_preview!['rows'] as List).cast<Map>()) {
      final rowNumber = x['rowNumber'] as int;
      Map<String, dynamic>? saved;
      for (final decision in savedDecisions) {
        if (decision['rowNumber'] == rowNumber) {
          saved = Map<String, dynamic>.from(decision);
          break;
        }
      }
      _actions[rowNumber] =
          saved?['action'] as String? ??
          (x['isValid'] == true && (x['candidates'] as List).isEmpty
              ? 'ImportNew'
              : 'Ignore');
      _targets[rowNumber] = saved?['targetId'] as String?;
      _financialStateResolutions[rowNumber] =
          saved?['financialStateResolution'] as String?;
    }
    setState(() {});
  });

  void _changeMapping(String field, int? value) {
    if (_mapping[field] == value) return;
    setState(() {
      _mapping[field] = value;
      _preview = null;
      _previewRevision = null;
      _actions.clear();
      _targets.clear();
      _financialStateResolutions.clear();
      _info = 'Mapeamento alterado. Gere uma nova prévia antes de revisar ou confirmar.';
    });
  }

  List<Map<String, dynamic>> _reviewDecisions() =>
      (_preview!['rows'] as List).cast<Map>().map((x) {
        final n = x['rowNumber'] as int;
        final a = _actions[n] ?? 'Ignore';
        return <String, dynamic>{
          'rowNumber': n,
          'action': a,
          if (a == 'LinkExisting' && _targets[n] != null)
            'targetId': _targets[n],
          if (_financialStateResolutions[n] != null)
            'financialStateResolution': _financialStateResolutions[n],
        };
      }).toList();

  Future<void> _saveReview() => _run(() async {
    final batchId = _preview!['batchId'] as String;
    await _api.post(
      'api/migrations/${_sessionId!}/batches/$batchId/review',
      body: {
        'decisions': _reviewDecisions(),
        if (_preview!['fingerprint'] != null)
          'fingerprint': _preview!['fingerprint'],
      },
    );
    _info = 'Decisões salvas. Você pode sair e retomar esta revisão depois.';
  });

  Future<void> _confirm() => _run(() async {
    if (_previewRevision != null &&
        _previewRevision != _currentPreviewRevision()) {
      _preview = null;
      _previewRevision = null;
      throw StateError(
        'Os dados revisados mudaram. Gere uma nova prévia antes de confirmar.',
      );
    }
    final batchId = _preview!['batchId'] as String;
    await _api.post(
      'api/migrations/${_sessionId!}/batches/$batchId/confirm',
      body: {
        'decisions': _reviewDecisions(),
        if (_preview!['fingerprint'] != null)
          'fingerprint': _preview!['fingerprint'],
      },
    );
    _preview = null;
    _previewRevision = null;
    _file = null;
    await _load();
  });

  String? _currentPreviewRevision() {
    final file = _file;
    final sessionId = _sessionId;
    if (file == null || sessionId == null) return null;
    final rows = file.rows
        .map(
          (row) => buildMigrationRowPayload(
            row.rowNumber,
            migrationImportTemplates[_type].fields,
            _mapping,
            row.cells,
          ),
        )
        .toList();
    return migrationPreviewRevision(
      sessionId: sessionId,
      entityType: _type + 1,
      fileName: file.fileName,
      fields: migrationImportTemplates[_type].fields,
      mapping: _mapping,
      rows: rows,
    );
  }

  Future<void> _release() => _run(() async {
    await _api.post('api/migrations/${_sessionId!}/release', body: {});
    await _load();
  });

  Future<void> _discard(String batchId) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Descartar lote?'),
        content: const Text(
          'O lote será removido da preparação e não participará da liberação. '
          'Nenhum registro já existente será excluído.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Voltar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Descartar lote'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    await _run(() async {
      await _api.post(
        'api/migrations/${_sessionId!}/batches/$batchId/discard',
        body: {},
      );
      _info = 'Lote descartado. Ele não bloqueará a liberação operacional.';
      await _load();
    });
  }

  String _statusLabel(Object? status) => switch (status) {
    'Previewed' => 'Aguardando revisão',
    'Confirmed' => 'Confirmado',
    'Released' => 'Liberado',
    'Discarded' => 'Descartado',
    _ => status?.toString() ?? 'Desconhecido',
  };

  String _batchTotalsLabel(Map totals) {
    final reviewed = totals['reviewedCount'];
    return 'Totais finais · Sucesso ${totals['successCount']} · '
        'Ignorados ${totals['ignoredCount']} · Erros ${totals['errorCount']} · '
        'Revisados ${reviewed ?? 'indisponível'}';
  }

  @override
  Widget build(BuildContext context) {
    final s = _session;
    final sessionExpired =
        s != null &&
        s['status'] != 3 &&
        (DateTime.tryParse(s['expiresAt'].toString())
                ?.isBefore(DateTime.now()) ??
            false);
    final batches = (s?['batches'] as List? ?? const []).cast<Map>();
    return SingleChildScrollView(
      padding: const EdgeInsets.all(28),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1050),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Motor de migração',
                style: Theme.of(context).textTheme.headlineMedium,
              ),
              const Text(
                'Prepare alunos, planos, matrículas e situação financeira atual em arquivos separados. Os dados só entram em operação após liberação do Admin. Situações financeiras importadas não geram receita no Avelri.',
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.all(8),
                  child: Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              if (_info != null)
                Padding(
                  padding: const EdgeInsets.all(8),
                  child: Text(
                    _info!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.primary,
                    ),
                  ),
                ),
              if (_busy) const LinearProgressIndicator(),
              if (s == null) ...[
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Sessões salvas',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const Text(
                          'As sessões e seus lotes ficam salvos na academia e podem ser retomados após reiniciar o aplicativo.',
                        ),
                        if (_sessions == null && !_busy)
                          const Text(
                            'Não foi possível carregar as sessões salvas.',
                          ),
                        if (_sessions?.isEmpty == true)
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 12),
                            child: Text('Nenhuma sessão salva nesta academia.'),
                          ),
                        for (final saved in _sessions ?? const [])
                          ListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(saved['name'].toString()),
                            subtitle: Text(
                              '${saved['sourceNamespace']} · ${saved['batchCount']} lotes · ${_sessionStatusLabel(saved)}',
                            ),
                            trailing: TextButton(
                              onPressed: _busy
                                  ? null
                                  : () => _resumeSession(
                                      saved['sessionId'] as String,
                                    ),
                              child: Text(
                                saved['status'] == 3 ? 'Abrir' : 'Retomar',
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      children: [
                        TextField(
                          controller: _name,
                          decoration: const InputDecoration(
                            labelText: 'Nome da migração',
                          ),
                        ),
                        TextField(
                          controller: _source,
                          decoration: const InputDecoration(
                            labelText: 'Sistema de origem',
                          ),
                        ),
                        FilledButton(
                          onPressed: _busy ? null : _create,
                          child: const Text('Criar nova sessão'),
                        ),
                      ],
                    ),
                  ),
                ),
              ] else ...[
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${s['name'] as String} · ${s['sourceNamespace'] as String}',
                        ),
                        Align(
                          alignment: Alignment.centerRight,
                          child: TextButton.icon(
                            onPressed: _busy ? null : _backToSessions,
                            icon: const Icon(Icons.arrow_back),
                            label: const Text('Voltar às sessões'),
                          ),
                        ),
                        if (sessionExpired)
                          const Text(
                            'Esta sessão expirou. Você pode consultar os lotes, mas não continuar a preparação.',
                          ),
                        for (final b in batches)
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      'Lote ${b['entityType']} · ${b['rowCount']} registros · ${b['errorCount']} erros · ${_statusLabel(b['status'])}',
                                    ),
                                  ),
                                  if (b['status'] == 'Previewed')
                                    Wrap(
                                      spacing: 4,
                                      children: [
                                        TextButton.icon(
                                          onPressed: _busy || sessionExpired
                                              ? null
                                              : () => _resumeBatch(
                                                  b['batchId'] as String,
                                                ),
                                          icon: const Icon(
                                            Icons.rate_review_outlined,
                                          ),
                                          label: const Text('Retomar revisão'),
                                        ),
                                        TextButton.icon(
                                          onPressed: _busy || sessionExpired
                                              ? null
                                              : () => _discard(
                                                  b['batchId'] as String,
                                                ),
                                          icon: const Icon(
                                            Icons.delete_outline,
                                          ),
                                          label: const Text('Descartar'),
                                        ),
                                      ],
                                    ),
                                  if (b['status'] == 'Confirmed')
                                    TextButton.icon(
                                      onPressed: _busy || sessionExpired
                                          ? null
                                          : () => _resumeBatch(
                                              b['batchId'] as String,
                                            ),
                                      icon: const Icon(Icons.rule_outlined),
                                      label: Text(
                                        (s['releaseBlockers'] as List? ??
                                                    const [])
                                                .any(
                                                  (blocker) => blocker
                                                      .toString()
                                                      .contains(
                                                        b['batchId'].toString(),
                                                      ),
                                                )
                                            ? 'Resolver conflito'
                                            : 'Reabrir revisão',
                                      ),
                                    ),
                                ],
                              ),
                              if (b['status'] == 'Released' &&
                                  b['totals'] is Map)
                                Padding(
                                  padding: const EdgeInsets.only(bottom: 8),
                                  child: Text(
                                    _batchTotalsLabel(b['totals'] as Map),
                                  ),
                                ),
                            ],
                          ),
                        for (final e
                            in (s['releaseBlockers'] as List? ?? const []))
                          Text('• $e'),
                        if (s['status'] == 3)
                          const Text('Sessão liberada.')
                        else
                          FilledButton(
                            onPressed:
                                _busy ||
                                    sessionExpired ||
                                    s['canRelease'] != true
                                ? null
                                : _release,
                            child: const Text('Liberar operacionalmente'),
                          ),
                        if (s['status'] == 3)
                          for (final r
                              in (s['results'] as List? ?? const [])
                                  .cast<Map>())
                            Text(
                              '${r['entityType']} · linha ${r['rowNumber']}: ${r['status']} — ${r['message']}',
                            ),
                      ],
                    ),
                  ),
                ),
                if (s['status'] != 3 && !sessionExpired)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          DropdownButton<int>(
                            value: _type,
                            items: List.generate(
                              migrationImportTemplates.length,
                              (i) => DropdownMenuItem(
                                value: i,
                                child: Text(migrationImportTemplates[i].label),
                              ),
                            ),
                            onChanged: _busy
                                ? null
                                : (v) => setState(() {
                                    _type = v!;
                                    _file = null;
                                    _preview = null;
                                    _previewRevision = null;
                                    _mapping.clear();
                                    _actions.clear();
                                    _targets.clear();
                                    _financialStateResolutions.clear();
                                  }),
                          ),
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: [
                              OutlinedButton(
                                onPressed: _busy ? null : _pick,
                                child: Text(
                                  _file?.fileName ?? 'Selecionar CSV/XLSX',
                                ),
                              ),
                              TextButton.icon(
                                onPressed: _busy ? null : _downloadTemplate,
                                icon: const Icon(Icons.download_outlined),
                                label: const Text('Baixar modelo'),
                              ),
                            ],
                          ),
                          Text(
                            'Modelo CSV de ${migrationImportTemplates[_type].label.toLowerCase()} com exemplo fictício. Outros arquivos CSV/XLSX continuam aceitos por mapeamento.',
                          ),
                          if (_file != null) ...[
                            Text(
                              '${_file!.rows.length} linhas. Faça o mapeamento; IDs externos relacionam os arquivos.',
                            ),
                            for (final f
                                in migrationImportTemplates[_type].fields)
                              Row(
                                children: [
                                  SizedBox(
                                    width: 190,
                                    child: Text(_fieldLabel(f)),
                                  ),
                                  Expanded(
                                    child: DropdownButton<int?>(
                                      value: _mapping[f],
                                      isExpanded: true,
                                      items: [
                                        const DropdownMenuItem(
                                          value: null,
                                          child: Text('Não mapeado'),
                                        ),
                                        for (
                                          var i = 0;
                                          i < _file!.headers.length;
                                          i++
                                        )
                                          DropdownMenuItem(
                                            value: i,
                                            child: Text(_file!.headers[i]),
                                          ),
                                      ],
                                      onChanged: _busy
                                          ? null
                                          : (v) => _changeMapping(f, v),
                                    ),
                                  ),
                                ],
                              ),
                            if (_ambiguousFields.isNotEmpty)
                              Text(
                                'Alguns cabeçalhos correspondem a mais de um campo. Selecione manualmente: ${_ambiguousFields.map(_fieldLabel).join(', ')}.',
                              ),
                            if (_preview == null)
                              FilledButton(
                                onPressed: _busy ? null : _previewFile,
                                child: const Text('Gerar prévia'),
                              ),
                          ],
                        ],
                      ),
                    ),
                  ),
                if (_preview != null)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Prévia: ${(_preview!['rows'] as List).length} registros',
                          ),
                          for (final raw
                              in (_preview!['rows'] as List).cast<Map>())
                            Builder(
                              builder: (context) {
                                final r = Map<String, dynamic>.from(raw);
                                final n = r['rowNumber'] as int;
                                final cs =
                                    (r['candidates'] as List? ?? const [])
                                        .cast<Map>();
                                final targetCandidates = <String, Map>{
                                  for (final candidate in cs)
                                    if (candidate['targetId'] is String)
                                      candidate['targetId'] as String:
                                          candidate,
                                };
                                final selectedTarget = _targets[n];
                                final info = [
                                  ...(r['errors'] as List? ?? const []),
                                  ...cs.map(
                                    (c) => '${c['name']} ${c['description']}',
                                  ),
                                ].join(' · ');
                                final values = Map<String, dynamic>.from(
                                  r['values'] as Map,
                                );
                                final requiresFinancialResolution =
                                    r['requiresFinancialResolution'] == true;
                                return ExpansionTile(
                                  title: Text(
                                    'Linha $n${r['isValid'] == true ? ' · válida' : ' · erro'}${cs.isEmpty ? '' : ' · revisar possível duplicidade'}',
                                  ),
                                  subtitle: Text(info),
                                  children: [
                                    Padding(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 16,
                                        vertical: 8,
                                      ),
                                      child: Wrap(
                                        spacing: 16,
                                        runSpacing: 8,
                                        children: [
                                          for (final entry
                                              in values.entries.where(
                                                (entry) =>
                                                    entry.value != null &&
                                                    !const {
                                                      'studentIdentityContext',
                                                      'resolvedStudentKey',
                                                      'resolvedFinancialTargetKey',
                                                    }.contains(entry.key),
                                              ))
                                            Text(
                                              '${_fieldLabel(entry.key)}: ${entry.value}',
                                            ),
                                        ],
                                      ),
                                    ),
                                    DropdownButton<String>(
                                      value: _actions[n],
                                      items: [
                                        DropdownMenuItem(
                                          value: 'ImportNew',
                                          child: Text(
                                            _type == 3
                                                ? 'Aplicar situação atual'
                                                : 'Criar novo',
                                          ),
                                        ),
                                        if (!requiresFinancialResolution)
                                          const DropdownMenuItem(
                                            value: 'LinkExisting',
                                            child: Text('Vincular existente'),
                                          ),
                                        DropdownMenuItem(
                                          value: 'Ignore',
                                          child: const Text('Ignorar'),
                                        ),
                                      ],
                                      onChanged:
                                          r['isValid'] == true ||
                                              requiresFinancialResolution
                                          ? (v) =>
                                                setState(() => _actions[n] = v!)
                                          : null,
                                    ),
                                    if (requiresFinancialResolution)
                                      DropdownButton<String>(
                                        value: _financialStateResolutions[n],
                                        hint: const Text(
                                          'Resolver conflito financeiro',
                                        ),
                                        items: const [
                                          DropdownMenuItem(
                                            value: 'UseEnrollmentState',
                                            child: Text(
                                              'Manter situação da matrícula',
                                            ),
                                          ),
                                          DropdownMenuItem(
                                            value: 'UseFinancialFile',
                                            child: Text(
                                              'Usar situação deste arquivo',
                                            ),
                                          ),
                                        ],
                                        onChanged: _actions[n] == 'ImportNew'
                                            ? (value) => setState(
                                                () =>
                                                    _financialStateResolutions[n] =
                                                        value,
                                              )
                                            : null,
                                      ),
                                    if (_actions[n] == 'LinkExisting' &&
                                        (targetCandidates.isNotEmpty ||
                                            selectedTarget != null))
                                      DropdownButton<String>(
                                        hint: const Text('Selecione destino'),
                                        value: selectedTarget,
                                        isExpanded: true,
                                        items: [
                                          for (final c
                                              in targetCandidates.values)
                                            DropdownMenuItem(
                                              value: c['targetId'] as String,
                                              child: Text(
                                                '${c['name']} · ${c['description']}',
                                                maxLines: 1,
                                                overflow: TextOverflow.ellipsis,
                                              ),
                                            ),
                                          if (selectedTarget != null &&
                                              !targetCandidates.containsKey(
                                                selectedTarget,
                                              ))
                                            DropdownMenuItem(
                                              value: selectedTarget,
                                              child: Text(
                                                '${_selectedStudentLabels[selectedTarget] ?? 'Aluno selecionado'} · $selectedTarget',
                                                maxLines: 1,
                                                overflow: TextOverflow.ellipsis,
                                              ),
                                            ),
                                        ],
                                        onChanged: (v) =>
                                            setState(() => _targets[n] = v),
                                      ),
                                    if (_type == 0 &&
                                        _actions[n] == 'LinkExisting' &&
                                        r['isValid'] == true)
                                      TextButton.icon(
                                        onPressed: _busy
                                            ? null
                                            : () => _selectExistingStudent(
                                                n,
                                                values['name'] as String?,
                                              ),
                                        icon: const Icon(
                                          Icons.person_search_outlined,
                                        ),
                                        label: const Text(
                                          'Buscar aluno existente',
                                        ),
                                      ),
                                  ],
                                );
                              },
                            ),
                          Wrap(
                            spacing: 8,
                            children: [
                              OutlinedButton(
                                onPressed: _busy ? null : _saveReview,
                                child: const Text('Salvar decisões'),
                              ),
                              FilledButton(
                                onPressed: _busy ? null : _confirm,
                                child: const Text('Confirmar revisão do lote'),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _MigrationStudentPicker extends StatefulWidget {
  const _MigrationStudentPicker({
    required this.api,
    required this.initialSearch,
  });

  final ApiClient api;
  final String initialSearch;

  @override
  State<_MigrationStudentPicker> createState() =>
      _MigrationStudentPickerState();
}

class _MigrationStudentPickerState extends State<_MigrationStudentPicker> {
  late final TextEditingController _search;
  late String _submittedSearch;
  List<Map<String, dynamic>> _students = [];
  int _page = 1;
  int _totalPages = 0;
  bool _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _search = TextEditingController(text: widget.initialSearch);
    _submittedSearch = widget.initialSearch.trim();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _load();
    });
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _load({int page = 1, bool submitSearch = false}) async {
    if (_loading) return;
    final search = submitSearch ? _search.text.trim() : _submittedSearch;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final query = Uri(
        queryParameters: {
          if (search.isNotEmpty) 'search': search,
          // StudentArchiveFilter.All includes active, inactive and archived students.
          'archiveFilter': '3',
          'page': page.toString(),
          'pageSize': '20',
        },
      ).query;
      final response = await widget.api.get('api/students?$query') as Map;
      final rawItems = response['items'];
      if (rawItems is! List) {
        throw const FormatException(
          'A busca de alunos retornou dados inválidos.',
        );
      }
      final students = rawItems
          .map((item) => Map<String, dynamic>.from(item as Map))
          .toList();
      if (!mounted) return;
      setState(() {
        _students = students;
        _page = page;
        _totalPages = (response['totalPages'] as num).toInt();
        _submittedSearch = search;
      });
    } catch (_) {
      if (mounted) {
        setState(
          () => _error = 'Não foi possível buscar os alunos. Tente novamente.',
        );
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _studentDetails(Map<String, dynamic> student) {
    final details = <String>[
      if (student['email'] is String && (student['email'] as String).isNotEmpty)
        student['email'] as String,
      if (student['phone'] is String && (student['phone'] as String).isNotEmpty)
        student['phone'] as String,
      if (student['birthDate'] is String) 'Nascimento: ${student['birthDate']}',
      if (student['archivedAt'] != null)
        'Arquivado'
      else if (student['isActive'] == true)
        'Ativo'
      else
        'Inativo',
      student['userId'] == null ? 'Sem conta digital' : 'Com conta digital',
      'ID: ${student['id']}',
    ];
    return details.join(' · ');
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Selecionar aluno existente'),
    content: SizedBox(
      width: 620,
      height: 400,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Confira os dados e selecione explicitamente o aluno deste vínculo.',
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _search,
                  enabled: !_loading,
                  decoration: const InputDecoration(
                    labelText: 'Nome, e-mail ou telefone',
                  ),
                  onSubmitted: (_) => _load(submitSearch: true),
                ),
              ),
              const SizedBox(width: 8),
              TextButton(
                onPressed: _loading ? null : () => _load(submitSearch: true),
                child: const Text('Buscar'),
              ),
            ],
          ),
          if (_error != null) Text(_error!),
          if (_loading) const LinearProgressIndicator(),
          const SizedBox(height: 8),
          Expanded(
            child: _students.isEmpty && !_loading
                ? const Center(child: Text('Nenhum aluno encontrado.'))
                : ListView(
                    children: [
                      for (final student in _students)
                        ListTile(
                          title: Text(student['name'] as String),
                          subtitle: Text(_studentDetails(student)),
                          enabled: !_loading && _error == null,
                          onTap: () => Navigator.of(context).pop(student),
                        ),
                    ],
                  ),
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              TextButton(
                onPressed: _loading || _page <= 1
                    ? null
                    : () => _load(page: _page - 1),
                child: const Text('Anterior'),
              ),
              Text('Página $_page de ${_totalPages == 0 ? 1 : _totalPages}'),
              TextButton(
                onPressed: _loading || _page >= _totalPages
                    ? null
                    : () => _load(page: _page + 1),
                child: const Text('Próxima'),
              ),
            ],
          ),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.of(context).pop(),
        child: const Text('Cancelar'),
      ),
    ],
  );
}
