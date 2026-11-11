import 'dart:async';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:avelri_gestao/core/update/release_manifest_validator.dart';

import 'update_controller.dart';

class UpdateDialog extends StatefulWidget {
  const UpdateDialog({this.controller, this.checkOnOpen = false, super.key});

  /// A supplied controller is useful for isolated widget tests.
  final UpdateController? controller;
  final bool checkOnOpen;

  @override
  State<UpdateDialog> createState() => _UpdateDialogState();
}

class _UpdateDialogState extends State<UpdateDialog> {
  late final UpdateController _controller;
  late final bool _ownsController;

  @override
  void initState() {
    super.initState();
    _ownsController = widget.controller == null;
    _controller =
        widget.controller ??
        UpdateController.forDesktop(
          requestDesktopShutdown: () async {
            await ServicesBinding.instance.exitApplication(
              ui.AppExitType.required,
            );
          },
        );
    _controller.addListener(_refresh);
    if (_ownsController || _controller.installedVersion == null) {
      unawaited(_controller.initialize());
    } else if (widget.checkOnOpen) {
      unawaited(_controller.check());
    }
  }

  void _refresh() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _controller.removeListener(_refresh);
    if (_ownsController) {
      _controller.cancelDownload();
      _controller.dispose();
    } else {
      unawaited(_controller.handleDialogClosed());
    }
    super.dispose();
  }

  Future<bool> _confirmInstallation(ReleaseManifest release) async {
    if (!mounted) return false;
    return await showDialog<bool>(
          context: context,
          barrierDismissible: false,
          builder: (context) => InstallUpdateConfirmationDialog(
            version: release.version.toString(),
            buildNumber: release.build,
            installedVersion: _controller.installedVersion?.version,
            installedBuild: _controller.installedVersion?.build,
          ),
        ) ??
        false;
  }

  @override
  Widget build(BuildContext context) {
    final version = _controller.installedVersion;
    final release = _controller.availableRelease;
    final state = _controller.state;
    final scheme = Theme.of(context).colorScheme;
    final status = switch (state) {
      UpdateViewState.notConfigured => (
        icon: Icons.info_outline_rounded,
        title: 'Atualizações não configuradas',
        detail:
            'A verificação de atualizações ainda não está configurada '
            'nesta instalação.',
        color: scheme.onSurfaceVariant,
      ),
      UpdateViewState.checking => (
        icon: Icons.sync_rounded,
        title: 'Verificando atualizações…',
        detail: 'Aguarde enquanto a versão disponível é verificada.',
        color: scheme.primary,
      ),
      UpdateViewState.current => (
        icon: Icons.check_circle_outline_rounded,
        title: 'Tudo em dia',
        detail: 'O Avelri Gestão está atualizado.',
        color: const Color(0xFF247454),
      ),
      UpdateViewState.available => (
        icon: Icons.new_releases_outlined,
        title: 'Atualização disponível:',
        detail: 'Uma nova versão verificada está disponível para download.',
        color: scheme.primary,
      ),
      UpdateViewState.downloading => (
        icon: Icons.download_rounded,
        title: 'Baixando atualização',
        detail: 'O instalador será verificado antes de ficar disponível.',
        color: scheme.primary,
      ),
      UpdateViewState.ready => (
        icon: Icons.verified_outlined,
        title: 'Download concluído',
        detail: 'O instalador está pronto para instalação.',
        color: scheme.primary,
      ),
      UpdateViewState.installing => (
        icon: Icons.install_desktop_rounded,
        title: 'Iniciando o instalador…',
        detail: 'Aguarde a confirmação de abertura do instalador.',
        color: scheme.primary,
      ),
      UpdateViewState.started => (
        icon: Icons.open_in_new_rounded,
        title: 'Instalador iniciado',
        detail: _controller.errorMessage ?? 'Instalador iniciado.',
        color: scheme.primary,
      ),
      UpdateViewState.error => (
        icon: Icons.error_outline_rounded,
        title: 'Não foi possível atualizar',
        detail: _controller.errorMessage ?? 'Não foi possível atualizar.',
        color: scheme.error,
      ),
    };
    final showNewVersion =
        release != null &&
        (state == UpdateViewState.available ||
            state == UpdateViewState.downloading ||
            state == UpdateViewState.ready ||
            state == UpdateViewState.installing ||
            state == UpdateViewState.started);

    return _UpdateDialogFrame(
      eyebrow: 'ATUALIZAÇÕES',
      title: 'Avelri Gestão',
      icon: Icons.system_update_alt_rounded,
      content: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (showNewVersion)
            _VersionComparison(
              installedVersion: version?.version,
              installedBuild: version?.build,
              newVersion: release.version.toString(),
              newBuild: release.build,
            )
          else
            _VersionCard(
              label: 'VERSÃO INSTALADA',
              version: version?.version,
              buildNumber: version?.build,
            ),
          const SizedBox(height: 20),
          _StatusPanel(
            icon: status.icon,
            title: status.title,
            detail: status.detail,
            color: status.color,
            busy:
                state == UpdateViewState.checking ||
                state == UpdateViewState.installing,
            progress: state == UpdateViewState.downloading
                ? _controller.progress
                : null,
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: state == UpdateViewState.installing
              ? null
              : () => Navigator.of(context).pop(),
          child: const Text('Fechar'),
        ),
        if (state == UpdateViewState.downloading)
          TextButton(
            onPressed: _controller.cancelDownload,
            child: const Text('Cancelar download'),
          ),
        if (state == UpdateViewState.error)
          FilledButton(
            onPressed: () => unawaited(_controller.retry()),
            child: const Text('Tentar novamente'),
          ),
        if (state == UpdateViewState.available)
          FilledButton.icon(
            onPressed: () => unawaited(_controller.download()),
            icon: const Icon(Icons.download_rounded, size: 18),
            label: const Text('Baixar atualização'),
          ),
        if (state == UpdateViewState.ready)
          FilledButton.icon(
            onPressed: () =>
                unawaited(_controller.install(_confirmInstallation)),
            icon: const Icon(Icons.install_desktop_rounded, size: 18),
            label: const Text('Instalar atualização'),
          ),
      ],
    );
  }
}

