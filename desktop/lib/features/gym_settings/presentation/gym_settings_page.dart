import 'gym_settings_form.dart';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:avelri_gestao/features/access_control/presentation/physical_release_control_button.dart';
import 'package:avelri_gestao/features/gym_settings/data/gym_settings_service.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

class GymSettingsPage extends StatefulWidget {
  const GymSettingsPage({super.key});
  @override
  State<GymSettingsPage> createState() => _GymSettingsPageState();
}

class _GymSettingsPageState extends State<GymSettingsPage> {
  late final AccessAgentsService agentsService;
  late final GymSettingsService settingsService;
  late Future<GymSettings> settings;
  late Future<List<AccessAgentSummary>> agents;
  int selectedSection = 0;
  final contentKey = GlobalKey();

  @override
  void initState() {
    super.initState();
    final api = context.read<ApiClient>();
    settingsService = GymSettingsService(api);
    agentsService = AccessAgentsService(api);
    settings = settingsService.get();
    agents = agentsService.getAgents();
  }

  void refresh() => setState(() {
    settings = settingsService.get();
    agents = agentsService.getAgents();
  });

  Future<void> refreshAgents() async {
    if (!mounted) return;
    final pending = agentsService.getAgents();
    setState(() {
      agents = pending;
    });
    try {
      await pending;
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Configuração operacional salva. A aplicação no equipamento depende da sincronização do agente.',
            ),
          ),
        );
      }
    } catch (_) {
      // FutureBuilder presents the refresh failure independently of the saved operation.
    }
  }

  Widget section(String title, List<Widget> children) => Card(
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          ...children,
        ],
      ),
    ),
  );

  Widget error(String message) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(
        message,
        style: TextStyle(color: Theme.of(context).colorScheme.error),
      ),
      TextButton.icon(
        onPressed: refresh,
        icon: const Icon(Icons.refresh),
        label: const Text('Tentar novamente'),
      ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final sections = [
      section('Dados da academia', [
        FutureBuilder<GymSettings>(
          future: settings,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const LinearProgressIndicator();
            }
            if (snapshot.hasError) {
              return error('Não foi possível consultar o fuso horário.');
            }
            final data = snapshot.requireData;
            return GymSettingsForm(
              key: ValueKey(data.version),
              settings: data,
              service: settingsService,
              onReload: refresh,
              onSaved: (saved) {
                setState(() {
                  settings = Future.value(saved);
                });
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text(
                      'Configurações salvas. Novas operações usarão o fuso salvo.',
                    ),
                  ),
                );
              },
            );
          },
        ),
      ]),
      section('Agentes e equipamentos', [
        const Text(
          'Estado informado pelos agentes vinculados. Comunicação, liberação física e passagem confirmada são informações distintas.',
        ),
        const SizedBox(height: 12),
        FutureBuilder<List<AccessAgentSummary>>(
          future: agents,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const LinearProgressIndicator();
            }
            if (snapshot.hasError) {
              return error('Não foi possível consultar os agentes.');
            }
            if (snapshot.requireData.isEmpty) {
              return const Text('Nenhum agente vinculado à academia.');
            }
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: snapshot.requireData
                  .map(
                    (agent) => Padding(
                      padding: const EdgeInsets.only(bottom: 16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Divider(),
                          Text(agent.name, style: theme.textTheme.titleMedium),
                          Wrap(
                            spacing: 8,
                            runSpacing: 4,
                            children: [
                              Chip(
                                label: Text(
                                  agent.isActive
                                      ? 'Agente ativo'
                                      : 'Agente inativo',
                                ),
                              ),
                              Chip(
                                label: Text(
                                  agent.isOnline ? 'Online' : 'Offline',
                                ),
                              ),
                              Chip(
                                label: Text(
                                  agent.configurationApplied
                                      ? 'Configuração aplicada'
                                      : 'Configuração pendente',
                                ),
                              ),
                              Chip(
                                label: Text(
                                  agent.releaseEnabled
                                      ? 'Liberação física ativa'
                                      : 'Liberação física desativada',
                                ),
                              ),
                            ],
                          ),
                          if (agent.devices.isEmpty)
                            const Text(
                              'Nenhum estado de equipamento informado.',
                            ),
                          for (final (index, device) in agent.devices.indexed)
                            Text(
                              'Equipamento ${index + 1}: ${device.enabled ? 'comunicação habilitada' : 'comunicação desabilitada'} • ${device.connected ? 'conectado' : 'desconectado'}',
                            ),
                          const SizedBox(height: 8),
                          PhysicalReleaseControlButton(
                            agent: agent,
                            primaryColor: theme.colorScheme.primary,
                            service: agentsService,
                            onSaved: refreshAgents,
                          ),
                        ],
                      ),
                    ),
                  )
                  .toList(),
            );
          },
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: () => context.go('/access-control'),
          icon: const Icon(Icons.settings_input_component),
          label: const Text('Abrir controle de acesso'),
        ),
      ]),
    ];
    const labels = ['Dados da academia', 'Controle de acesso'];
    const icons = [Icons.storefront_outlined, Icons.sensor_door_outlined];
    return LayoutBuilder(
      builder: (context, constraints) {
        final wide = constraints.maxWidth >= 960;
        final navigation = Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var index = 0; index < labels.length; index++)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: ListTile(
                  selected: selectedSection == index,
                  selectedTileColor: theme.colorScheme.secondaryContainer,
                  selectedColor: theme.colorScheme.onSecondaryContainer,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  leading: Icon(icons[index]),
                  title: Text(labels[index]),
                  onTap: () => setState(() => selectedSection = index),
                ),
              ),
          ],
        );
        final content = IndexedStack(
          key: contentKey,
          index: selectedSection,
          children: [
            for (var index = 0; index < sections.length; index++)
              SingleChildScrollView(
                key: PageStorageKey('gym-settings-section-$index'),
                padding: const EdgeInsets.only(bottom: 16),
                child: Align(
                  alignment: Alignment.topLeft,
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 900),
                    child: sections[index],
                  ),
                ),
              ),
          ],
        );
        return Padding(
          padding: EdgeInsets.all(wide ? 24 : 16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  if (!wide)
                    Expanded(
                      child: DropdownButtonFormField<int>(
                        key: const ValueKey('settings-section-selector'),
                        initialValue: selectedSection,
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'Seção',
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                        items: [
                          for (var index = 0; index < labels.length; index++)
                            DropdownMenuItem(
                              value: index,
                              child: Text(labels[index]),
                            ),
                        ],
                        onChanged: (value) {
                          if (value != null) {
                            setState(() => selectedSection = value);
                          }
                        },
                      ),
                    )
                  else
                    const Spacer(),
                  IconButton(
                    tooltip: 'Atualizar configurações',
                    onPressed: refresh,
                    icon: const Icon(Icons.refresh),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Expanded(
                child: wide
                    ? Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          SizedBox(width: 240, child: navigation),
                          const SizedBox(width: 24),
                          Expanded(child: content),
                        ],
                      )
                    : content,
              ),
            ],
          ),
        );
      },
    );
  }
}
