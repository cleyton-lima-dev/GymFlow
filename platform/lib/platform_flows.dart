import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'platform_api.dart';
import 'platform_ui.dart';

class ProvisionDialog extends StatefulWidget {
  const ProvisionDialog({super.key, required this.api});
  final PlatformApi api;
  @override
  State<ProvisionDialog> createState() => _ProvisionDialogState();
}

class _ProvisionDialogState extends State<ProvisionDialog> {
  final scroll = ScrollController();
  final form = GlobalKey<FormState>();
  final fields = List.generate(4, (_) => TextEditingController());
  final requestKey = PlatformApi.requestKey();
  bool physical = false, busy = false, attempted = false;
  String? error;
  @override
  void dispose() {
    scroll.dispose();
    for (final field in fields) {
      field.dispose();
    }
    super.dispose();
  }

  Future<void> save() async {
    if (!form.currentState!.validate() || busy) return;
    setState(() {
      busy = true;
      error = null;
      attempted = true;
    });
    try {
      final result = await widget.api.request(
        'POST',
        '/api/platform/gym-onboardings',
        requestKey: requestKey,
        body: {
          'administrativeName': fields[0].text.trim(),
          'timeZoneId': fields[1].text.trim(),
          'adminName': fields[2].text.trim(),
          'adminEmail': fields[3].text.trim(),
          'physicalAccessContracted': physical,
        },
      );
      if (mounted) Navigator.pop(context, result);
    } on PlatformFailure catch (e) {
      if (mounted) setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Widget field(int i) => TextFormField(
    controller: fields[i],
    keyboardType: i == 3 ? TextInputType.emailAddress : TextInputType.text,
    textInputAction: TextInputAction.next,
    enabled: !busy && !attempted,
    maxLength: i == 1
        ? 100
        : i == 3
        ? 200
        : 150,
    decoration: InputDecoration(
      counterText: '',
      labelText: [
        'Nome fantasia',
        'Fuso horário IANA',
        'Nome do primeiro Admin',
        'E-mail do primeiro Admin',
      ][i],
      hintText: i == 1 ? 'America/Sao_Paulo' : null,
    ),
    validator: (value) {
      final missing = requiredField(value);
      if (missing != null) return missing;
      if (i == 1 &&
          !RegExp(r'^(UTC|[A-Za-z0-9._+\-]+(?:/[A-Za-z0-9._+\-]+)+)$')
              .hasMatch(value!.trim())) {
        return 'Informe um fuso IANA válido.';
      }
      if (i == 3 &&
          !RegExp(r'^[^\s@]+@[^\s@]+\.[^\s@]+$').hasMatch(value!.trim())) {
        return 'Informe um e-mail válido.';
      }
      return null;
    },
  );

  @override
  Widget build(BuildContext context) => AlertDialog(
    insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
    title: const Text('Cadastrar academia'),
    content: SizedBox(
      width: 640,
      child: DialogScrollArea(
        controller: scroll,
        child: Form(
          key: form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'O primeiro Admin definirá sua própria senha. Nenhum módulo ou equipamento será ativado automaticamente.',
              ),
              const SizedBox(height: 20),
              for (var group = 0; group < 2; group++) ...[
                Text(
                  group == 0
                      ? '1. Dados da academia'
                      : '2. Primeiro administrador',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 16),
                if (MediaQuery.sizeOf(context).width >= 740)
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(child: field(group * 2)),
                      const SizedBox(width: 16),
                      Expanded(child: field(group * 2 + 1)),
                    ],
                  )
                else
                  Column(
                    children: [
                      field(group * 2),
                      const SizedBox(height: 16),
                      field(group * 2 + 1),
                    ],
                  ),
                const SizedBox(height: 20),
              ],
              Text(
                '3. Controle de acesso',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                value: physical,
                onChanged: busy || attempted
                    ? null
                    : (value) => setState(() => physical = value ?? false),
                title: const Text('Contrato inclui controle físico'),
                subtitle: const Text(
                  'Exige pareamento e homologação do hardware contratado.',
                ),
              ),
              if (error != null) errorText(error!),
              if (attempted && !busy)
                const Text(
                  'Ao repetir, serão enviados os mesmos dados. Para corrigir campos, feche e confira primeiro a listagem.',
                ),
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: busy ? null : () => Navigator.pop(context),
        child: const Text('Fechar'),
      ),
      FilledButton(
        onPressed: busy ? null : save,
        child: Text(
          busy
              ? 'Provisionando…'
              : attempted
              ? 'Repetir solicitação'
              : 'Provisionar',
        ),
      ),
    ],
  );
}

