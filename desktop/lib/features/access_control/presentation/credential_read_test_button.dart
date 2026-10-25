import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:avelri_gestao/features/access_control/presentation/credential_read_probe.dart';
import 'package:flutter/material.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

class CredentialReadTestButton extends StatelessWidget {
  const CredentialReadTestButton({
    required this.agent,
    required this.primaryColor,
    required this.service,
    required this.onCompleted,
    super.key,
  });

  final AccessAgentSummary agent;
  final Color primaryColor;
  final AccessAgentsService service;
  final Future<void> Function() onCompleted;

  bool get _canRun =>
      agent.isOnline &&
      agent.configurationApplied &&
      agent.hasConnectedDevice &&
      !agent.releaseEnabled;

  String get _disabledReason {
    if (agent.releaseEnabled) {
      return 'Desative a liberação física antes de testar somente a leitura.';
    }

    if (!agent.isOnline) {
      return 'O Access Agent precisa estar online.';
    }

    if (!agent.configurationApplied) {
      return 'A configuração ainda não foi aplicada pelo Agent.';
    }

    if (!agent.hasConnectedDevice) {
      return 'A catraca precisa estar habilitada e conectada.';
    }

    return 'O teste de leitura não está disponível.';
  }

  @override
  Widget build(BuildContext context) {
    final button = OutlinedButton.icon(
      onPressed: _canRun ? () => _showTest(context) : null,
      icon: const Icon(Icons.contactless_outlined),
      label: const Text('Testar leitura'),
    );

    if (_canRun) {
      return button;
    }

    return Tooltip(message: _disabledReason, child: button);
  }

  Future<void> _showTest(BuildContext context) async {
    final startedAt = DateTime.now().toUtc();

    final latestReadAt = CredentialReadProbe.latestReadAt(agent);

    final baseline = latestReadAt != null && latestReadAt.isAfter(startedAt)
        ? latestReadAt
        : startedAt;

    final detected = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => _CredentialReadTestDialog(
        agentId: agent.id,
        baseline: baseline,
        primaryColor: primaryColor,
        service: service,
      ),
    );

    if (detected == true) {
      await onCompleted();
    }
  }
}

class _CredentialReadTestDialog extends StatefulWidget {
  const _CredentialReadTestDialog({
    required this.agentId,
    required this.baseline,
    required this.primaryColor,
    required this.service,
  });

  final String agentId;
  final DateTime baseline;
  final Color primaryColor;
  final AccessAgentsService service;

  @override
  State<_CredentialReadTestDialog> createState() =>
      _CredentialReadTestDialogState();
}

class _CredentialReadTestDialogState extends State<_CredentialReadTestDialog> {
  CredentialReadObservation? _result;

  bool _isRunning = true;
  bool _timedOut = false;

  String? _errorMessage;

  @override
  void initState() {
    super.initState();

    WidgetsBinding.instance.addPostFrameCallback((_) => _runTest());
  }

  Future<void> _runTest() async {
    if (!mounted) {
      return;
    }

    setState(() {
      _isRunning = true;
      _timedOut = false;
      _result = null;
      _errorMessage = null;
    });

    try {
      final probe = CredentialReadProbe(widget.service.getAgents);

      final result = await probe.waitForNextRead(
        agentId: widget.agentId,
        baseline: widget.baseline,
      );

      if (!mounted) {
        return;
      }

      setState(() {
        _isRunning = false;

        if (result == null) {
          _timedOut = true;
        } else {
          _result = result;
        }
      });
    } on ApiException {
      if (!mounted) {
        return;
      }

      setState(() {
        _isRunning = false;
        _errorMessage = 'Não foi possível consultar o status do Access Agent.';
      });
    } on TimeoutException {
      if (!mounted) {
        return;
      }

      setState(() {
        _isRunning = false;
        _errorMessage = 'A consulta ao Avelri demorou mais que o esperado.';
      });
    } on http.ClientException {
      if (!mounted) {
        return;
      }

      setState(() {
        _isRunning = false;
        _errorMessage = 'Não foi possível conectar ao Avelri durante o teste.';
      });
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _isRunning = false;
        _errorMessage = 'O teste de leitura não pôde ser concluído.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return PopScope(
      canPop: !_isRunning,
      child: AlertDialog(
        title: const Text('Testar leitura da catraca'),
        content: SizedBox(
          width: 470,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Apresente um cartão, PIN ou biometria na catraca. '
                'O Avelri verificará somente se a credencial foi lida.',
              ),
              const SizedBox(height: 12),
              const Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.lock_outline_rounded, size: 18),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'A liberação física permanece desativada durante este teste.',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 22),
              if (_isRunning)
                const _WaitingState()
              else if (_result != null)
                _SuccessState(result: _result!)
              else if (_timedOut)
                const _TimeoutState()
              else if (_errorMessage != null)
                Text(
                  _errorMessage!,
                  style: TextStyle(color: colorScheme.error),
                ),
            ],
          ),
        ),
        actions: [
          if (!_isRunning && (_timedOut || _errorMessage != null))
            TextButton(
              onPressed: _runTest,
              child: const Text('Tentar novamente'),
            ),
          TextButton(
            onPressed: _isRunning
                ? null
                : () => Navigator.of(context).pop(_result != null),
            child: const Text('Fechar'),
          ),
        ],
      ),
    );
  }
}

class _WaitingState extends StatelessWidget {
  const _WaitingState();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      children: [
        const SizedBox(
          width: 22,
          height: 22,
          child: CircularProgressIndicator(strokeWidth: 2.5),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            'Aguardando uma nova leitura... '
            'Isso pode levar até cerca de 90 segundos.',
            style: TextStyle(color: colorScheme.onSurfaceVariant),
          ),
        ),
      ],
    );
  }
}

class _SuccessState extends StatelessWidget {
  const _SuccessState({required this.result});

  final CredentialReadObservation result;

  @override
  Widget build(BuildContext context) {
    final occurredAt = DateFormat('dd/MM/yyyy HH:mm:ss')
        .format(result.readAt.toLocal());

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(
          Icons.check_circle_outline_rounded,
          color: AvelriSemanticColors.success,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            'Leitura detectada com sucesso.\n'
            '${_credentialTypeLabel(result.credentialType)} • $occurredAt\n'
            'Nenhum identificador da credencial foi exibido e a catraca não foi liberada fisicamente.',
          ),
        ),
      ],
    );
  }
}

class _TimeoutState extends StatelessWidget {
  const _TimeoutState();

  @override
  Widget build(BuildContext context) {
    return const Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.schedule_rounded, color: AvelriSemanticColors.warning),
        SizedBox(width: 10),
        Expanded(
          child: Text(
            'Nenhuma nova leitura foi recebida dentro do tempo esperado. '
            'Confirme a conexão da catraca e tente novamente.',
          ),
        ),
      ],
    );
  }
}

String _credentialTypeLabel(int value) {
  return switch (value) {
    1 => 'Cartão',
    2 => 'QR Code',
    3 => 'Biometria',
    4 => 'Reconhecimento facial',
    5 => 'PIN',
    99 => 'Outro',
    _ => 'Credencial',
  };
}
