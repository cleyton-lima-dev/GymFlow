import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/core/web/clear_activation_token.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:http/http.dart' as http;

class ActivateInvitePage extends StatefulWidget {
  const ActivateInvitePage({
    required this.authService,
    required this.token,
    super.key,
  });

  final AuthService authService;
  final String token;

  @override
  State<ActivateInvitePage> createState() => _ActivateInvitePageState();
}

class _ActivateInvitePageState extends State<ActivateInvitePage> {
  final _formKey = GlobalKey<FormState>();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _isValidating = true;
  bool _isSubmitting = false;
  bool _isValid = false;
  bool _activated = false;
  bool _obscurePassword = true;
  bool _obscureConfirmation = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    clearActivationTokenFromAddressBar();
    unawaited(_validateInvite());
  }

  @override
  void dispose() {
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _validateInvite() async {
    if (mounted) {
      setState(() {
        _isValidating = true;
        _errorMessage = null;
      });
    }
    if (widget.token.trim().isEmpty) {
      setState(() {
        _isValidating = false;
        _errorMessage = _unavailableMessage;
      });
      return;
    }

    try {
      await widget.authService.validateStudentAppInvite(widget.token);
      if (!mounted) return;
      setState(() {
        _isValidating = false;
        _isValid = true;
      });
    } on ApiException catch (exception) {
      if (!mounted) return;
      setState(() {
        _isValidating = false;
        _errorMessage = _messageFromApiException(exception);
      });
    } on TimeoutException {
      if (!mounted) return;
      setState(() {
        _isValidating = false;
        _errorMessage =
            'A validação demorou mais que o esperado. Tente novamente.';
      });
    } on http.ClientException {
      if (!mounted) return;
      setState(() {
        _isValidating = false;
        _errorMessage =
            'Não foi possível conectar ao servidor. Verifique sua conexão.';
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isValidating = false;
        _errorMessage = 'Não foi possível validar o convite.';
      });
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });
    try {
      await widget.authService.activateStudentApp(
        widget.token,
        _passwordController.text,
      );
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _isValid = false;
        _activated = true;
        _passwordController.clear();
        _confirmPasswordController.clear();
      });
    } on ApiException catch (exception) {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _errorMessage = _messageFromApiException(exception);
      });
    } on TimeoutException {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _errorMessage =
            'A ativação demorou mais que o esperado. Tente novamente.';
      });
    } on http.ClientException {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _errorMessage =
            'Não foi possível conectar ao servidor. Verifique sua conexão.';
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _errorMessage = 'Não foi possível ativar o acesso.';
      });
    }
  }

  String _messageFromApiException(ApiException exception) {
    try {
      final decoded = jsonDecode(exception.message);
      if (decoded is Map<String, dynamic> && decoded['message'] is String) {
        return decoded['message'] as String;
      }
    } on FormatException {
      // Fall back to a safe, user-facing message for non-JSON responses.
    }
    if (exception.statusCode == 400) return _unavailableMessage;
    if (exception.statusCode == 429) {
      return 'Muitas tentativas. Aguarde um pouco e tente novamente.';
    }
    if (exception.statusCode >= 500) {
      return 'O serviço está temporariamente indisponível. Tente novamente.';
    }
    return 'Não foi possível concluir a operação.';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 460),
              child: Card(
                child: Padding(
                  padding: const EdgeInsets.all(28),
                  child: _buildContent(context, colors),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildContent(BuildContext context, ColorScheme colors) {
    if (_isValidating) {
      return const Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          CircularProgressIndicator(),
          SizedBox(height: 18),
          Text('Validando convite…'),
        ],
      );
    }

    if (_activated) {
      return Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Icon(Icons.check_circle_outline, size: 48, color: colors.primary),
          const SizedBox(height: 16),
          Text(
            'Acesso ativado',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          const Text(
            'Sua senha foi criada. Entre no aplicativo com o e-mail do convite e a senha definida.',
          ),
          const SizedBox(height: 20),
          FilledButton(
            onPressed: () => context.go('/login'),
            child: const Text('Ir para o aplicativo'),
          ),
        ],
      );
    }

    if (!_isValid) {
      return Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Icon(Icons.link_off, size: 48, color: colors.error),
          const SizedBox(height: 16),
          Text(
            'Não foi possível validar o convite',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(_errorMessage ?? _unavailableMessage),
          const SizedBox(height: 12),
          TextButton(
            onPressed: _validateInvite,
            child: const Text('Tentar novamente'),
          ),
        ],
      );
    }

    return Form(
      key: _formKey,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Crie sua senha',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          const Text('A senha é pessoal e não fica visível para a academia.'),
          const SizedBox(height: 22),
          TextFormField(
            controller: _passwordController,
            obscureText: _obscurePassword,
            autocorrect: false,
            enableSuggestions: false,
            decoration: InputDecoration(
              labelText: 'Senha',
              helperText: 'Use de 8 a 128 caracteres.',
              suffixIcon: IconButton(
                tooltip: _obscurePassword ? 'Mostrar senha' : 'Ocultar senha',
                onPressed: () =>
                    setState(() => _obscurePassword = !_obscurePassword),
                icon: Icon(
                  _obscurePassword
                      ? Icons.visibility_outlined
                      : Icons.visibility_off_outlined,
                ),
              ),
            ),
            validator: (value) {
              final length = value?.length ?? 0;
              if (length < 8) {
                return 'A senha precisa ter pelo menos 8 caracteres.';
              }
              if (length > 128) {
                return 'A senha pode ter no máximo 128 caracteres.';
              }
              return null;
            },
          ),
          const SizedBox(height: 14),
          TextFormField(
            controller: _confirmPasswordController,
            obscureText: _obscureConfirmation,
            autocorrect: false,
            enableSuggestions: false,
            decoration: InputDecoration(
              labelText: 'Confirme a senha',
              suffixIcon: IconButton(
                tooltip: _obscureConfirmation
                    ? 'Mostrar senha'
                    : 'Ocultar senha',
                onPressed: () => setState(
                  () => _obscureConfirmation = !_obscureConfirmation,
                ),
                icon: Icon(
                  _obscureConfirmation
                      ? Icons.visibility_outlined
                      : Icons.visibility_off_outlined,
                ),
              ),
            ),
            validator: (value) => value != _passwordController.text
                ? 'As senhas não coincidem.'
                : null,
          ),
          if (_errorMessage != null) ...[
            const SizedBox(height: 14),
            Text(_errorMessage!, style: TextStyle(color: colors.error)),
          ],
          const SizedBox(height: 22),
          FilledButton(
            onPressed: _isSubmitting ? null : _submit,
            child: _isSubmitting
                ? const SizedBox.square(
                    dimension: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Ativar acesso'),
          ),
        ],
      ),
    );
  }
}

const _unavailableMessage =
    'Este convite é inválido, já foi utilizado ou revogado. Solicite um novo convite ao Admin.';
