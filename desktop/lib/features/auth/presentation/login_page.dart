import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/features/auth/presentation/login_view_model.dart';

class LoginPage extends StatelessWidget {
  const LoginPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => LoginViewModel(
        context.read<SessionController>(),
      ),
      child: const _LoginView(),
    );
  }
}

class _LoginView extends StatefulWidget {
  const _LoginView();

  @override
  State<_LoginView> createState() => _LoginViewState();
}

class _LoginViewState extends State<_LoginView> {
  final _formKey = GlobalKey<FormState>();

  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  bool _obscurePassword = true;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();

    super.dispose();
  }

  Future<void> _submit() async {
    FocusManager.instance.primaryFocus?.unfocus();

    final isValid = _formKey.currentState?.validate() ?? false;

    if (!isValid) {
      return;
    }

    await context.read<LoginViewModel>().login(
      email: _emailController.text,
      password: _passwordController.text,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF4F5FA),
      body: Stack(
        children: [
          const Positioned(
            top: -170,
            right: -120,
            child: _AmbientGlow(
              size: 440,
              color: Color(0xFF6C3CE7),
            ),
          ),
          const Positioned(
            bottom: -210,
            left: -160,
            child: _AmbientGlow(
              size: 500,
              color: Color(0xFF9D7AF2),
            ),
          ),
          SafeArea(
            child: LayoutBuilder(
              builder: (context, constraints) {
                return SingleChildScrollView(
                  padding: const EdgeInsets.all(32),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      minHeight: constraints.maxHeight - 64,
                    ),
                    child: Center(
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(
                          maxWidth: 460,
                        ),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            const _BrandHeader(),
                            const SizedBox(height: 34),
                            _LoginCard(
                              formKey: _formKey,
                              emailController: _emailController,
                              passwordController: _passwordController,
                              obscurePassword: _obscurePassword,
                              onTogglePassword: () {
                                setState(() {
                                  _obscurePassword = !_obscurePassword;
                                });
                              },
                              onSubmit: _submit,
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _BrandHeader extends StatelessWidget {
  const _BrandHeader();

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Container(
          width: 76,
          height: 76,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: const Color(0xFF3F17AB),
            borderRadius: BorderRadius.circular(22),
            boxShadow: const [
              BoxShadow(
                color: Color(0x303F17AB),
                blurRadius: 28,
                offset: Offset(0, 12),
              ),
            ],
          ),
          child: const CustomPaint(
            painter: _AvelriMarkPainter(
              color: Colors.white,
            ),
          ),
        ),
        const SizedBox(height: 20),
        const Text(
          'AVELRI',
          style: TextStyle(
            color: Color(0xFF12152A),
            fontSize: 30,
            fontWeight: FontWeight.w900,
            letterSpacing: 2.2,
          ),
        ),
        const SizedBox(height: 5),
        const Text(
          'GESTÃO',
          style: TextStyle(
            color: Color(0xFF777D93),
            fontSize: 12,
            fontWeight: FontWeight.w700,
            letterSpacing: 4,
          ),
        ),
      ],
    );
  }
}

class _LoginCard extends StatelessWidget {
  const _LoginCard({
    required this.formKey,
    required this.emailController,
    required this.passwordController,
    required this.obscurePassword,
    required this.onTogglePassword,
    required this.onSubmit,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController emailController;
  final TextEditingController passwordController;
  final bool obscurePassword;
  final VoidCallback onTogglePassword;
  final Future<void> Function() onSubmit;

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<LoginViewModel>();

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(36, 34, 36, 36),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(
          color: const Color(0xFFE4E6EF),
        ),
        boxShadow: const [
          BoxShadow(
            color: Color(0x10071028),
            blurRadius: 35,
            offset: Offset(0, 18),
          ),
        ],
      ),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Bem-vindo de volta',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: Color(0xFF171A2C),
                fontSize: 24,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'Entre para acessar o painel da sua academia.',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: Color(0xFF777D93),
                fontSize: 14,
                height: 1.4,
              ),
            ),
            const SizedBox(height: 30),
            const _FieldLabel('E-mail'),
            const SizedBox(height: 8),
            TextFormField(
              controller: emailController,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              autofillHints: const [
                AutofillHints.username,
                AutofillHints.email,
              ],
              autocorrect: false,
              enableSuggestions: false,
              onChanged: (_) {
                context.read<LoginViewModel>().clearError();
              },
              validator: (value) {
                final email = value?.trim() ?? '';

                if (email.isEmpty) {
                  return 'Informe seu e-mail.';
                }

                final isValid = RegExp(
                  r'^[^@\s]+@[^@\s]+\.[^@\s]+$',
                ).hasMatch(email);

                if (!isValid) {
                  return 'Informe um e-mail válido.';
                }

                return null;
              },
              decoration: _fieldDecoration(
                hintText: 'seu@email.com',
                prefixIcon: Icons.mail_outline_rounded,
              ),
            ),
            const SizedBox(height: 20),
            const _FieldLabel('Senha'),
            const SizedBox(height: 8),
            TextFormField(
              controller: passwordController,
              obscureText: obscurePassword,
              textInputAction: TextInputAction.done,
              autofillHints: const [
                AutofillHints.password,
              ],
              autocorrect: false,
              enableSuggestions: false,
              onChanged: (_) {
                context.read<LoginViewModel>().clearError();
              },
              onFieldSubmitted: (_) {
                if (!viewModel.isLoading) {
                  onSubmit();
                }
              },
              validator: (value) {
                if (value == null || value.isEmpty) {
                  return 'Informe sua senha.';
                }

                return null;
              },
              decoration: _fieldDecoration(
                hintText: 'Digite sua senha',
                prefixIcon: Icons.lock_outline_rounded,
                suffix: IconButton(
                  tooltip: obscurePassword
                      ? 'Mostrar senha'
                      : 'Ocultar senha',
                  onPressed: onTogglePassword,
                  icon: Icon(
                    obscurePassword
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined,
                  ),
                ),
              ),
            ),
            if (viewModel.errorMessage != null) ...[
              const SizedBox(height: 18),
              Container(
                padding: const EdgeInsets.all(13),
                decoration: BoxDecoration(
                  color: const Color(0xFFFFF0F1),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: const Color(0xFFFFD8DB),
                  ),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.error_outline_rounded,
                      size: 20,
                      color: Color(0xFFBE3441),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        viewModel.errorMessage!,
                        style: const TextStyle(
                          color: Color(0xFF9A2A35),
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
            const SizedBox(height: 26),
            SizedBox(
              height: 52,
              child: FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFF3F17AB),
                  foregroundColor: Colors.white,
                  disabledBackgroundColor:
                  const Color(0xFF3F17AB).withAlpha(140),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(13),
                  ),
                ),
                onPressed: viewModel.isLoading ? null : onSubmit,
                child: viewModel.isLoading
                    ? const SizedBox(
                  width: 22,
                  height: 22,
                  child: CircularProgressIndicator(
                    strokeWidth: 2.3,
                    color: Colors.white,
                  ),
                )
                    : const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text(
                      'Entrar',
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(width: 8),
                    Icon(
                      Icons.arrow_forward_rounded,
                      size: 19,
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  InputDecoration _fieldDecoration({
    required String hintText,
    required IconData prefixIcon,
    Widget? suffix,
  }) {
    const borderColor = Color(0xFFDADDE8);

    final border = OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
      borderSide: const BorderSide(
        color: borderColor,
      ),
    );

    return InputDecoration(
      hintText: hintText,
      hintStyle: const TextStyle(
        color: Color(0xFFA0A5B7),
        fontSize: 14,
      ),
      prefixIcon: Icon(
        prefixIcon,
        size: 20,
        color: const Color(0xFF777D93),
      ),
      suffixIcon: suffix,
      filled: true,
      fillColor: const Color(0xFFF9FAFC),
      contentPadding: const EdgeInsets.symmetric(
        horizontal: 16,
        vertical: 17,
      ),
      border: border,
      enabledBorder: border,
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(
          color: Color(0xFF3F17AB),
          width: 1.5,
        ),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(
          color: Color(0xFFBE3441),
        ),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(
          color: Color(0xFFBE3441),
          width: 1.5,
        ),
      ),
    );
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: Color(0xFF35394E),
        fontSize: 13,
        fontWeight: FontWeight.w700,
      ),
    );
  }
}

class _AmbientGlow extends StatelessWidget {
  const _AmbientGlow({
    required this.size,
    required this.color,
  });

  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          boxShadow: [
            BoxShadow(
              color: color.withAlpha(25),
              blurRadius: 110,
              spreadRadius: 55,
            ),
          ],
        ),
      ),
    );
  }
}

