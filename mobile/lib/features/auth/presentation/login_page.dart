import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/features/auth/presentation/login_view_model.dart';
import 'package:provider/provider.dart';

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
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark.copyWith(
        statusBarColor: Colors.transparent,
        systemNavigationBarColor: Colors.white,
        systemNavigationBarIconBrightness: Brightness.dark,
      ),
      child: Scaffold(
        backgroundColor: Colors.white,
        body: Stack(
          children: [
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              height: 170,
              child: IgnorePointer(
                child: CustomPaint(
                  painter: _LoginWavesPainter(
                    color: colorScheme.primary,
                  ),
                ),
              ),
            ),
            SafeArea(
              child: LayoutBuilder(
                builder: (context, constraints) {
                  final compact = constraints.maxHeight < 700;

                  return SingleChildScrollView(
                    keyboardDismissBehavior:
                    ScrollViewKeyboardDismissBehavior.onDrag,
                    padding: const EdgeInsets.symmetric(horizontal: 28),
                    child: ConstrainedBox(
                      constraints: BoxConstraints(
                        minHeight: constraints.maxHeight,
                      ),
                      child: Center(
                        child: ConstrainedBox(
                          constraints: const BoxConstraints(
                            maxWidth: 440,
                          ),
                          child: Form(
                            key: _formKey,
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                SizedBox(height: compact ? 18 : 38),

                                Center(
                                  child: SizedBox(
                                    width: compact ? 80 : 94,
                                    height: compact ? 80 : 94,
                                    child: CustomPaint(
                                      painter: _GymFlowLogoPainter(
                                        color: colorScheme.primary,
                                      ),
                                    ),
                                  ),
                                ),

                                SizedBox(height: compact ? 14 : 20),

                                FittedBox(
                                  fit: BoxFit.scaleDown,
                                  child: Text.rich(
                                    TextSpan(
                                      children: [
                                        const TextSpan(
                                          text: 'GYM',
                                          style: TextStyle(
                                            color: Color(0xFF071227),
                                          ),
                                        ),
                                        TextSpan(
                                          text: 'FLOW',
                                          style: TextStyle(
                                            color: colorScheme.primary,
                                          ),
                                        ),
                                      ],
                                    ),
                                    textAlign: TextAlign.center,
                                    style:
                                    theme.textTheme.displaySmall?.copyWith(
                                      fontWeight: FontWeight.w900,
                                      fontSize: 44,
                                      height: 1,
                                      letterSpacing: -1.4,
                                    ),
                                  ),
                                ),

                                const SizedBox(height: 14),

                                Text(
                                  'Organize treinos. Acompanhe resultados.',
                                  textAlign: TextAlign.center,
                                  style: theme.textTheme.bodyLarge?.copyWith(
                                    color: const Color(0xFF626B82),
                                    fontWeight: FontWeight.w500,
                                    fontSize: 16,
                                  ),
                                ),

                                SizedBox(height: compact ? 32 : 50),

                                Text(
                                  'Bem-vindo de volta!',
                                  textAlign: TextAlign.center,
                                  style:
                                  theme.textTheme.headlineSmall?.copyWith(
                                    color: const Color(0xFF071227),
                                    fontWeight: FontWeight.w800,
                                    fontSize: 24,
                                  ),
                                ),

                                const SizedBox(height: 8),

                                Text(
                                  'Faça login para acessar sua conta.',
                                  textAlign: TextAlign.center,
                                  style: theme.textTheme.bodyLarge?.copyWith(
                                    color: const Color(0xFF626B82),
                                    fontSize: 16,
                                  ),
                                ),

                                SizedBox(height: compact ? 28 : 36),

                                _FieldLabel(
                                  text: 'E-mail',
                                  theme: theme,
                                ),

                                const SizedBox(height: 10),

                                TextFormField(
                                  controller: _emailController,
                                  keyboardType: TextInputType.emailAddress,
                                  textInputAction: TextInputAction.next,
                                  autofillHints: const [
                                    AutofillHints.username,
                                    AutofillHints.email,
                                  ],
                                  autocorrect: false,
                                  enableSuggestions: false,
                                  onChanged: (_) {
                                    context
                                        .read<LoginViewModel>()
                                        .clearError();
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
                                  decoration: _inputDecoration(
                                    context,
                                    hintText: 'Digite seu e-mail',
                                    prefixIcon: Icons.mail_outline_rounded,
                                  ),
                                ),

                                const SizedBox(height: 20),

                                _FieldLabel(
                                  text: 'Senha',
                                  theme: theme,
                                ),

                                const SizedBox(height: 10),

                                TextFormField(
                                  controller: _passwordController,
                                  obscureText: _obscurePassword,
                                  textInputAction: TextInputAction.done,
                                  autofillHints: const [
                                    AutofillHints.password,
                                  ],
                                  autocorrect: false,
                                  enableSuggestions: false,
                                  onChanged: (_) {
                                    context
                                        .read<LoginViewModel>()
                                        .clearError();
                                  },
                                  onFieldSubmitted: (_) {
                                    if (!context
                                        .read<LoginViewModel>()
                                        .isLoading) {
                                      _submit();
                                    }
                                  },
                                  validator: (value) {
                                    if (value == null || value.isEmpty) {
                                      return 'Informe sua senha.';
                                    }

                                    return null;
                                  },
                                  decoration: _inputDecoration(
                                    context,
                                    hintText: 'Digite sua senha',
                                    prefixIcon: Icons.lock_outline_rounded,
                                    suffix: IconButton(
                                      tooltip: _obscurePassword
                                          ? 'Mostrar senha'
                                          : 'Ocultar senha',
                                      onPressed: () {
                                        setState(() {
                                          _obscurePassword =
                                          !_obscurePassword;
                                        });
                                      },
                                      icon: Icon(
                                        _obscurePassword
                                            ? Icons.visibility_outlined
                                            : Icons.visibility_off_outlined,
                                        color: colorScheme.primary,
                                      ),
                                    ),
                                  ),
                                ),

                                Consumer<LoginViewModel>(
                                  builder: (context, viewModel, child) {
                                    if (viewModel.errorMessage == null) {
                                      return const SizedBox(height: 28);
                                    }

                                    return Padding(
                                      padding: const EdgeInsets.only(
                                        top: 16,
                                        bottom: 16,
                                      ),
                                      child: Container(
                                        padding: const EdgeInsets.all(12),
                                        decoration: BoxDecoration(
                                          color: colorScheme.errorContainer,
                                          borderRadius:
                                          BorderRadius.circular(12),
                                        ),
                                        child: Row(
                                          crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                          children: [
                                            Icon(
                                              Icons.error_outline_rounded,
                                              size: 20,
                                              color: colorScheme
                                                  .onErrorContainer,
                                            ),
                                            const SizedBox(width: 10),
                                            Expanded(
                                              child: Text(
                                                viewModel.errorMessage!,
                                                style: theme
                                                    .textTheme.bodyMedium
                                                    ?.copyWith(
                                                  color: colorScheme
                                                      .onErrorContainer,
                                                ),
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                    );
                                  },
                                ),

                                Consumer<LoginViewModel>(
                                  builder: (context, viewModel, child) {
                                    return _GradientLoginButton(
                                      isLoading: viewModel.isLoading,
                                      primaryColor: colorScheme.primary,
                                      onPressed:
                                      viewModel.isLoading ? null : _submit,
                                    );
                                  },
                                ),

                                SizedBox(height: compact ? 100 : 160),
                              ],
                            ),
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
      ),
    );
  }

  InputDecoration _inputDecoration(
      BuildContext context, {
        required String hintText,
        required IconData prefixIcon,
        Widget? suffix,
      }) {
    final colorScheme = Theme.of(context).colorScheme;

    final normalBorder = OutlineInputBorder(
      borderRadius: BorderRadius.circular(14),
      borderSide: BorderSide(
        color: colorScheme.primary.withAlpha(55),
      ),
    );

    return InputDecoration(
      hintText: hintText,
      hintStyle: const TextStyle(
        color: Color(0xFF7A8297),
        fontSize: 16,
      ),
      prefixIcon: Icon(
        prefixIcon,
        color: colorScheme.primary,
        size: 23,
      ),
      suffixIcon: suffix,
      filled: true,
      fillColor: Colors.white,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: 18,
        vertical: 18,
      ),
      border: normalBorder,
      enabledBorder: normalBorder,
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: BorderSide(
          color: colorScheme.primary,
          width: 1.6,
        ),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: BorderSide(
          color: colorScheme.error,
        ),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: BorderSide(
          color: colorScheme.error,
          width: 1.5,
        ),
      ),
    );
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel({
    required this.text,
    required this.theme,
  });

  final String text;
  final ThemeData theme;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: theme.textTheme.titleMedium?.copyWith(
        color: const Color(0xFF071227),
        fontWeight: FontWeight.w600,
        fontSize: 16,
      ),
    );
  }
}

class _GradientLoginButton extends StatelessWidget {
  const _GradientLoginButton({
    required this.isLoading,
    required this.primaryColor,
    required this.onPressed,
  });

  final bool isLoading;
  final Color primaryColor;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 58,
      child: Material(
        color: Colors.transparent,
        borderRadius: BorderRadius.circular(13),
        child: Ink(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              colors: [
                primaryColor,
                const Color(0xFF5014C9),
              ],
            ),
            borderRadius: BorderRadius.circular(13),
            boxShadow: [
              BoxShadow(
                color: primaryColor.withAlpha(38),
                blurRadius: 18,
                offset: const Offset(0, 8),
              ),
            ],
          ),
          child: InkWell(
            borderRadius: BorderRadius.circular(13),
            onTap: onPressed,
            child: Center(
              child: isLoading
                  ? const SizedBox(
                width: 23,
                height: 23,
                child: CircularProgressIndicator(
                  strokeWidth: 2.5,
                  color: Colors.white,
                ),
              )
                  : const Text(
                'Entrar',
                style: TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w800,
                  fontSize: 19,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _GymFlowLogoPainter extends CustomPainter {
  const _GymFlowLogoPainter({
    required this.color,
  });

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
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
      ..strokeWidth = size.width * 0.065
      ..strokeJoin = StrokeJoin.round
      ..strokeCap = StrokeCap.round;

    canvas.drawPath(hexagon, outlinePaint);

    final barPaint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;

    final cy = center.dy;
    final scale = size.width;

    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(
          center: Offset(center.dx, cy),
          width: scale * 0.46,
          height: scale * 0.075,
        ),
        Radius.circular(scale * 0.04),
      ),
      barPaint,
    );

    void drawPlate(double x, double width, double height) {
      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(
            center: Offset(x, cy),
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
  bool shouldRepaint(covariant _GymFlowLogoPainter oldDelegate) {
    return oldDelegate.color != color;
  }
}

class _LoginWavesPainter extends CustomPainter {
  const _LoginWavesPainter({
    required this.color,
  });

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    _drawWave(
      canvas,
      size,
      startY: size.height * 0.30,
      controlY1: size.height * 0.18,
      controlY2: size.height * 0.85,
      endY: size.height * 0.32,
      fillAlpha: 13,
      strokeAlpha: 32,
    );

    _drawWave(
      canvas,
      size,
      startY: size.height * 0.55,
      controlY1: size.height * 0.45,
      controlY2: size.height * 0.92,
      endY: size.height * 0.53,
      fillAlpha: 9,
      strokeAlpha: 22,
    );

    _drawWave(
      canvas,
      size,
      startY: size.height * 0.75,
      controlY1: size.height * 0.68,
      controlY2: size.height * 0.98,
      endY: size.height * 0.73,
      fillAlpha: 6,
      strokeAlpha: 14,
    );
  }

  void _drawWave(
      Canvas canvas,
      Size size, {
        required double startY,
        required double controlY1,
        required double controlY2,
        required double endY,
        required int fillAlpha,
        required int strokeAlpha,
      }) {
    final path = Path()
      ..moveTo(0, startY)
      ..cubicTo(
        size.width * 0.22,
        controlY1,
        size.width * 0.38,
        controlY2,
        size.width * 0.58,
        size.height * 0.62,
      )
      ..cubicTo(
        size.width * 0.76,
        size.height * 0.40,
        size.width * 0.88,
        endY,
        size.width,
        endY,
      );

    final fillPath = Path.from(path)
      ..lineTo(size.width, size.height)
      ..lineTo(0, size.height)
      ..close();

    canvas.drawPath(
      fillPath,
      Paint()
        ..color = color.withAlpha(fillAlpha)
        ..style = PaintingStyle.fill,
    );

    canvas.drawPath(
      path,
      Paint()
        ..color = color.withAlpha(strokeAlpha)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.2,
    );
  }

  @override
  bool shouldRepaint(covariant _LoginWavesPainter oldDelegate) {
    return oldDelegate.color != color;
  }
}