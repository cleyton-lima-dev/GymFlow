class ColumnHeaderSuggestion {
  const ColumnHeaderSuggestion({this.index, this.isAmbiguous = false});

  final int? index;
  final bool isAmbiguous;
}

class ColumnHeaderMapper {
  const ColumnHeaderMapper._();

  static const _diacritics = <String, String>{
    'á': 'a',
    'à': 'a',
    'ã': 'a',
    'â': 'a',
    'ä': 'a',
    'é': 'e',
    'è': 'e',
    'ê': 'e',
    'ë': 'e',
    'í': 'i',
    'ì': 'i',
    'î': 'i',
    'ï': 'i',
    'ó': 'o',
    'ò': 'o',
    'õ': 'o',
    'ô': 'o',
    'ö': 'o',
    'ú': 'u',
    'ù': 'u',
    'û': 'u',
    'ü': 'u',
    'ç': 'c',
  };

  static const aliases = <String, List<String>>{
    'externalId': [
      'external id',
      'external identifier',
      'id externo',
      'identificador externo',
      'codigo externo',
      'código externo',
      'code',
    ],
    'name': [
      'name',
      'nome',
      'nome completo',
      'full name',
      'aluno',
      'cliente',
      'plano',
      'nome do plano',
    ],
    'email': ['email', 'e-mail', 'correio eletronico', 'correio eletrônico'],
    'phone': [
      'phone',
      'telephone',
      'telefone',
      'telefones',
      'celular',
      'mobile',
    ],
    'birthDate': [
      'birth date',
      'date of birth',
      'data nascimento',
      'data de nascimento',
      'nascimento',
      'aniversario',
      'aniversário',
    ],
    'price': ['price', 'amount', 'preco', 'preço', 'valor'],
    'durationMonths': [
      'duration months',
      'duration',
      'months',
      'duracao',
      'duração',
      'duracao em meses',
      'duração em meses',
      'meses',
    ],
    'billingCycle': [
      'billing cycle',
      'billing period',
      'cycle',
      'ciclo de cobranca',
      'ciclo de cobrança',
      'periodicidade',
    ],
    'studentExternalId': [
      'student external id',
      'student id',
      'id externo aluno',
      'id do aluno',
      'codigo aluno',
      'código do aluno',
    ],
    'planExternalId': [
      'plan external id',
      'plan id',
      'id externo plano',
      'id do plano',
      'codigo plano',
      'código do plano',
    ],
    'enrollmentExternalId': [
      'enrollment external id',
      'enrollment id',
      'id externo matrícula',
      'id externo da matrícula',
      'id da matrícula',
      'codigo matrícula',
      'código da matrícula',
    ],
    'chargeExternalId': [
      'charge external id',
      'charge id',
      'id externo cobrança',
      'id externo da cobrança',
      'id da cobrança',
      'codigo cobrança',
      'código da cobrança',
    ],
    'startDate': [
      'start date',
      'enrollment start',
      'data inicio',
      'data de inicio',
      'data de início',
      'inicio',
      'início',
    ],
    'endDate': ['end date', 'enrollment end', 'data fim', 'data final', 'fim'],
    'chargeState': [
      'charge state',
      'charge status',
      'payment status',
      'situacao cobranca',
      'situação cobrança',
      'status da cobranca',
      'status da cobrança',
      'pagamento',
    ],
    'chargeDueDate': [
      'charge due date',
      'due date',
      'data vencimento',
      'vencimento',
      'vencimento cobranca',
      'vencimento cobrança',
    ],
  };

  static ColumnHeaderSuggestion suggest(List<String> headers, String field) {
    final fieldAliases = <String>{
      field,
      ...?aliases[field],
    }.map(normalize).where((value) => value.isNotEmpty).toSet();
    final matches = <int>[];
    for (var index = 0; index < headers.length; index++) {
      if (fieldAliases.contains(normalize(headers[index]))) matches.add(index);
    }
    if (matches.length == 1) {
      return ColumnHeaderSuggestion(index: matches.single);
    }
    return ColumnHeaderSuggestion(isAmbiguous: matches.length > 1);
  }

  static String normalize(String value) => value
      .trim()
      .toLowerCase()
      .replaceAllMapped(
        RegExp('[${_diacritics.keys.join()}]'),
        (match) => _diacritics[match[0]]!,
      )
      .replaceAll(RegExp('[^a-z0-9]'), '');
}
