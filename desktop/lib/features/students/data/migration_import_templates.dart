class MigrationImportTemplate {
  const MigrationImportTemplate({
    required this.label,
    required this.fileName,
    required this.assetPath,
    required this.fields,
  });

  final String label;
  final String fileName;
  final String assetPath;
  final List<String> fields;
}

const migrationImportTemplates = <MigrationImportTemplate>[
  MigrationImportTemplate(
    label: 'Alunos',
    fileName: 'modelo-alunos.csv',
    assetPath: 'assets/import_templates/modelo-alunos.csv',
    fields: ['externalId', 'name', 'email', 'phone', 'birthDate'],
  ),
  MigrationImportTemplate(
    label: 'Planos',
    fileName: 'modelo-planos.csv',
    assetPath: 'assets/import_templates/modelo-planos.csv',
    fields: ['externalId', 'name', 'price', 'durationMonths', 'billingCycle'],
  ),
  MigrationImportTemplate(
    label: 'Matrículas e cobrança atual',
    fileName: 'modelo-matriculas.csv',
    assetPath: 'assets/import_templates/modelo-matriculas.csv',
    fields: [
      'externalId',
      'studentExternalId',
      'planExternalId',
      'name',
      'price',
      'durationMonths',
      'billingCycle',
      'startDate',
      'endDate',
      'chargeState',
      'chargeDueDate',
    ],
  ),
  MigrationImportTemplate(
    label: 'Situação financeira atual',
    fileName: 'modelo-situacao-financeira.csv',
    assetPath: 'assets/import_templates/modelo-situacao-financeira.csv',
    fields: [
      'externalId',
      'studentExternalId',
      'enrollmentExternalId',
      'chargeExternalId',
      'chargeState',
    ],
  ),
];