class InstallUpdateConfirmationDialog extends StatelessWidget {
  const InstallUpdateConfirmationDialog({
    required this.version,
    required this.buildNumber,
    this.installedVersion,
    this.installedBuild,
    super.key,
  });

  final String version;
  final int buildNumber;
  final String? installedVersion;
  final int? installedBuild;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return _UpdateDialogFrame(
      eyebrow: 'CONFIRMAÇÃO',
      title: 'Instalar atualização?',
      icon: Icons.install_desktop_rounded,
      content: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (installedVersion != null && installedBuild != null)
            _VersionComparison(
              installedVersion: installedVersion,
              installedBuild: installedBuild,
              newVersion: version,
              newBuild: buildNumber,
            )
          else
            _VersionCard(
              label: 'NOVA VERSÃO',
              version: version,
              buildNumber: buildNumber,
              highlighted: true,
            ),
          const SizedBox(height: 20),
          _StatusPanel(
            icon: Icons.info_outline_rounded,
            title: 'Antes de continuar',
            detail:
                'Salve seu trabalho antes de continuar. Se o instalador '
                'iniciar corretamente, o Avelri Gestão será fechado para '
                'permitir a atualização.',
            color: scheme.primary,
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: const Text('Cancelar'),
        ),
        FilledButton.icon(
          onPressed: () => Navigator.of(context).pop(true),
          icon: const Icon(Icons.install_desktop_rounded, size: 18),
          label: const Text('Instalar e fechar'),
        ),
      ],
    );
  }
}

class _UpdateDialogFrame extends StatelessWidget {
  const _UpdateDialogFrame({
    required this.eyebrow,
    required this.title,
    required this.icon,
    required this.content,
    required this.actions,
  });

