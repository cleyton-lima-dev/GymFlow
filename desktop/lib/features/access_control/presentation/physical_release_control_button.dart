import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:flutter/material.dart';

class PhysicalReleaseControlButton extends StatelessWidget {
  const PhysicalReleaseControlButton({
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

  bool get _canEnable =>
      agent.isOnline &&
      agent.configurationApplied &&
      agent.hasConnectedDevice;

  @override
  Widget build(BuildContext context) {
    final enabled = agent.releaseEnabled;

    final button = OutlinedButton.icon(
      onPressed: enabled || _canEnable
          ? () => _showConfirmation(context)
          : null,
      icon: Icon(
        enabled
            ? Icons.lock_outline_rounded
            : Icons.lock_open_rounded,
      ),
      label: Text(
        enabled
            ? 'Desativar liberação física'
            : 'Ativar liberação física',
      ),
    );

    if (enabled || _canEnable) {
      return button;
    }

    return Tooltip(
      message:
          'Para ativar, o Agent precisa estar online, com a configuração aplicada e uma catraca conectada.',
      child: button,
    );
  }

  Future<void> _showConfirmation(BuildContext context) async {
    final enable = !agent.releaseEnabled;

    final saved = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => _PhysicalReleaseConfirmationDialog(
        agentId: agent.id,
        enable: enable,
        primaryColor: primaryColor,
        service: service,
      ),
    );

    if (saved == true) {
      await onSaved();
    }
  }
}

class _PhysicalReleaseConfirmationDialog extends StatefulWidget {
  const _PhysicalReleaseConfirmationDialog({
    required this.agentId,
    required this.enable,
    required this.primaryColor,
    required this.service,
  });

  final String agentId;
  final bool enable;
  final Color primaryColor;
  final AccessAgentsService service;

  @override
  State<_PhysicalReleaseConfirmationDialog> createState() =>
      _PhysicalReleaseConfirmationDialogState();
}

class _PhysicalReleaseConfirmationDialogState
    extends State<_PhysicalReleaseConfirmationDialog> {
  bool _confirmed = false;
  bool _isSaving = false;
  String? _errorMessage;

  Future<void> _save() async {
    if (_isSaving) {
      return;
    }

    if (widget.enable && !_confirmed) {
      return;
    }

    setState(() {
      _isSaving = true;
      _errorMessage = null;
    });

    try {
      await widget.service.setReleaseEnabled(
        agentId: widget.agentId,
        enabled: widget.enable,
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
        _errorMessage = exception.statusCode == 409
            ? 'A liberação física não pode ser ativada enquanto o Agent ou a catraca não estiverem prontos.'
            : 'Não foi possível alterar a liberação física.';

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
        _errorMessage = 'Não foi possível alterar a liberação física.';
        _isSaving = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final enable = widget.enable;
    final colorScheme = Theme.of(context).colorScheme;

    return PopScope(
      canPop: !_isSaving,
      child: AlertDialog(
        title: Text(
          enable
              ? 'Ativar liberação física?'
              : 'Desativar liberação física?',
        ),
        content: SizedBox(
          width: 460,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                enable
                    ? 'Quando esta opção estiver ativa, uma decisão de acesso permitida poderá acionar fisicamente a catraca.'
                    : 'A catraca deixará de ser liberada fisicamente pelo Avelri. A comunicação com o equipamento poderá continuar ativa.',
              ),
              if (enable) ...[
                const SizedBox(height: 18),
                CheckboxListTile(
                  contentPadding: EdgeInsets.zero,
                  value: _confirmed,
                  onChanged: _isSaving
                      ? null
                      : (value) {
                          setState(() {
                            _confirmed = value ?? false;
                          });
                        },
                  title: const Text(
                    'Confirmo que a catraca está pronta para homologação e que desejo permitir o acionamento físico.',
                  ),
                  controlAffinity: ListTileControlAffinity.leading,
                ),
              ],
              if (_errorMessage != null) ...[
                const SizedBox(height: 14),
                Text(
                  _errorMessage!,
                  style: TextStyle(color: colorScheme.error),
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
            style: FilledButton.styleFrom(
              backgroundColor: enable
                  ? colorScheme.error
                  : widget.primaryColor,
            ),
            onPressed:
                _isSaving || (enable && !_confirmed)
                    ? null
                    : _save,
            child: _isSaving
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : Text(
                    enable
                        ? 'Ativar liberação'
                        : 'Desativar liberação',
                  ),
          ),
        ],
      ),
    );
  }
}