Future<void> showInvitation(
  BuildContext context,
  Map<String, dynamic> result,
) async {
  final token = result['token'] as String?;
  final link = token == null
      ? null
      : Uri.base.replace(query: '', fragment: 'activate=$token').toString();
  await showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (context) => AlertDialog(
      title: Text(
        token == null
            ? 'Academia já provisionada'
            : 'Convite do primeiro Admin',
      ),
      content: SizedBox(
        width: 560,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                token == null
                    ? 'A solicitação já foi concluída. Consulte a academia para reemitir o convite, se necessário.'
                    : 'Válido por 24 horas e uma única utilização. Compartilhe por um canal seguro. Após fechar, este link não poderá ser recuperado.',
              ),
              if (link != null) ...[
                const SizedBox(height: 16),
                SelectableText(link),
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: () async {
                    await Clipboard.setData(ClipboardData(text: link));
                  },
                  icon: const Icon(Icons.copy),
                  label: const Text('Copiar convite'),
                ),
              ],
            ],
          ),
        ),
      ),
      actions: [
        FilledButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Concluir'),
        ),
      ],
    ),
  );
}

class AcademyDialog extends StatefulWidget {
  const AcademyDialog({super.key, required this.api, required this.gymId});
  final PlatformApi api;
  final String gymId;
  @override
  State<AcademyDialog> createState() => _AcademyDialogState();
}

class _AcademyDialogState extends State<AcademyDialog> {
  final scroll = ScrollController();
  Map<String, dynamic>? data;
  String? error;
  bool busy = false;
  final evidence = TextEditingController();
  @override
  void initState() {
    super.initState();
    load();
  }

  @override
  void dispose() {
    scroll.dispose();
    evidence.dispose();
    super.dispose();
  }