class _AvelriMarkPainter extends CustomPainter {
  const _AvelriMarkPainter({
    required this.color,
  });

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(
      size.width / 2,
      size.height / 2,
    );

    final radius = size.shortestSide * 0.43;

    final hexagon = Path();

    for (var i = 0; i < 6; i++) {
      final angle = -math.pi / 2 + i * math.pi / 3;

      final point = Offset(
        center.dx + radius * math.cos(angle),
        center.dy + radius * math.sin(angle),
      );

      if (i == 0) {
        hexagon.moveTo(point.dx, point.dy);
      } else {
        hexagon.lineTo(point.dx, point.dy);
      }
    }

    hexagon.close();

    final outlinePaint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * 0.07
      ..strokeJoin = StrokeJoin.round
      ..strokeCap = StrokeCap.round;

    canvas.drawPath(
      hexagon,
      outlinePaint,
    );

    final barPaint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;

    final scale = size.width;

    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(
          center: center,
          width: scale * 0.46,
          height: scale * 0.075,
        ),
        Radius.circular(scale * 0.04),
      ),
      barPaint,
    );

    void drawPlate(
        double x,
        double width,
        double height,
        ) {
      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(
            center: Offset(
              x,
              center.dy,
            ),
            width: width,
            height: height,
          ),
          Radius.circular(width * 0.45),
        ),
        barPaint,
      );
    }

    drawPlate(
      center.dx - scale * 0.22,
      scale * 0.10,
      scale * 0.34,
    );

    drawPlate(
      center.dx + scale * 0.22,
      scale * 0.10,
      scale * 0.34,
    );

    drawPlate(
      center.dx - scale * 0.31,
      scale * 0.065,
      scale * 0.21,
    );

    drawPlate(
      center.dx + scale * 0.31,
      scale * 0.065,
      scale * 0.21,
    );
  }

  @override
  bool shouldRepaint(
      covariant _AvelriMarkPainter oldDelegate,
      ) {
    return oldDelegate.color != color;
  }
}