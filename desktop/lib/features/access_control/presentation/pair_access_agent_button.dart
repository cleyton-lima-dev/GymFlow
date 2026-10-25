import 'dart:async';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

class PairAccessAgentButton extends StatefulWidget {
  const PairAccessAgentButton({super.key});

  @override
  State<PairAccessAgentButton> createState() => _PairAccessAgentButtonState();
}

class _PairAccessAgentButtonState extends State<PairAccessAgentButton> {
  bool _isGenerating = false;

  Future<void> _generatePairingCode() async {
    if (_isGenerating) {
      return;
    }

    setState(() {
      _isGenerating = true;
    });

    try {
      final service = AccessAgentsService(context.read<ApiClient>());

      final pairingCode = await service.createPairingCode();

      if (!mounted) {
        return;
      }

      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (dialogContext) {
          final colorScheme = Theme.of(dialogContext).colorScheme;

          return AlertDialog(
            title: const Text('Parear Access Agent'),
            content: SizedBox(
              width: 430,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Use este código no computador que ficará conectado à catraca.',
                  ),
                  const SizedBox(height: 22),
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 20,
                      vertical: 18,
                    ),
                    decoration: BoxDecoration(
                      color: colorScheme.surfaceContainerHighest,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: SelectableText(
                      pairingCode.code,
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: colorScheme.onSurface,
                        fontSize: 30,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 4,
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text(
                    'Expira em ${DateFormat('dd/MM/yyyy HH:mm').format(pairingCode.expiresAt.toLocal())}.',
                    style: TextStyle(
                      color: colorScheme.onSurfaceVariant,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'O código é temporário, funciona uma única vez e pode ser substituído gerando outro.',
                    style: TextStyle(
                      color: colorScheme.onSurfaceVariant,
                      fontSize: 13,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'O Admin não precisa guardar Agent ID, Gym ID ou Secret.',
                    style: TextStyle(
                      color: colorScheme.onSurfaceVariant,
                      fontSize: 13,
                    ),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton.icon(
                onPressed: () async {
                  await Clipboard.setData(
                    ClipboardData(text: pairingCode.code),
                  );

                  if (!mounted) {
                    return;
                  }

                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('Código de pareamento copiado.'),
                    ),
                  );
                },
                icon: const Icon(Icons.copy_rounded),
                label: const Text('Copiar código'),
              ),
              FilledButton(
                onPressed: () {
                  Navigator.of(dialogContext).pop();
                },
                child: const Text('Concluir'),
              ),
            ],
          );
        },
      );
    } on ApiException catch (exception) {
      if (!mounted) {
        return;
      }

      final message = switch (exception.statusCode) {
        403 => 'Você não possui permissão para gerar o código.',
        429 => 'Muitas solicitações. Aguarde um instante e tente novamente.',
        >= 500 => 'O Avelri está temporariamente indisponível.',
        _ => 'Não foi possível gerar o código de pareamento.',
      };

      _showError(message);
    } on TimeoutException {
      if (!mounted) {
        return;
      }

      _showError('A solicitação demorou mais que o esperado.');
    } on http.ClientException {
      if (!mounted) {
        return;
      }

      _showError('Não foi possível conectar ao servidor.');
    } on FormatException {
      if (!mounted) {
        return;
      }

      _showError('O servidor retornou um código de pareamento inválido.');
    } catch (_) {
      if (!mounted) {
        return;
      }

      _showError('Não foi possível gerar o código de pareamento.');
    } finally {
      if (mounted) {
        setState(() {
          _isGenerating = false;
        });
      }
    }
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    return FilledButton.icon(
      onPressed: _isGenerating ? null : _generatePairingCode,
      icon: _isGenerating
          ? const SizedBox(
              width: 16,
              height: 16,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          : const Icon(Icons.link_rounded),
      label: Text(_isGenerating ? 'Gerando...' : 'Parear Agent'),
    );
  }
}