  final String eyebrow;
  final String title;
  final IconData icon;
  final Widget content;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Dialog(
      backgroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 18, vertical: 24),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      clipBehavior: Clip.antiAlias,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Container(
                padding: const EdgeInsets.fromLTRB(28, 26, 28, 25),
                decoration: const BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [Color(0xFFF5F1FD), Colors.white],
                  ),
                ),
                child: Row(
                  children: [
                    Container(
                      width: 52,
                      height: 52,
                      decoration: BoxDecoration(
                        color: scheme.primary.withValues(alpha: 0.10),
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: Icon(icon, color: scheme.primary, size: 27),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            eyebrow,
                            style: TextStyle(
                              color: scheme.primary,
                              fontSize: 11,
                              fontWeight: FontWeight.w800,
                              letterSpacing: 1.4,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            title,
                            style: TextStyle(
                              color: scheme.onSurface,
                              fontSize: 22,
                              fontWeight: FontWeight.w800,
                              height: 1.2,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(28, 4, 28, 28),
                child: content,
              ),
              Divider(height: 1, color: scheme.outlineVariant),
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 14, 20, 18),
                child: Align(
                  alignment: Alignment.centerRight,
                  child: Wrap(
                    alignment: WrapAlignment.end,
                    spacing: 8,
                    runSpacing: 8,
                    children: actions,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _VersionComparison extends StatelessWidget {
  const _VersionComparison({
    required this.installedVersion,
    required this.installedBuild,
    required this.newVersion,
    required this.newBuild,
  });

  final String? installedVersion;
  final int? installedBuild;
  final String newVersion;
  final int newBuild;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final current = _VersionCard(
        label: 'VERSÃO INSTALADA',
        version: installedVersion,
        buildNumber: installedBuild,
      );
      final next = _VersionCard(
        label: 'NOVA VERSÃO',
        version: newVersion,
        buildNumber: newBuild,
        highlighted: true,
      );
      final arrow = Icon(
        constraints.maxWidth < 410
            ? Icons.arrow_downward_rounded
            : Icons.arrow_forward_rounded,
        color: Theme.of(context).colorScheme.primary,
        size: 20,
      );
      if (constraints.maxWidth < 410) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            current,
            const SizedBox(height: 8),
            arrow,
            const SizedBox(height: 8),
            next,
          ],
        );
      }
      return Row(
        children: [
          Expanded(child: current),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: arrow,
          ),
          Expanded(child: next),
        ],
      );
    },
  );
}

class _VersionCard extends StatelessWidget {
  const _VersionCard({
    required this.label,
    required this.version,
    required this.buildNumber,
    this.highlighted = false,
  });

  final String label;
  final String? version;
  final int? buildNumber;
  final bool highlighted;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: highlighted ? const Color(0xFFF7F3FD) : const Color(0xFFF8F9FC),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: highlighted
              ? scheme.primary.withValues(alpha: 0.24)
              : scheme.outlineVariant,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: TextStyle(
              color: highlighted ? scheme.primary : scheme.onSurfaceVariant,
              fontSize: 10,
              fontWeight: FontWeight.w800,
              letterSpacing: 1.1,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            version ?? '—',
            style: TextStyle(
              color: scheme.onSurface,
              fontSize: 24,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            buildNumber == null ? 'Lendo versão…' : 'Build $buildNumber',
            style: TextStyle(color: scheme.onSurfaceVariant, fontSize: 12),
          ),
        ],
      ),
    );
  }
}

class _StatusPanel extends StatelessWidget {
  const _StatusPanel({
    required this.icon,
    required this.title,
    required this.detail,
    required this.color,
    this.busy = false,
    this.progress,
  });

  final IconData icon;
  final String title;
  final String detail;
  final Color color;
  final bool busy;
  final double? progress;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: color.withValues(alpha: 0.14)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: busy
                    ? SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2.5,
                          color: color,
                        ),
                      )
                    : Icon(icon, color: color, size: 22),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: TextStyle(
                        color: scheme.onSurface,
                        fontSize: 15,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 5),
                    Text(
                      detail,
                      style: TextStyle(
                        color: scheme.onSurfaceVariant,
                        fontSize: 13,
                        height: 1.45,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (progress != null) ...[
            const SizedBox(height: 18),
            Row(
              children: [
                Text(
                  'Progresso do download',
                  style: TextStyle(
                    color: scheme.onSurfaceVariant,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const Spacer(),
                Text(
                  '${(progress! * 100).round()}%',
                  style: TextStyle(
                    color: scheme.primary,
                    fontSize: 13,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 9),
            LinearProgressIndicator(
              value: progress,
              minHeight: 7,
              borderRadius: BorderRadius.circular(5),
              backgroundColor: scheme.primary.withValues(alpha: 0.12),
              valueColor: AlwaysStoppedAnimation<Color>(scheme.primary),
            ),
          ],
        ],
      ),
    );
  }
}
