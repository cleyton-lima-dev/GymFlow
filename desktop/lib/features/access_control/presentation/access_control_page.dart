import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:avelri_gestao/features/access_control/presentation/access_control_view_model.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

class AccessControlPage extends StatelessWidget {
  const AccessControlPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          AccessControlViewModel(AccessAgentsService(context.read<ApiClient>()))
            ..loadInitial(),
      child: const _AccessControlView(),
    );
  }
}

class _AccessControlView extends StatelessWidget {
  const _AccessControlView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<AccessControlViewModel>();

    final branding = context.watch<BrandingController>().branding;

    final primaryColor = branding.primaryColor;
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1250),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Controle de acesso',
                        style: TextStyle(
                          color: colorScheme.onSurface,
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 7),
                      Text(
                        'Acompanhe o Access Agent, a catraca e a sincronização com o Avelri.',
                        style: TextStyle(
                          color: colorScheme.onSurfaceVariant,
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  tooltip: 'Atualizar',
                  onPressed: viewModel.isLoading ? null : viewModel.refresh,
                  icon: Icon(Icons.refresh_rounded, color: primaryColor),
                ),
              ],
            ),
            const SizedBox(height: 26),
            if (viewModel.isLoading && !viewModel.hasAgents)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 80),
                child: Center(child: CircularProgressIndicator()),
              )
            else if (viewModel.errorMessage != null)
              _ErrorState(
                message: viewModel.errorMessage!,
                onRetry: viewModel.retry,
              )
            else if (!viewModel.hasAgents)
              const _EmptyState()
            else
              ...viewModel.agents.map(
                (agent) => Padding(
                  padding: const EdgeInsets.only(bottom: 20),
                  child: _AgentCard(agent: agent, primaryColor: primaryColor),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _AgentCard extends StatelessWidget {
  const _AgentCard({required this.agent, required this.primaryColor});

  final AccessAgentSummary agent;
  final Color primaryColor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: theme.dividerColor),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: primaryColor.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(Icons.dns_outlined, color: primaryColor),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      agent.name,
                      style: TextStyle(
                        color: colorScheme.onSurface,
                        fontSize: 18,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      agent.machineName,
                      style: TextStyle(
                        color: colorScheme.onSurfaceVariant,
                        fontSize: 13,
                      ),
                    ),
                  ],
                ),
              ),
              _StatusBadge(
                label: agent.isOnline ? 'Agent online' : 'Agent offline',
                healthy: agent.isOnline,
              ),
            ],
          ),
          const SizedBox(height: 22),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              _InfoTile(
                label: 'Última comunicação',
                value: _formatDateTime(agent.lastSeenAt),
              ),
              _InfoTile(
                label: 'Última sincronização offline',
                value: _formatDateTime(agent.lastOfflineSyncAt),
              ),
              _InfoTile(
                label: 'Eventos pendentes',
                value:
                    agent.pendingOfflineEvents?.toString() ?? 'Não informado',
              ),
              _InfoTile(
                label: 'Configuração',
                value: agent.configurationApplied ? 'Aplicada' : 'Pendente',
              ),
              _InfoTile(
                label: 'Liberação física',
                value: agent.releaseEnabled ? 'Ativada' : 'Desativada',
              ),
            ],
          ),
          const SizedBox(height: 22),
          Divider(color: theme.dividerColor),
          const SizedBox(height: 14),
          Text(
            'Catracas e dispositivos',
            style: TextStyle(
              color: colorScheme.onSurface,
              fontSize: 15,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 12),
          if (agent.devices.isEmpty)
            Text(
              'Nenhum dispositivo foi reportado pelo Agent.',
              style: TextStyle(color: colorScheme.onSurfaceVariant),
            )
          else
            ...agent.devices.map(
              (device) => Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _DeviceTile(device: device),
              ),
            ),
          if (agent.lastFailureAt != null || agent.lastFailureCode != null) ...[
            const SizedBox(height: 10),
            Divider(color: theme.dividerColor),
            const SizedBox(height: 14),
            _FailureTile(
              code: agent.lastFailureCode,
              occurredAt: agent.lastFailureAt,
            ),
          ],
        ],
      ),
    );
  }
}

