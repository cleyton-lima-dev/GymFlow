import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:flutter/material.dart';

class ConfigureAccessDeviceButton extends StatelessWidget {
  const ConfigureAccessDeviceButton({
    required this.agent,
    required this.primaryColor,
    required this.service,
    required this.onSaved,
    super.key,
  });

  final AccessAgentSummary agent;
  final Color primaryColor;
  final AccessAgentsService service;
  final Future<void> Function() onSaved;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: () => _showDialog(context),
      icon: const Icon(Icons.settings_ethernet_rounded),
      label: const Text('Configurar catraca'),
    );
  }

  Future<void> _showDialog(BuildContext context) async {
    AccessDeviceConfiguration? existing;

    for (final device in agent.desiredDevices) {
      if (device.providerKey == 'toletus-litenet2') {
        existing = device;
        break;
      }
    }

    final saved = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => _ConfigureAccessDeviceDialog(
        agentId: agent.id,
        primaryColor: primaryColor,
        service: service,
        initialHost: existing?.host ?? '',
        initialPort: existing?.port ?? 7878,
        initialEnabled: existing?.enabled ?? false,
      ),
    );

    if (saved == true) {
      await onSaved();
    }
  }
}

class _ConfigureAccessDeviceDialog extends StatefulWidget {
  const _ConfigureAccessDeviceDialog({
    required this.agentId,
    required this.primaryColor,
    required this.service,
    required this.initialHost,
    required this.initialPort,
    required this.initialEnabled,
  });

  final String agentId;
  final Color primaryColor;
  final AccessAgentsService service;
  final String initialHost;
  final int initialPort;
  final bool initialEnabled;

  @override
  State<_ConfigureAccessDeviceDialog> createState() =>
      _ConfigureAccessDeviceDialogState();
}

class _ConfigureAccessDeviceDialogState
    extends State<_ConfigureAccessDeviceDialog> {
  late final TextEditingController _hostController;
  late final TextEditingController _portController;

  late bool _enabled;

  bool _isSaving = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();

    _hostController = TextEditingController(text: widget.initialHost);

    _portController = TextEditingController(
      text: widget.initialPort.toString(),
    );

    _enabled = widget.initialEnabled;
  }

  @override
  void dispose() {
    _hostController.dispose();
    _portController.dispose();

    super.dispose();
  }

  Future<void> _save() async {
    if (_isSaving) {
      return;
    }

    final host = _hostController.text.trim();

    final port = int.tryParse(_portController.text.trim());

    if (host.isEmpty) {
      setState(() {
        _errorMessage = 'Informe o endereço IP da catraca.';
      });

      return;
    }

    if (port == null || port < 1 || port > 65535) {
      setState(() {
        _errorMessage = 'Informe uma porta válida.';
      });

      return;
    }

    setState(() {
      _isSaving = true;
      _errorMessage = null;
    });

    try {
      await widget.service.configureToletus(
        agentId: widget.agentId,
        host: host,
        port: port,
        enabled: _enabled,
      );

      if (!mounted) {
        return;
      }

      Navigator.of(context).pop(true);
    } on ApiException catch (exception) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = exception.statusCode == 400
            ? 'O IP ou a porta informados são inválidos.'
            : 'Não foi possível salvar a configuração.';

        _isSaving = false;
      });
    } on TimeoutException {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = 'A operação demorou mais que o esperado.';

        _isSaving = false;
      });
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _errorMessage = 'Não foi possível salvar a configuração.';

        _isSaving = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !_isSaving,
      child: AlertDialog(
        title: const Text('Configurar catraca'),
        content: SizedBox(
          width: 430,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Configure a comunicação de rede com a catraca. '
                'Habilitar a comunicação não ativa a liberação física de acesso.',
              ),
              const SizedBox(height: 18),
              TextField(
                controller: _hostController,
                enabled: !_isSaving,
                decoration: const InputDecoration(
                  labelText: 'Endereço IP',
                  hintText: '192.168.0.50',
                ),
              ),
              const SizedBox(height: 14),
              TextField(
                controller: _portController,
                enabled: !_isSaving,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Porta'),
              ),
              const SizedBox(height: 18),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                value: _enabled,
                onChanged: _isSaving
                    ? null
                    : (value) {
                        setState(() {
                          _enabled = value;
                        });
                      },
                title: const Text('Habilitar comunicação'),
                subtitle: Text(
                  _enabled
                      ? 'O Agent tentará conectar à catraca pelo endereço configurado.'
                      : 'O Agent manterá a catraca desconectada.',
                ),
              ),
              const SizedBox(height: 8),
              const Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.lock_outline_rounded, size: 18),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'A liberação física permanece desativada e exige uma etapa separada.',
                    ),
                  ),
                ],
              ),
              if (_errorMessage != null) ...[
                const SizedBox(height: 14),
                Text(
                  _errorMessage!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: _isSaving
                ? null
                : () => Navigator.of(context).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: widget.primaryColor),
            onPressed: _isSaving ? null : _save,
            child: _isSaving
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Salvar'),
          ),
        ],
      ),
    );
  }
}
