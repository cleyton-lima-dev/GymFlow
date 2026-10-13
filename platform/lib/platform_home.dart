import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'platform_api.dart';
import 'platform_flows.dart';
import 'platform_ui.dart';

class PlatformHome extends StatefulWidget {
  const PlatformHome({super.key, required this.api});
  final PlatformApi api;
  @override
  State<PlatformHome> createState() => _PlatformHomeState();
}

class _PlatformHomeState extends State<PlatformHome> {
  final form = GlobalKey<FormState>();
  final email = TextEditingController(),
      password = TextEditingController(),
      code = TextEditingController(),
      search = TextEditingController();
  String? operatorName, error;
  List<Map<String, dynamic>>? academies;
  bool busy = false;
  int section = 0, page = 1, loadVersion = 0;
  String status = 'all';
  static const labels = ['Visão geral', 'Academias', 'Implantações'];
  static const icons = [
    Icons.grid_view_rounded,
    Icons.storefront_outlined,
    Icons.fact_check_outlined,
  ];
  @override
  void initState() {
    super.initState();
    widget.api.onSessionExpired = () {
      if (mounted) logout('Sessão encerrada. Entre novamente com MFA.');
    };
  }

  @override
  void dispose() {
    widget.api.onSessionExpired = null;
    for (final c in [email, password, code, search]) {
      c.dispose();
    }
    super.dispose();
  }

  void logout([String? message]) {
    loadVersion++;
    widget.api.token = null;
    setState(() {
      operatorName = null;
      academies = null;
      error = message;
      busy = false;
      section = 0;
      page = 1;
      status = 'all';
      search.clear();
      password.clear();
      code.clear();
    });
  }

  Future<void> login() async {
    if (!form.currentState!.validate() || busy) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final result = await widget.api.request(
        'POST',
        '/api/platform/auth/login',
        body: {
          'email': email.text.trim(),
          'password': password.text,
          'code': code.text.trim(),
        },
      );
      widget.api.token = result['token'] as String;
      password.clear();
      code.clear();
      if (!mounted) return;
      setState(() => operatorName = result['name'] as String);
      await load();
    } on PlatformFailure catch (e) {
      if (mounted) setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> load() async {
    final version = ++loadVersion;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final items = <Map<String, dynamic>>[];
      int current = 1, total = 0;
      do {
        final result = await widget.api.request(
          'GET',
          '/api/platform/gym-onboardings?page=$current&pageSize=100',
        );
        if (!mounted || version != loadVersion || widget.api.token == null) {
          return;
        }
        final batch = (result['items'] as List).cast<Map<String, dynamic>>();
        total = (result['total'] as num).toInt();
        items.addAll(batch);
        if (batch.isEmpty && items.length < total) {
          throw const PlatformFailure(
            'A listagem mudou. Atualize para consultar os indicadores.',
          );
        }
        current++;
      } while (items.length < total);
      if (items.length != total ||
          items.map((a) => a['gymId']).toSet().length != total) {
        throw const PlatformFailure(
          'A listagem mudou. Atualize para consultar os indicadores.',
        );
      }
      if (mounted && version == loadVersion) {
        setState(() {
          academies = items;
          page = 1;
        });
      }
    } on PlatformFailure catch (e) {
      if (mounted && version == loadVersion) setState(() => error = e.message);
    } finally {
      if (mounted && version == loadVersion) setState(() => busy = false);
    }
  }

  Future<void> provision() async {
    final result = await showDialog<Map<String, dynamic>>(
      context: context,
      barrierDismissible: false,
      builder: (_) => ProvisionDialog(api: widget.api),
    );
    if (!mounted || result == null) return;
    await showInvitation(context, result);
    if (mounted && operatorName != null) await load();
  }

  Future<void> details(Map<String, dynamic> a) async {
    await showDialog<void>(
      context: context,
      builder: (_) =>
          AcademyDialog(api: widget.api, gymId: a['gymId'] as String),
    );
    if (mounted && operatorName != null) await load();
  }

