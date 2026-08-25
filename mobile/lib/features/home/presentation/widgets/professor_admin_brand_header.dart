import 'package:flutter/material.dart';
import 'package:gymflow/app/theme/branding_controller.dart';
import 'package:gymflow/app/theme/gym_branding.dart';
import 'package:provider/provider.dart';

class ProfessorAdminBrandHeader extends StatelessWidget {
  const ProfessorAdminBrandHeader({
    this.height = 112,
    super.key,
  });

  final double height;

  @override
  Widget build(BuildContext context) {
    final branding = context.select<BrandingController, GymBranding>(
          (controller) => controller.branding,
    );

    final logoAsset = branding.logoAsset;

    if (logoAsset != null && logoAsset.isNotEmpty) {
      return Center(
        child: Image.asset(
          logoAsset,
          height: height,
          fit: BoxFit.contain,
          cacheWidth: 512,
          filterQuality: FilterQuality.medium,
        ),
      );
    }

    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(
          Icons.fitness_center_rounded,
          color: colorScheme.primary,
          size: 34,
        ),
        const SizedBox(width: 12),
        Flexible(
          child: Text(
            branding.displayName.toUpperCase(),
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
              fontWeight: FontWeight.w900,
              letterSpacing: -0.8,
            ),
          ),
        ),
      ],
    );
  }
}
