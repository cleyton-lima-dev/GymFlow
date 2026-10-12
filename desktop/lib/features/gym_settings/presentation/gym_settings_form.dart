import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/gym_settings/data/gym_settings_service.dart';
import 'package:flutter/material.dart';

class GymSettingsForm extends StatefulWidget {
  const GymSettingsForm({
    super.key,
    required this.settings,
    required this.service,
    required this.onSaved,
    required this.onReload,
  });
  final GymSettings settings;
  final GymSettingsService service;
  final ValueChanged<GymSettings> onSaved;
  final VoidCallback onReload;
  @override
  State<GymSettingsForm> createState() => _GymSettingsFormState();
}

class _GymSettingsFormState extends State<GymSettingsForm> {
  final form = GlobalKey<FormState>();
  late final name = TextEditingController(
    text: widget.settings.administrativeName ?? '',
  );
  late final zone = TextEditingController(
    text: widget.settings.timeZoneId ?? '',
  );
  bool saving = false;
  bool confirming = false;
  bool conflict = false;
  String? error;
  @override
  void dispose() {
    name.dispose();
    zone.dispose();
    super.dispose();
  }

  Future<void> save() async {
    if (saving || confirming || !form.currentState!.validate()) return;
    setState(() {
      confirming = true;
      error = null;
      conflict = false;
    });
    try {
      final changed = zone.text.trim() != widget.settings.timeZoneId;
      if (changed) {
        final confirmed = await showDialog<bool>(
          context: context,
          barrierDismissible: false,
          builder: (context) => AlertDialog(
            title: const Text('Confirmar mudança de fuso horário'),
            content: SizedBox(
              width: 480,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'De: ${widget.settings.timeZoneId ?? 'não configurado'}\nPara: ${zone.text.trim()}',
                    ),
                    const SizedBox(height: 16),
                    const Text(
                      'Após salvar, novas operações usarão o novo horário da academia. Isso pode mudar a interpretação de hoje, a vigência das matrículas, cobranças vencidas, frequência e períodos dos relatórios. Datas contratuais e registros históricos não serão convertidos.',
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context, false),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(context, true),
                child: const Text('Confirmar alteração'),
              ),
            ],
          ),
        );
        if (confirmed != true || !mounted) return;
      }
      setState(() {
        confirming = false;
        saving = true;
      });
      final result = await widget.service.save(
        name: name.text.trim(),
        timeZone: zone.text.trim(),
        version: widget.settings.version,
        confirmed: changed,
      );
      if (mounted) widget.onSaved(result);
    } on ApiException catch (ex) {
      if (mounted) {
        setState(() {
          conflict = ex.statusCode == 409;
          error = conflict
              ? 'As configurações foram alteradas por outra operação. Recarregue os dados antes de salvar.'
              : ex.statusCode == 400
              ? 'Verifique o nome fantasia e informe um fuso IANA válido disponível no servidor.'
              : 'Não foi possível salvar as configurações.';
        });
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => error = 'Não foi possível salvar. Consulte os dados atuais antes de tentar novamente.',
        );
      }
    } finally {
      if (mounted) {
        setState(() {
          saving = false;
          confirming = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => Form(
    key: form,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextFormField(
          controller: name,
          enabled: !saving && !confirming,
          maxLength: 150,
          decoration: const InputDecoration(
            labelText: 'Nome fantasia',
            helperMaxLines: 3,
            helperText: 'Nome administrativo da academia, independente da identidade visual.',
          ),
          validator: (value) => value == null || value.trim().isEmpty
              ? 'Informe o nome fantasia.'
              : value.length > 150
              ? 'Use até 150 caracteres.'
              : null,
        ),
        const SizedBox(height: 12),
        TextFormField(
          controller: zone,
          enabled: !saving && !confirming,
          maxLength: 100,
          decoration: const InputDecoration(
            labelText: 'Fuso horário (IANA)',
            hintText: 'America/Sao_Paulo',
          ),
          validator: (value) =>
              value == null ||
                  !RegExp(r'^(UTC|[A-Za-z0-9._+\-]+(?:/[A-Za-z0-9._+\-]+)+)$')
                      .hasMatch(value.trim())
              ? 'Informe um fuso IANA, como America/Sao_Paulo.'
              : null,
        ),
        Text(switch (widget.settings.timeZoneStatus) {
          'missing' => 'Fuso horário não configurado',
          'invalid' => 'Fuso horário inválido',
          _ =>
            widget.settings.timeZoneSource == 'persisted'
                ? 'Fuso salvo para esta academia.'
                : 'Fuso atual vindo da configuração legada.',
        }),
        const SizedBox(height: 12),
        const Text(
          'Mudanças de fuso exigem confirmação dos impactos. Registros históricos não serão convertidos.',
        ),
        if (error != null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Text(
              error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            FilledButton.icon(
              onPressed: saving || confirming || conflict ? null : save,
              icon: saving
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.save_outlined),
              label: Text(saving ? 'Salvando…' : 'Salvar configurações'),
            ),
            if (conflict)
              TextButton(
                onPressed: saving ? null : widget.onReload,
                child: const Text('Recarregar configurações'),
              ),
          ],
        ),
      ],
    ),
  );
}
