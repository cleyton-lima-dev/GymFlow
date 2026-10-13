import 'package:flutter/material.dart';

const avelriPurple = Color(0xFF3F17AB);
const avelriNavy = Color(0xFF040C22);
const platformBackground = Color(0xFFF5F6FA);

class DialogScrollArea extends StatelessWidget {
  const DialogScrollArea({
    super.key,
    required this.controller,
    required this.child,
  });
  final ScrollController controller;
  final Widget child;
  @override
  Widget build(BuildContext context) => ScrollConfiguration(
    behavior: ScrollConfiguration.of(context).copyWith(scrollbars: false),
    child: Scrollbar(
      controller: controller,
      thumbVisibility: true,
      child: SingleChildScrollView(
        controller: controller,
        padding: const EdgeInsets.only(right: 10),
        child: child,
      ),
    ),
  );
}

ThemeData platformTheme() {
  final scheme = ColorScheme.fromSeed(seedColor: avelriPurple).copyWith(
    primary: avelriPurple,
    secondary: avelriNavy,
    surface: Colors.white,
  );
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: platformBackground,
    textTheme: const TextTheme(
      headlineMedium: TextStyle(
        fontSize: 28,
        fontWeight: FontWeight.w700,
        color: avelriNavy,
      ),
      headlineSmall: TextStyle(
        fontSize: 22,
        fontWeight: FontWeight.w700,
        color: avelriNavy,
      ),
      titleLarge: TextStyle(
        fontSize: 18,
        fontWeight: FontWeight.w700,
        color: avelriNavy,
      ),
      titleMedium: TextStyle(
        fontSize: 15,
        fontWeight: FontWeight.w600,
        color: avelriNavy,
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: const Color(0xFFFAFAFD),
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xFFDEE1EB)),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: avelriPurple, width: 2),
      ),
    ),
    cardTheme: CardThemeData(
      elevation: 0,
      margin: EdgeInsets.zero,
      color: Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE5E7EF)),
      ),
    ),
    dialogTheme: DialogThemeData(
      backgroundColor: Colors.white,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size(0, 46),
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size(0, 44),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
  );
}

String statusLabel(String? status) => switch (status) {
  'awaiting-admin' => 'Aguardando Admin',
  'configuration' => 'Em configuração',
  'physical-validation' => 'Homologação física pendente',
  'ready' => 'Pronta para conclusão',
  'completed' => 'Implantação concluída',
  _ => 'Em andamento',
};
String? requiredField(String? value) =>
    value == null || value.trim().isEmpty ? 'Campo obrigatório.' : null;
Widget errorText(String message) => Padding(
  padding: const EdgeInsets.symmetric(vertical: 12),
  child: Semantics(
    liveRegion: true,
    child: Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF1F0),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(message, style: const TextStyle(color: Color(0xFFB3261E))),
    ),
  ),
);

class PlatformBrand extends StatelessWidget {
  const PlatformBrand({super.key, this.light = false});
  final bool light;
  @override
  Widget build(BuildContext context) => Row(
    children: [
      Container(
        width: 38,
        height: 38,
        decoration: BoxDecoration(
          color: avelriPurple,
          borderRadius: BorderRadius.circular(11),
        ),
        child: const Icon(
          Icons.fitness_center_rounded,
          color: Colors.white,
          size: 21,
        ),
      ),
      const SizedBox(width: 12),
      Flexible(
        child: FittedBox(
          fit: BoxFit.scaleDown,
          alignment: Alignment.centerLeft,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Avelri',
                style: TextStyle(
                  fontWeight: FontWeight.w800,
                  fontSize: 23,
                  color: light ? Colors.white : avelriNavy,
                  height: 1.1,
                ),
              ),
              Text(
                'PLATFORM',
                style: TextStyle(
                  fontSize: 10,
                  letterSpacing: 2.3,
                  color: light
                      ? const Color(0xFFB6BDD3)
                      : const Color(0xFF68728C),
                ),
              ),
            ],
          ),
        ),
      ),
    ],
  );
}

class StatusBadge extends StatelessWidget {
  const StatusBadge(this.status, {super.key});
  final String? status;
  @override
  Widget build(BuildContext context) {
    final done = status == 'completed';
    final ready = status == 'ready';
    final color = done
        ? const Color(0xFF18704A)
        : ready
        ? avelriPurple
        : const Color(0xFF98620B);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: .08),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        statusLabel(status),
        style: TextStyle(
          color: color,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

String displayDate(String value) {
  final date = DateTime.tryParse(value)?.toLocal();
  if (date == null) return 'Data indisponível';
  String two(int v) => v.toString().padLeft(2, '0');
  return '${two(date.day)}/${two(date.month)}/${date.year} às ${two(date.hour)}:${two(date.minute)}';
}

class OnboardingStep extends StatelessWidget {
  const OnboardingStep({
    super.key,
    required this.number,
    required this.title,
    required this.done,
    required this.description,
  });
  final int number;
  final String title, description;
  final bool done;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 12),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        CircleAvatar(
          radius: 17,
          backgroundColor: done
              ? const Color(0xFFEAF6EF)
              : const Color(0xFFF0EBFC),
          foregroundColor: done ? const Color(0xFF18704A) : avelriPurple,
          child: done
              ? const Icon(Icons.check, size: 19)
              : Text(
                  '$number',
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                  ),
                ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 4),
              Text(
                description,
                style: const TextStyle(color: Color(0xFF68728C), fontSize: 13),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}
