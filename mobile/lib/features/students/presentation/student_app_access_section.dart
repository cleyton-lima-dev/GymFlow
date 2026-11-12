import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:gymflow/features/students/presentation/student_details_view_model.dart';
import 'package:gymflow/features/students/presentation/student_invite_share_links.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

const _studentAppBaseUrl = String.fromEnvironment(
  'STUDENT_APP_URL',
  defaultValue: 'https://avelri.pages.dev',
);

class StudentAppAccessSection extends StatelessWidget {
  const StudentAppAccessSection({required this.student, super.key});

  final StudentSummary student;

  @override
  Widget build(BuildContext context) {
    return Consumer<StudentDetailsViewModel>(
      builder: (context, viewModel, _) {
        final isPendingInvite =
            student.appAccessEnabled && student.userId == null;
        final canChangeAccess = !student.appAccessEnabled || isPendingInvite;
        final isEligible = student.isActive && student.archivedAt == null;
        final colorScheme = Theme.of(context).colorScheme;

        return Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Acesso ao aplicativo',
                  style: Theme.of(context).textTheme.titleLarge
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),
                const SizedBox(height: 12),
                Text(
                  !student.appAccessEnabled
                      ? 'Acesso desabilitado.'
                      : isPendingInvite
                      ? 'Convite pendente de ativação.'
                      : 'Acesso habilitado.',
                ),
                if (viewModel.appAccessErrorMessage != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    viewModel.appAccessErrorMessage!,
                    style: TextStyle(color: colorScheme.error),
                  ),
                ],
                if (canChangeAccess) ...[
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: viewModel.isUpdatingAppAccess || !isEligible
                        ? null
                        : () => _manageAppAccess(context, viewModel),
                    icon: viewModel.isUpdatingAppAccess
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.mail_outline),
                    label: Text(
                      viewModel.isUpdatingAppAccess
                          ? 'Processando…'
                          : isPendingInvite
                          ? 'Reemitir convite'
                          : 'Liberar acesso',
                    ),
                  ),
                ],
                if (!isEligible) ...[
                  const SizedBox(height: 8),
                  Text(
                    'Ative o cadastro do aluno antes de liberar acesso.',
                    style: Theme.of(context).textTheme.bodySmall
                        ?.copyWith(color: colorScheme.onSurfaceVariant),
                  ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _manageAppAccess(
    BuildContext context,
    StudentDetailsViewModel viewModel,
  ) async {
    final currentStudent = viewModel.student ?? student;
    final hasPendingInvite =
        currentStudent.appAccessEnabled && currentStudent.userId == null;

    if (hasPendingInvite) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Reemitir convite?'),
          content: const Text(
            'O link anterior deixará de funcionar e um novo convite será criado. '
            'Compartilhe o novo link manualmente com o aluno.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext, true),
              child: const Text('Reemitir convite'),
            ),
          ],
        ),
      );
      if (!context.mounted || confirmed != true) return;
    }

    String? email;
    if (currentStudent.userId == null &&
        !StudentInviteShareLinks.isValidEmail(currentStudent.email)) {
      email = await _showEmailDialog(
        context,
        initialEmail: currentStudent.email,
      );
      if (!context.mounted || email == null) return;
    }

    final result = await viewModel.setAppAccess(enabled: true, email: email);
    if (!context.mounted) return;

    if (!result.succeeded) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            viewModel.appAccessErrorMessage ??
                'Não foi possível liberar o acesso ao aplicativo.',
          ),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
      return;
    }

    if (result.token != null) {
      final inviteEmail =
          viewModel.student?.email?.trim() ?? email?.trim() ?? '';
      await _showInviteDialog(
        context,
        email: inviteEmail,
        token: result.token!,
        expiresAt: result.expiresAt,
      );
      if (!context.mounted) return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          result.token == null
              ? 'Acesso ao aplicativo habilitado.'
              : 'Convite preparado. O aluno definirá a própria senha.',
        ),
      ),
    );
  }

  Future<String?> _showEmailDialog(
    BuildContext context, {
    String? initialEmail,
  }) async {
    final formKey = GlobalKey<FormState>();
    final controller = TextEditingController(text: initialEmail?.trim());
    final email = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('E-mail do aluno'),
        content: Form(
          key: formKey,
          child: TextFormField(
            controller: controller,
            autofocus: true,
            keyboardType: TextInputType.emailAddress,
            autocorrect: false,
            decoration: const InputDecoration(
              labelText: 'E-mail',
              hintText: 'aluno@exemplo.com',
            ),
            validator: (value) => StudentInviteShareLinks.isValidEmail(value)
                ? null
                : 'Informe um e-mail válido.',
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () {
              if (formKey.currentState?.validate() != true) return;
              Navigator.pop(
                dialogContext,
                controller.text.trim().toLowerCase(),
              );
            },
            child: const Text('Continuar'),
          ),
        ],
      ),
    );
    controller.dispose();
    return email;
  }

  Future<void> _showInviteDialog(
    BuildContext context, {
    required String email,
    required String token,
    required DateTime? expiresAt,
  }) async {
    final inviteLink = StudentInviteShareLinks.inviteLink(
      baseUrl: _studentAppBaseUrl,
      token: token,
    );
    final inviteMessage = StudentInviteShareLinks.message(
      email: email,
      inviteLink: inviteLink,
    );
    final emailUri = StudentInviteShareLinks.email(
      email: email,
      inviteLink: inviteLink,
    );
    final whatsappUri = StudentInviteShareLinks.whatsapp(
      email: email,
      inviteLink: inviteLink,
    );

    await showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Convite para o aplicativo'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Copie o convite e compartilhe manualmente pelo canal escolhido. '
                'Nada será enviado automaticamente. O aluno criará a própria '
                'senha ao ativar a conta.',
              ),
              if (expiresAt != null) ...[
                const SizedBox(height: 8),
                Text('Válido até ${_formatDateTime(expiresAt.toLocal())}.'),
              ],
              const SizedBox(height: 12),
              SelectableText(inviteLink),
              const SizedBox(height: 8),
              SelectableText(inviteMessage),
            ],
          ),
        ),
        actions: [
          TextButton.icon(
            onPressed: () => _openInviteChannel(
              context,
              dialogContext,
              emailUri,
              'o aplicativo de e-mail',
            ),
            icon: const Icon(Icons.email_outlined),
            label: const Text('Abrir e-mail'),
          ),
          TextButton.icon(
            onPressed: () => _openInviteChannel(
              context,
              dialogContext,
              whatsappUri,
              'o WhatsApp',
            ),
            icon: const Icon(Icons.chat_outlined),
            label: const Text('Abrir WhatsApp'),
          ),
          TextButton(
            onPressed: () => _copyInvite(
              context,
              dialogContext,
              inviteMessage,
              'Mensagem do convite copiada.',
            ),
            child: const Text('Copiar mensagem'),
          ),
          TextButton(
            onPressed: () => _copyInvite(
              context,
              dialogContext,
              inviteLink,
              'Link do convite copiado.',
            ),
            child: const Text('Copiar link'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Fechar'),
          ),
        ],
      ),
    );
  }

  Future<void> _openInviteChannel(
    BuildContext context,
    BuildContext dialogContext,
    Uri uri,
    String channel,
  ) async {
    final opened = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!dialogContext.mounted) return;
    if (opened) {
      Navigator.pop(dialogContext);
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          'Não foi possível abrir $channel. Copie o convite para compartilhar manualmente.',
        ),
      ),
    );
  }

  Future<void> _copyInvite(
    BuildContext context,
    BuildContext dialogContext,
    String text,
    String confirmation,
  ) async {
    await Clipboard.setData(ClipboardData(text: text));
    if (dialogContext.mounted) Navigator.pop(dialogContext);
    if (context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(confirmation)));
    }
  }

  String _formatDateTime(DateTime value) {
    final day = value.day.toString().padLeft(2, '0');
    final month = value.month.toString().padLeft(2, '0');
    final hour = value.hour.toString().padLeft(2, '0');
    final minute = value.minute.toString().padLeft(2, '0');
    return '$day/$month/${value.year} às $hour:$minute';
  }
}
