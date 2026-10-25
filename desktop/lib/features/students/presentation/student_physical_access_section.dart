import 'package:avelri_gestao/features/students/models/physical_access_credential.dart';
import 'package:avelri_gestao/features/students/models/physical_access_override.dart';
import 'package:flutter/material.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:avelri_gestao/app/theme/app_button_styles.dart';

class StudentPhysicalAccessSection extends StatelessWidget {
  const StudentPhysicalAccessSection({
    required this.credentials,
    required this.accessOverride,
    required this.isLoading,
    required this.errorMessage,
    required this.primaryColor,
    required this.onRefresh,
    required this.onAddCredential,
    required this.onIssuePin,
    required this.onToggleCredential,
    required this.onAllowAccess,
    required this.onBlockAccess,
    required this.onRemoveOverride,
    super.key,
  });

  final List<PhysicalAccessCredential> credentials;
  final PhysicalAccessOverride? accessOverride;
  final bool isLoading;
  final String? errorMessage;
  final Color primaryColor;

  final Future<void> Function() onRefresh;
  final VoidCallback onAddCredential;
  final VoidCallback onIssuePin;

  final Future<void> Function(PhysicalAccessCredential credential)
  onToggleCredential;

  final VoidCallback onAllowAccess;
  final VoidCallback onBlockAccess;
  final VoidCallback onRemoveOverride;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(26),
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
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: primaryColor.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(
                  Icons.door_front_door_outlined,
                  color: primaryColor,
                  size: 20,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  'Controle de acesso',
                  style: TextStyle(
                    color: colorScheme.onSurface,
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              IconButton(
                tooltip: 'Atualizar controle de acesso',
                onPressed: isLoading ? null : onRefresh,
                icon: Icon(Icons.refresh_rounded, color: primaryColor),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Gerencie credenciais da catraca e exceções manuais de entrada.',
            style: TextStyle(color: colorScheme.onSurfaceVariant, fontSize: 13),
          ),
          const SizedBox(height: 24),

          if (errorMessage != null) ...[
            Text(
              errorMessage!,
              style: TextStyle(
                color: Theme.of(context).colorScheme.error,
                fontSize: 12,
              ),
            ),
            const SizedBox(height: 16),
          ],

          _OverrideCard(
            accessOverride: accessOverride,
            primaryColor: primaryColor,
            isLoading: isLoading,
            onAllowAccess: onAllowAccess,
            onBlockAccess: onBlockAccess,
            onRemoveOverride: onRemoveOverride,
          ),

          const SizedBox(height: 22),

          Row(
            children: [
              Expanded(
                child: Text(
                  'Credenciais',
                  style: TextStyle(
                    color: colorScheme.onSurface,
                    fontSize: 14,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              OutlinedButton.icon(
                onPressed: isLoading ? null : onAddCredential,
                icon: const Icon(Icons.add_rounded, size: 18),
                label: const Text('Adicionar credencial'),
              ),
              OutlinedButton.icon(
                onPressed: isLoading ? null : onIssuePin,
                icon: const Icon(Icons.pin_outlined, size: 18),
                label: Text(
                  credentials.any((item) => item.type == 5)
                      ? 'Redefinir PIN'
                      : 'Gerar PIN',
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),

          if (isLoading && credentials.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 24),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (credentials.isEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 22),
              child: Center(
                child: Text(
                  'Nenhuma credencial cadastrada.',
                  style: TextStyle(color: colorScheme.onSurfaceVariant),
                ),
              ),
            )
          else
            ...credentials.map(
              (credential) => Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: _CredentialCard(
                  credential: credential,
                  isLoading: isLoading,
                  primaryColor: primaryColor,
                  onToggle: () => onToggleCredential(credential),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _OverrideCard extends StatelessWidget {
  const _OverrideCard({
    required this.accessOverride,
    required this.primaryColor,
    required this.isLoading,
    required this.onAllowAccess,
    required this.onBlockAccess,
    required this.onRemoveOverride,
  });

  final PhysicalAccessOverride? accessOverride;
  final Color primaryColor;
  final bool isLoading;
  final VoidCallback onAllowAccess;
  final VoidCallback onBlockAccess;
  final VoidCallback onRemoveOverride;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final current = accessOverride;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: theme.dividerColor),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Exceção manual',
            style: TextStyle(
              color: colorScheme.onSurface,
              fontSize: 14,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            current == null
                ? 'Nenhuma exceção manual ativa.'
                : current.type == 1
                ? 'Entrada liberada manualmente.'
                : 'Entrada bloqueada manualmente.',
            style: TextStyle(color: colorScheme.onSurfaceVariant, fontSize: 13),
          ),
          if (current?.reason != null) ...[
            const SizedBox(height: 6),
            Text(
              'Motivo: ${current!.reason}',
              style: TextStyle(
                color: colorScheme.onSurfaceVariant,
                fontSize: 12,
              ),
            ),
          ],
          const SizedBox(height: 16),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: [
              OutlinedButton.icon(
                onPressed: isLoading ? null : onAllowAccess,
                icon: const Icon(Icons.lock_open_outlined, size: 18),
                label: const Text('Liberar manualmente'),
              ),
              OutlinedButton.icon(
                onPressed: isLoading ? null : onBlockAccess,
                icon: const Icon(Icons.block_outlined, size: 18),
                label: const Text('Bloquear manualmente'),
              ),
              if (current != null)
                TextButton.icon(
                  style: AppButtonStyles.destructiveText(context),
                  onPressed: isLoading ? null : onRemoveOverride,
                  icon: const Icon(Icons.close_rounded, size: 18),
                  label: const Text('Remover exceção'),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

class _CredentialCard extends StatelessWidget {
  const _CredentialCard({
    required this.credential,
    required this.isLoading,
    required this.primaryColor,
    required this.onToggle,
  });

  final PhysicalAccessCredential credential;
  final bool isLoading;
  final Color primaryColor;
  final VoidCallback onToggle;

  static String _providerLabel(String providerKey) {
    return switch (providerKey.toLowerCase()) {
      'toletus' => 'Toletus',
      'toletus-litenet2' => 'Toletus LiteNet2',
      'controlid' => 'Control iD',
      'henry' => 'Henry',
      _ => providerKey,
    };
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: theme.dividerColor),
      ),
      child: Row(
        children: [
          Icon(
            _credentialIcon(credential.type),
            color: credential.isActive
                ? primaryColor
                : colorScheme.onSurfaceVariant,
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _credentialTypeLabel(credential.type),
                  style: TextStyle(
                    color: colorScheme.onSurface,
                    fontSize: 13,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Integração: ${_providerLabel(credential.providerKey)}',
                  style: TextStyle(
                    color: colorScheme.onSurfaceVariant,
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 2),
                if (credential.type != 5)
                  Text(
                    'Identificador técnico: ${credential.externalIdentifier}',
                    style: TextStyle(
                      color: colorScheme.onSurfaceVariant,
                      fontSize: 11,
                    ),
                  )
                else
                  Text(
                    'O PIN é exibido somente na emissão ou redefinição.',
                    style: TextStyle(
                      color: colorScheme.onSurfaceVariant,
                      fontSize: 11,
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 16),
          Text(
            credential.isActive ? 'Ativa' : 'Inativa',
            style: TextStyle(
              color: credential.isActive
                  ? AvelriSemanticColors.success
                  : AvelriSemanticColors.inactive,
              fontSize: 12,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(width: 12),
          Switch(
            value: credential.isActive,
            onChanged: isLoading ? null : (_) => onToggle(),
          ),
        ],
      ),
    );
  }

  static String _credentialTypeLabel(int type) {
    return switch (type) {
      1 => 'Cartão',
      2 => 'QR Code',
      3 => 'Biometria',
      4 => 'Reconhecimento facial',
      5 => 'PIN',
      _ => 'Outro',
    };
  }

  static IconData _credentialIcon(int type) {
    return switch (type) {
      1 => Icons.credit_card_outlined,
      2 => Icons.qr_code_rounded,
      3 => Icons.fingerprint_rounded,
      4 => Icons.face_outlined,
      5 => Icons.pin_outlined,
      _ => Icons.badge_outlined,
    };
  }
}
