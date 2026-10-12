import 'package:avelri_gestao/features/reports/data/reports_service.dart';
import 'package:intl/intl.dart';

// Display-only projection. Original columns/rows remain intact for the CSV API.
List<int> visibleReportColumns(ReportResult data) {
  int priority(String column) {
    if (column == 'Aluno') return 0;
    if (column == 'Cadastro' || column == 'Situação' || column == 'Decisão') {
      return 1;
    }
    if (column.startsWith('Plano')) return 2;
    if (column.startsWith('Matrícula vigente')) return 3;
    if (column.contains(r'(R$)') || column == 'Valor') return 4;
    if (column.contains('Data') ||
        column.contains('Vencimento') ||
        column.contains('Início') ||
        column.startsWith('Pagamento local')) {
      return 5;
    }
    return 6;
  }

  final indices = List.generate(data.columns.length, (i) => i).where((i) {
    final label = data.columns[i].trim().toLowerCase();
    return !label.startsWith('id ') &&
        label != 'id' &&
        !label.contains('guid') &&
        label != 'requestid';
  }).toList();
  indices.sort((a, b) {
    final order = priority(data.columns[a])
        .compareTo(priority(data.columns[b]));
    return order == 0 ? a.compareTo(b) : order;
  });
  return indices;
}

String reportColumnLabel(String column) {
  if (column.startsWith('Pagamento local')) return 'Pagamento';
  if (column.startsWith('Data local')) return 'Data e hora';
  return {
        'Cadastro': 'Situação cadastral',
        'Matrícula vigente': 'Matrícula vigente hoje',
        'Último contrato': 'Situação do último contrato',
        'Vencimento do último contrato': 'Último vencimento',
        'Passagem confirmada registrada': 'Confirmação de passagem',
        'Presença registrada por passagem': 'Confirmação de passagem',
        r'Líquido não cancelado (R$)': r'Total líquido (R$)',
        r'Recebido das cobranças selecionadas (R$)':
            r'Recebido das cobranças (R$)',
      }[column] ??
      column;
}

String reportCellLabel(String column, String value) {
  if (column == 'Origem') {
    return {
          'Online': 'Online',
          'OfflineCache': 'Offline com permissão salva',
          'OfflineFailClosed': 'Offline sem permissão',
          'Acesso físico': 'Confirmação de passagem',
        }[value] ??
        value;
  }
  if (column == 'Motivo') {
    return {
          'Eligible': 'Acesso permitido',
          'ManualOverride': 'Liberação manual',
          'CredentialNotFound': 'Credencial não encontrada',
          'CredentialInactive': 'Credencial inativa',
          'StudentInactive': 'Aluno inativo',
          'StudentArchived': 'Aluno arquivado',
          'NoValidEnrollment': 'Sem matrícula vigente',
          'FinancialRestriction': 'Restrição financeira',
          'ManualBlock': 'Bloqueio manual',
          'PlanRestriction': 'Restrição do plano',
          'OfflineNoCachedPermission': 'Sem permissão salva para uso offline',
        }[value] ??
        value;
  }
  return value == 'Sim (registro da API)' ? 'Sim' : value;
}

String reportExplanation(String kind, String view) => switch (kind) {
  'students' => 'Matrícula vigente hoje e situação do último contrato são informações diferentes: um contrato futuro pode coexistir com uma matrícula vigente. Matrícula vigente não significa liberação da catraca.',
  'financial' when view == 'payments' || view == 'revenue' => 'Valores efetivamente recebidos, considerando a data do pagamento e os descontos registrados.',
  'financial' => 'Cobranças com vencimento no período. O total líquido exclui canceladas. O recebido corresponde a essas cobranças, mesmo quando pago em outra data.',
  'enrollments' => 'Contratos que vencem no período. A situação considera a data atual da academia; a vigência termina na data de vencimento.',
  'attendance' => 'Presenças registradas. Registro manual não comprova passagem pela catraca; acesso autorizado, sozinho, não conta como presença.',
  _ => 'Tentativas de acesso e suas decisões. A confirmação de passagem é exibida apenas quando registrada; não comprova homologação da catraca.',
};

String reportPeriodLabel(ReportResult data) {
  String date(String value) =>
      DateFormat('dd/MM/yyyy').format(DateTime.parse(value));
  return '${data.fromDate == null ? 'Situação atual' : '${date(data.fromDate!)} a ${date(data.toDate!)}'} • Horário da academia (${data.timeZone})';
}
