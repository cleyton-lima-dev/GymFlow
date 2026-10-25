import 'package:flutter/material.dart';

/// A dashboard grid that keeps card sizes predictable while adapting its
/// column count to the available desktop width.
class DashboardCardGrid extends StatelessWidget {
  const DashboardCardGrid({
    required this.itemCount,
    required this.itemBuilder,
    required this.minItemWidth,
    required this.itemHeight,
    this.maxColumns = 4,
    this.spacing = 16,
    super.key,
  });

  final int itemCount;
  final IndexedWidgetBuilder itemBuilder;
  final double minItemWidth;
  final double itemHeight;
  final int maxColumns;
  final double spacing;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final columns =
          ((constraints.maxWidth + spacing) / (minItemWidth + spacing))
              .floor()
              .clamp(1, maxColumns)
              .toInt();

      return GridView.builder(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: columns,
          mainAxisExtent: itemHeight,
          crossAxisSpacing: spacing,
          mainAxisSpacing: spacing,
        ),
        itemCount: itemCount,
        itemBuilder: itemBuilder,
      );
    },
  );
}