class _DeviceTile extends StatelessWidget {
  const _DeviceTile({required this.device});

  final AccessDeviceStatus device;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final connected = device.enabled && device.connected;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: theme.dividerColor),
      ),
      child: Row(
        children: [
          Icon(
            Icons.door_sliding_outlined,
            color: connected
                ? const Color(0xFF2CB67D)
                : colorScheme.onSurfaceVariant,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _providerLabel(device.providerKey),
                  style: TextStyle(
                    color: colorScheme.onSurface,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  device.endpoint ?? 'Endereço não informado',
                  style: TextStyle(
                    color: colorScheme.onSurfaceVariant,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          _StatusBadge(
            label: !device.enabled
                ? 'Desabilitada'
                : device.connected
                ? 'Conectada'
                : 'Desconectada',
            healthy: connected,
          ),
        ],
      ),
    );
  }
}

class _FailureTile extends StatelessWidget {
  const _FailureTile({required this.code, required this.occurredAt});

  final String? code;
  final DateTime? occurredAt;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.warning_amber_rounded, color: Color(0xFFF3A712)),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Última falha',
                style: TextStyle(
                  color: colorScheme.onSurface,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                _failureLabel(code),
                style: TextStyle(color: colorScheme.onSurfaceVariant),
              ),
              if (occurredAt != null) ...[
                const SizedBox(height: 3),
                Text(
                  _formatDateTime(occurredAt),
                  style: TextStyle(
                    color: colorScheme.onSurfaceVariant,
                    fontSize: 12,
                  ),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _InfoTile extends StatelessWidget {
  const _InfoTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      width: 205,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: TextStyle(
              color: colorScheme.onSurfaceVariant,
              fontSize: 11,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 5),
          Text(
            value,
            style: TextStyle(
              color: colorScheme.onSurface,
              fontSize: 13,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.label, required this.healthy});

  final String label;
  final bool healthy;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    final color = healthy
        ? const Color(0xFF2CB67D)
        : colorScheme.onSurfaceVariant;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 7,
            height: 7,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: 6),
          Text(
            label,
            style: TextStyle(
              color: color,
              fontSize: 11,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 70),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Column(
        children: [
          Icon(
            Icons.dns_outlined,
            size: 46,
            color: colorScheme.onSurfaceVariant,
          ),
          const SizedBox(height: 12),
          Text(
            'Nenhum Access Agent configurado.',
            style: TextStyle(
              color: colorScheme.onSurfaceVariant,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 70),
      child: Center(
        child: Column(
          children: [
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 14),
            OutlinedButton(
              onPressed: onRetry,
              child: const Text('Tentar novamente'),
            ),
          ],
        ),
      ),
    );
  }
}

String _formatDateTime(DateTime? value) {
  if (value == null) {
    return 'Não informado';
  }

  return DateFormat('dd/MM/yyyy HH:mm').format(value.toLocal());
}

String _providerLabel(String providerKey) {
  return switch (providerKey) {
    'toletus-litenet2' => 'Toletus LiteNet2',
    _ => providerKey,
  };
}

String _failureLabel(String? code) {
  return switch (code) {
    'OfflineSync.ApiUnavailable' =>
      'API indisponível durante a sincronização offline.',
    'OfflineSync.AuthenticationUnavailable' =>
      'Não foi possível autenticar o Agent.',
    'OfflineSync.Unauthorized' => 'A autenticação do Agent foi recusada.',
    'OfflineSync.Timeout' => 'A sincronização offline excedeu o tempo limite.',
    'OfflineSync.Unexpected' =>
      'Ocorreu uma falha inesperada na sincronização offline.',
    null => 'Falha operacional registrada.',
    _ => code,
  };
}