  void navigate(int value) => setState(() {
    section = value;
    page = 1;
  });
  @override
  Widget build(BuildContext context) {
    if (operatorName == null) return Scaffold(body: loginView());
    return LayoutBuilder(
      builder: (context, c) {
        final wide = c.maxWidth >= 1000;
        return Scaffold(
          drawer: wide
              ? null
              : Drawer(
                  backgroundColor: avelriNavy,
                  child: navigation(close: true),
                ),
          body: SafeArea(
            child: Row(
              children: [
                if (wide)
                  SizedBox(
                    width: 232,
                    child: Material(color: avelriNavy, child: navigation()),
                  ),
                Expanded(
                  child: Column(
                    children: [
                      Container(
                        padding: EdgeInsets.symmetric(
                          horizontal: wide ? 28 : 12,
                          vertical: 12,
                        ),
                        decoration: const BoxDecoration(
                          color: Colors.white,
                          border: Border(
                            bottom: BorderSide(color: Color(0xFFE5E7EF)),
                          ),
                        ),
                        child: Row(
                          children: [
                            if (!wide)
                              Builder(
                                builder: (context) => IconButton(
                                  tooltip: 'Abrir menu',
                                  icon: const Icon(Icons.menu),
                                  onPressed: () =>
                                      Scaffold.of(context).openDrawer(),
                                ),
                              ),
                            Expanded(
                              child: Text(
                                labels[section],
                                style: Theme.of(context).textTheme.titleLarge,
                              ),
                            ),
                            if (c.maxWidth >= 650) ...[
                              const Icon(
                                Icons.verified_user_outlined,
                                size: 18,
                                color: avelriPurple,
                              ),
                              const SizedBox(width: 8),
                              Flexible(
                                child: Text(
                                  operatorName!,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                              const SizedBox(width: 12),
                            ],
                            IconButton(
                              tooltip: 'Sair',
                              onPressed: logout,
                              icon: const Icon(Icons.logout_rounded),
                            ),
                          ],
                        ),
                      ),
                      if (busy) const LinearProgressIndicator(minHeight: 2),
                      Expanded(
                        child: Scrollbar(
                          child: SingleChildScrollView(
                            padding: EdgeInsets.all(c.maxWidth < 600 ? 16 : 28),
                            child: Align(
                              alignment: Alignment.topLeft,
                              child: ConstrainedBox(
                                constraints: const BoxConstraints(
                                  maxWidth: 1500,
                                ),
                                child: content(),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget navigation({bool close = false}) => SafeArea(
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Padding(
          padding: EdgeInsets.fromLTRB(24, 28, 24, 32),
          child: PlatformBrand(light: true),
        ),
        const Padding(
          padding: EdgeInsets.symmetric(horizontal: 24, vertical: 8),
          child: Text(
            'ADMINISTRAÇÃO',
            style: TextStyle(
              color: Color(0xFF8791AD),
              fontSize: 10,
              letterSpacing: 1.5,
            ),
          ),
        ),
        for (var i = 0; i < labels.length; i++)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
            child: ListTile(
              key: ValueKey('nav-$i'),
              selected: section == i,
              selectedTileColor: avelriPurple,
              selectedColor: Colors.white,
              iconColor: const Color(0xFFB6BDD3),
              textColor: const Color(0xFFB6BDD3),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
              ),
              leading: Icon(icons[i], size: 21),
              title: Text(labels[i], style: const TextStyle(fontSize: 14)),
              onTap: () {
                navigate(i);
                if (close) Navigator.pop(context);
              },
            ),
          ),
        const Spacer(),
        const Padding(
          padding: EdgeInsets.all(24),
          child: Row(
            children: [
              Icon(Icons.lock_outline, color: Color(0xFF8791AD), size: 16),
              SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Acesso exclusivo do operador',
                  style: TextStyle(color: Color(0xFF8791AD), fontSize: 12),
                ),
              ),
            ],
          ),
        ),
      ],
    ),
  );
  Widget loginView() => LayoutBuilder(
    builder: (context, c) {
      final wide = c.maxWidth >= 1000;
      final loginForm = Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (!wide) ...[
                  const PlatformBrand(),
                  const SizedBox(height: 32),
                ],
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Form(
                      key: form,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          const Align(
                            alignment: Alignment.centerLeft,
                            child: CircleAvatar(
                              backgroundColor: Color(0xFFF0EBFC),
                              foregroundColor: avelriPurple,
                              child: Icon(Icons.admin_panel_settings_outlined),
                            ),
                          ),
                          const SizedBox(height: 20),
                          Text(
                            'Bem-vindo ao Platform',
                            style: Theme.of(context).textTheme.headlineSmall,
                          ),
                          const SizedBox(height: 8),
                          const Text(
                            'Entre para acompanhar academias e implantações.',
                            style: TextStyle(color: Color(0xFF68728C)),
                          ),
                          const SizedBox(height: 24),
                          TextFormField(
                            controller: email,
                            enabled: !busy,
                            decoration: const InputDecoration(
                              labelText: 'E-mail',
                              prefixIcon: Icon(Icons.mail_outline),
                            ),
                            keyboardType: TextInputType.emailAddress,
                            validator: requiredField,
                            textInputAction: TextInputAction.next,
                          ),
                          const SizedBox(height: 16),
                          TextFormField(
                            controller: password,
                            enabled: !busy,
                            obscureText: true,
                            enableSuggestions: false,
                            autocorrect: false,
                            decoration: const InputDecoration(
                              labelText: 'Senha',
                              prefixIcon: Icon(Icons.lock_outline),
                            ),
                            validator: requiredField,
                            textInputAction: TextInputAction.next,
                          ),
                          const SizedBox(height: 16),
                          TextFormField(
                            controller: code,
                            enabled: !busy,
                            keyboardType: TextInputType.number,
                            inputFormatters: [
                              FilteringTextInputFormatter.digitsOnly,
                              LengthLimitingTextInputFormatter(6),
                            ],
                            decoration: const InputDecoration(
                              labelText: 'Código do autenticador',
                              prefixIcon: Icon(Icons.shield_outlined),
                              helperText: 'Digite os 6 dígitos do seu aplicativo autenticador.',
                              helperMaxLines: 2,
                            ),
                            validator: (v) =>
                                RegExp(r'^\d{6}$').hasMatch(v ?? '')
                                ? null
                                : 'Informe os 6 dígitos.',
                            onFieldSubmitted: (_) => login(),
                          ),
                          if (error != null) errorText(error!),
                          const SizedBox(height: 24),
                          FilledButton(
                            onPressed: busy ? null : login,
                            child: Text(busy ? 'Entrando…' : 'Entrar com MFA'),
                          ),
                          const SizedBox(height: 16),
                          const Text(
                            'Acesso restrito a operadores autorizados da Avelri.',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              fontSize: 12,
                              color: Color(0xFF68728C),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
      return Row(
        children: [
          if (wide)
            Expanded(
              child: Container(
                decoration: const BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [avelriNavy, Color(0xFF22124E)],
                  ),
                ),
                padding: const EdgeInsets.all(48),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const PlatformBrand(light: true),
                    const Spacer(),
                    const Icon(
                      Icons.hub_outlined,
                      color: Color(0xFFB6A0FF),
                      size: 64,
                    ),
                    const SizedBox(height: 24),
                    const Text(
                      'Cada academia.\nUma implantação bem cuidada.',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 34,
                        fontWeight: FontWeight.w700,
                        height: 1.2,
                      ),
                    ),
                    const SizedBox(height: 20),
                    const Text(
                      'Provisionamento seguro, etapas claras e histórico em um só lugar.',
                      style: TextStyle(
                        color: Color(0xFFB6BDD3),
                        fontSize: 16,
                        height: 1.6,
                      ),
                    ),
                    const Spacer(),
                    const Text(
                      'AVELRI • GESTÃO QUE ACOMPANHA O CRESCIMENTO',
                      style: TextStyle(
                        color: Color(0xFF8791AD),
                        fontSize: 10,
                        letterSpacing: 1.2,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          Expanded(child: loginForm),
        ],
      );
    },
  );
  Widget content() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Wrap(
        alignment: WrapAlignment.spaceBetween,
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 16,
        runSpacing: 16,
        children: [
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                section == 0
                    ? 'Sua operação, em perspectiva'
                    : section == 1
                    ? 'Academias cadastradas'
                    : 'Acompanhe cada implantação',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 6),
              Text(
                section == 0
                    ? 'Indicadores das academias provisionadas.'
                    : section == 1
                    ? 'Encontre uma academia e consulte seus dados.'
                    : 'Da ativação do Admin à conclusão.',
                style: const TextStyle(color: Color(0xFF68728C)),
              ),
            ],
          ),
          FilledButton.icon(
            onPressed: busy ? null : provision,
            icon: const Icon(Icons.add),
            label: const Text('Nova academia'),
          ),
        ],
      ),
      const SizedBox(height: 24),
      if (error != null) ...[
        errorText(error!),
        if (academies != null)
          const Text(
            'Os dados abaixo são da última consulta concluída.',
            style: TextStyle(color: Color(0xFF68728C)),
          ),
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            onPressed: busy ? null : load,
            icon: const Icon(Icons.refresh),
            label: const Text('Tentar novamente'),
          ),
        ),
      ],
      if (section == 0) ...[metrics(), const SizedBox(height: 24)],
      academyList(),
    ],
  );
  Widget metrics() {
    final all = academies,
        completed = academies?.where((a) => a['status'] == 'completed').length;
    final values = [
      all?.length,
      all == null ? null : all.length - completed!,
      completed,
    ];
    const titles = [
      'Academias cadastradas',
      'Implantações pendentes',
      'Implantações concluídas',
    ];
    const metricIcons = [
      Icons.storefront_outlined,
      Icons.pending_actions_outlined,
      Icons.task_alt_rounded,
    ];
    return LayoutBuilder(
      builder: (context, c) {
        final columns = c.maxWidth >= 750
            ? 3
            : c.maxWidth >= 480
            ? 2
            : 1;
        return Wrap(
          spacing: 16,
          runSpacing: 16,
          children: List.generate(
            3,
            (i) => SizedBox(
              width: (c.maxWidth - (columns - 1) * 16) / columns,
              child: Card(
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          CircleAvatar(
                            radius: 20,
                            backgroundColor: i == 2
                                ? const Color(0xFFEAF6EF)
                                : const Color(0xFFF0EBFC),
                            foregroundColor: i == 2
                                ? const Color(0xFF18704A)
                                : avelriPurple,
                            child: Icon(metricIcons[i], size: 21),
                          ),
                          const Spacer(),
                          const Icon(
                            Icons.insights_outlined,
                            size: 18,
                            color: Color(0xFF8791AD),
                          ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      Text(
                        values[i]?.toString() ?? '—',
                        key: ValueKey('metric-$i'),
                        style: const TextStyle(
                          fontSize: 32,
                          fontWeight: FontWeight.w700,
                          color: avelriNavy,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        titles[i],
                        style: const TextStyle(color: Color(0xFF68728C)),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        );
      },
    );
  }

  List<Map<String, dynamic>> get filtered {
    final query = search.text.trim().toLowerCase();
    return (academies ?? [])
        .where(
          (a) =>
              '${a['administrativeName']} ${a['adminName']} ${a['adminEmail']}'
                  .toLowerCase()
                  .contains(query) &&
              (status == 'all' ||
                  (status == 'pending'
                      ? a['status'] != 'completed'
                      : a['status'] == status)),
        )
        .toList();
  }

  Widget academyList() {
    final items = filtered;
    final pages = ((items.length + 24) ~/ 25).clamp(1, 1 << 30);
    final current = page.clamp(1, pages);
    final visible = items.skip((current - 1) * 25).take(25);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    section == 2 ? 'Etapas de implantação' : 'Academias',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ),
                IconButton(
                  tooltip: 'Atualizar academias',
                  onPressed: busy ? null : load,
                  icon: const Icon(Icons.refresh),
                ),
              ],
            ),
            const SizedBox(height: 12),
            LayoutBuilder(
              builder: (context, c) {
                final queryField = TextField(
                  key: const ValueKey('academy-search'),
                  controller: search,
                  decoration: const InputDecoration(
                    labelText: 'Buscar academia ou Admin',
                    prefixIcon: Icon(Icons.search),
                  ),
                  onChanged: (_) => setState(() => page = 1),
                );
                final statusField = DropdownButtonFormField<String>(
                  key: ValueKey('status-$status'),
                  initialValue: status,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Situação'),
                  items: const [
                    DropdownMenuItem(value: 'all', child: Text('Todas')),
                    DropdownMenuItem(
                      value: 'pending',
                      child: Text('Pendentes'),
                    ),
                    DropdownMenuItem(
                      value: 'awaiting-admin',
                      child: Text('Aguardando Admin'),
                    ),
                    DropdownMenuItem(
                      value: 'configuration',
                      child: Text('Em configuração'),
                    ),
                    DropdownMenuItem(
                      value: 'physical-validation',
                      child: Text('Homologação física'),
                    ),
                    DropdownMenuItem(
                      value: 'ready',
                      child: Text('Prontas para conclusão'),
                    ),
                    DropdownMenuItem(
                      value: 'completed',
                      child: Text('Concluídas'),
                    ),
                  ],
                  onChanged: (v) => setState(() {
                    status = v ?? 'all';
                    page = 1;
                  }),
                );
                return c.maxWidth >= 650
                    ? Row(
                        children: [
                          Expanded(child: queryField),
                          const SizedBox(width: 16),
                          SizedBox(width: 240, child: statusField),
                        ],
                      )
                    : Column(
                        children: [
                          queryField,
                          const SizedBox(height: 12),
                          statusField,
                        ],
                      );
              },
            ),
            const SizedBox(height: 16),
            if (academies == null)
              Padding(
                padding: const EdgeInsets.all(32),
                child: Text(
                  busy
                      ? 'Carregando academias…'
                      : 'Não foi possível carregar as academias.',
                  textAlign: TextAlign.center,
                ),
              )
            else if (items.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(
                  vertical: 36,
                  horizontal: 12,
                ),
                child: Column(
                  children: [
                    const Icon(
                      Icons.storefront_outlined,
                      size: 40,
                      color: Color(0xFF8791AD),
                    ),
                    const SizedBox(height: 12),
                    Text(
                      academies!.isEmpty
                          ? 'Nenhuma academia provisionada.'
                          : 'Nenhuma academia encontrada.',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 6),
                    const Text(
                      'Cadastre uma academia ou ajuste a busca e a situação.',
                      textAlign: TextAlign.center,
                    ),
                  ],
                ),
              )
            else ...[
              for (final a in visible) academyRow(a),
              const SizedBox(height: 12),
              Wrap(
                alignment: WrapAlignment.spaceBetween,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    '${items.length} ${items.length == 1 ? 'academia' : 'academias'}',
                    style: const TextStyle(
                      color: Color(0xFF68728C),
                      fontSize: 12,
                    ),
                  ),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        'Página $current de $pages',
                        style: const TextStyle(fontSize: 12),
                      ),
                      IconButton(
                        tooltip: 'Página anterior',
                        onPressed: busy || current <= 1
                            ? null
                            : () => setState(() => page = current - 1),
                        icon: const Icon(Icons.chevron_left),
                      ),
                      IconButton(
                        tooltip: 'Próxima página',
                        onPressed: busy || current >= pages
                            ? null
                            : () => setState(() => page = current + 1),
                        icon: const Icon(Icons.chevron_right),
                      ),
                    ],
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget academyRow(Map<String, dynamic> a) => InkWell(
    borderRadius: BorderRadius.circular(12),
    onTap: () => details(a),
    child: Container(
      padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 8),
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: Color(0xFFEDEEF4))),
      ),
      child: LayoutBuilder(
        builder: (context, c) {
          final identity = Row(
            children: [
              const CircleAvatar(
                backgroundColor: Color(0xFFF0EBFC),
                foregroundColor: avelriPurple,
                child: Icon(Icons.storefront_outlined, size: 21),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      a['administrativeName'] as String,
                      style: const TextStyle(fontWeight: FontWeight.w600),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      a['adminEmail'] as String,
                      style: const TextStyle(
                        fontSize: 12,
                        color: Color(0xFF68728C),
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
            ],
          );
          return c.maxWidth < 650
              ? Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    identity,
                    const SizedBox(height: 12),
                    StatusBadge(a['status'] as String?),
                    if (section == 2) ...[
                      const SizedBox(height: 10),
                      progress(a),
                    ],
                    const SizedBox(height: 8),
                    const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Ver implantação',
                          style: TextStyle(
                            color: avelriPurple,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        Icon(
                          Icons.chevron_right,
                          size: 16,
                          color: avelriPurple,
                        ),
                      ],
                    ),
                  ],
                )
              : Row(
                  children: [
                    Expanded(flex: 3, child: identity),
                    const SizedBox(width: 16),
                    if (section == 2) ...[
                      Expanded(flex: 2, child: progress(a)),
                      const SizedBox(width: 16),
                    ],
                    Flexible(
                      flex: 2,
                      child: StatusBadge(a['status'] as String?),
                    ),
                    const SizedBox(width: 8),
                    const Icon(Icons.chevron_right, color: Color(0xFF8791AD)),
                  ],
                );
        },
      ),
    ),
  );
  Widget progress(Map<String, dynamic> a) {
    final steps = [
      a['adminActivatedAt'] != null,
      a['configurationConfirmed'] == true,
      if (a['physicalAccessContracted'] == true)
        a['physicalValidationEvidence'] != null,
      a['completedAt'] != null,
    ];
    final done = steps.where((v) => v).length;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '$done de ${steps.length} etapas concluídas',
          style: const TextStyle(fontSize: 12, color: Color(0xFF68728C)),
        ),
        const SizedBox(height: 6),
        ClipRRect(
          borderRadius: BorderRadius.circular(4),
          child: LinearProgressIndicator(
            value: done / steps.length,
            minHeight: 5,
          ),
        ),
      ],
    );
  }
}