  String get path => '/api/platform/gym-onboardings/${widget.gymId}';
  Future<void> load() async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final result = await widget.api.request('GET', path);
      if (mounted) setState(() => data = result);
    } on PlatformFailure catch (e) {
      if (mounted) setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> step(String step) async {
    if (busy) return;
    if (step == 'physical' && evidence.text.trim().isEmpty) {
      setState(
        () => error = 'Informe a referência da evidência do teste físico.',
      );
      return;
    }
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.api.request(
        'PATCH',
        '$path/steps',
        body: {
          'version': (data!['academy'] as Map)['version'],
          'step': step,
          'evidence': step == 'physical' ? evidence.text.trim() : null,
        },
      );
      await load();
    } on PlatformFailure catch (e) {
      if (mounted) setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> reissue() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Substituir convite?'),
        content: const Text(
          'O convite anterior será invalidado. O novo convite terá validade de 24 horas.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Reemitir'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final result = await widget.api.request(
        'POST',
        '$path/admin-invitation',
        body: {'version': (data!['academy'] as Map)['version']},
      );
      if (!mounted) return;
      await showInvitation(context, result);
      if (mounted) await load();
    } on PlatformFailure catch (e) {
      if (mounted) setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final a = data?['academy'] as Map<String, dynamic>?;
    final completed = a?['completedAt'] != null;
    final canComplete =
        a?['adminActivatedAt'] != null &&
        a?['configurationConfirmed'] == true &&
        (a?['physicalAccessContracted'] != true ||
            a?['physicalValidationEvidence'] != null);
    final audit = List<Map<String, dynamic>>.from(data?['audit'] as List? ?? [])
      ..sort(
        (x, y) =>
            DateTime.parse(x['occurredAt'] as String)
                .compareTo(DateTime.parse(y['occurredAt'] as String)),
      );
    return AlertDialog(
      insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      title: Text(a?['administrativeName'] as String? ?? 'Implantação'),
      content: SizedBox(
        width: 700,
        child: DialogScrollArea(
          controller: scroll,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (busy) const LinearProgressIndicator(),
              if (error != null) errorText(error!),
              if (a != null) ...[
                StatusBadge(a['status'] as String?),
                const SizedBox(height: 16),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: platformBackground,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Primeiro Admin',
                        style: Theme.of(context).textTheme.labelMedium,
                      ),
                      const SizedBox(height: 4),
                      Text(a['adminName'] as String),
                      Text(
                        a['adminEmail'] as String,
                        style: const TextStyle(color: Color(0xFF68728C)),
                      ),
                      const SizedBox(height: 12),
                      Text('Fuso horário: ${a['timeZoneId']}'),
                    ],
                  ),
                ),
                const Divider(height: 32),
                OnboardingStep(
                  number: 1,
                  title: 'Ativação do primeiro Admin',
                  done: a['adminActivatedAt'] != null,
                  description: a['adminActivatedAt'] != null
                      ? 'Credencial configurada pelo Admin'
                      : 'Aguardando definição da senha',
                ),
                if (a['adminActivatedAt'] == null)
                  OutlinedButton(
                    onPressed: busy ? null : reissue,
                    child: const Text('Reemitir convite'),
                  ),
                const SizedBox(height: 12),
                OnboardingStep(
                  number: 2,
                  title: 'Dados iniciais',
                  done: a['configurationConfirmed'] == true,
                  description: a['configurationConfirmed'] == true
                      ? 'Nome fantasia e fuso horário confirmados.'
                      : 'Confira nome fantasia e fuso horário com a academia.',
                ),
                if (!completed && a['configurationConfirmed'] != true)
                  OutlinedButton(
                    onPressed: busy ? null : () => step('configuration'),
                    child: const Text('Confirmar dados iniciais'),
                  ),
                if (a['physicalAccessContracted'] == true) ...[
                  const Divider(height: 32),
                  OnboardingStep(
                    number: 3,
                    title: 'Homologação física',
                    done: a['physicalValidationEvidence'] != null,
                    description:
                        'Etapa exigida pelo contrato de controle físico.',
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Pareamento e agente online não comprovam passagem física. Registre somente testes reais do hardware contratado e homologado.',
                  ),
                  if (a['physicalValidationEvidence'] != null)
                    Text('Evidência: ${a['physicalValidationEvidence']}'),
                  if (!completed) ...[
                    const SizedBox(height: 16),
                    TextField(
                      controller: evidence,
                      enabled: !busy,
                      maxLength: 1000,
                      maxLines: 3,
                      decoration: const InputDecoration(
                        labelText: 'Referência da evidência',
                        hintText: 'Equipamento, data e referência do registro de passagem confirmada. Não inclua credenciais.',
                      ),
                    ),
                    OutlinedButton(
                      onPressed: busy ? null : () => step('physical'),
                      child: const Text('Registrar homologação física'),
                    ),
                  ],
                ],
                const Divider(height: 32),
                OnboardingStep(
                  number: a['physicalAccessContracted'] == true ? 4 : 3,
                  title: 'Conclusão da implantação',
                  done: completed,
                  description: completed
                      ? 'Todas as etapas foram confirmadas.'
                      : canComplete
                      ? 'Etapas obrigatórias confirmadas. A implantação pode ser concluída.'
                      : 'Conclua as etapas obrigatórias acima para liberar esta ação.',
                ),
                if (!completed)
                  FilledButton.icon(
                    onPressed: busy || !canComplete
                        ? null
                        : () => step('complete'),
                    icon: const Icon(Icons.check),
                    label: const Text('Concluir implantação'),
                  ),
                const SizedBox(height: 24),
                Text(
                  'Histórico',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 12),
                if (audit.isEmpty)
                  const Text(
                    'Nenhum evento registrado.',
                    style: TextStyle(color: Color(0xFF68728C)),
                  ),
                for (final event in audit)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Padding(
                          padding: EdgeInsets.only(top: 3),
                          child: Icon(
                            Icons.history_rounded,
                            size: 18,
                            color: avelriPurple,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                auditLabel(event['action'] as String),
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                displayDate(event['occurredAt'] as String),
                                style: const TextStyle(
                                  color: Color(0xFF68728C),
                                  fontSize: 12,
                                ),
                              ),
                              Text(
                                event['actorType'] == 'admin'
                                    ? 'Admin da academia'
                                    : 'Operador da plataforma',
                                style: const TextStyle(
                                  color: Color(0xFF68728C),
                                  fontSize: 12,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: busy ? null : load,
          child: const Text('Atualizar'),
        ),
        TextButton(
          onPressed: busy ? null : () => Navigator.pop(context),
          child: const Text('Fechar'),
        ),
      ],
    );
  }
}

String auditLabel(String action) => switch (action) {
  'provisioned' => 'Academia provisionada',
  'admin-activated' => 'Admin ativado',
  'invitation-reissued' => 'Convite substituído',
  'step-configuration' => 'Dados iniciais confirmados',
  'step-physical' => 'Homologação física registrada pelo operador',
  'step-complete' => 'Implantação concluída',
  _ => 'Etapa atualizada',
};

class ActivationPage extends StatefulWidget {
  const ActivationPage({super.key, required this.api, required this.token});
  final PlatformApi api;
  final String token;
  @override
  State<ActivationPage> createState() => _ActivationPageState();
}

class _ActivationPageState extends State<ActivationPage> {
  final form = GlobalKey<FormState>();
  final password = TextEditingController(),
      confirmation = TextEditingController();
  bool busy = false, done = false;
  String? error;
  @override
  void dispose() {
    password.dispose();
    confirmation.dispose();
    super.dispose();
  }

  Future<void> submitActivation() async {
    if (!form.currentState!.validate() || busy) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.api.request(
        'POST',
        '/api/onboarding/admin-activation',
        body: {'token': widget.token, 'password': password.text},
      );
      password.clear();
      confirmation.clear();
      if (mounted) setState(() => done = true);
    } on PlatformFailure catch (e) {
      if (mounted) setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Avelri • Ativação do Admin')),
    body: SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 440),
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: done
                  ? const Text(
                      'Senha configurada. Abra o Avelri Gestão e entre com seu e-mail e sua nova senha.',
                    )
                  : Form(
                      key: form,
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(
                            'Defina sua senha',
                            style: Theme.of(context).textTheme.headlineSmall,
                          ),
                          const SizedBox(height: 16),
                          const Text(
                            'Este convite é pessoal e só pode ser utilizado uma vez.',
                          ),
                          const SizedBox(height: 24),
                          TextFormField(
                            controller: password,
                            enabled: !busy,
                            obscureText: true,
                            enableSuggestions: false,
                            maxLength: 128,
                            decoration: const InputDecoration(
                              labelText: 'Nova senha',
                            ),
                            validator: (value) =>
                                value == null ||
                                    value.length < 8 ||
                                    value.length > 128
                                ? 'Use de 8 a 128 caracteres.'
                                : null,
                          ),
                          const SizedBox(height: 16),
                          TextFormField(
                            controller: confirmation,
                            enabled: !busy,
                            obscureText: true,
                            enableSuggestions: false,
                            decoration: const InputDecoration(
                              labelText: 'Confirme a senha',
                            ),
                            validator: (value) => value != password.text
                                ? 'As senhas devem ser iguais.'
                                : null,
                          ),
                          if (error != null) errorText(error!),
                          const SizedBox(height: 24),
                          FilledButton(
                            onPressed: busy ? null : submitActivation,
                            child: Text(
                              busy ? 'Ativando…' : 'Definir senha e ativar',
                            ),
                          ),
                        ],
                      ),
                    ),
            ),
          ),
        ),
      ),
    ),
  );
}
