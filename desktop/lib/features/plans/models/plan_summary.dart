enum PlanBillingCycle {
  monthly(1),
  quarterly(3),
  semiannual(6),
  annual(12);

  const PlanBillingCycle(this.value);

  final int value;

  static PlanBillingCycle fromValue(int value) {
    return PlanBillingCycle.values.firstWhere(
          (cycle) => cycle.value == value,
    );
  }
}

class PlanSummary {
  const PlanSummary({
    required this.id,
    required this.name,
    required this.price,
    required this.durationMonths,
    required this.billingCycle,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String name;
  final double price;
  final int durationMonths;
  final PlanBillingCycle billingCycle;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;

  factory PlanSummary.fromJson(Map<String, dynamic> json) {
    final updatedAtValue = json['updatedAt'] as String?;

    return PlanSummary(
      id: json['id'] as String,
      name: json['name'] as String,
      price: (json['price'] as num).toDouble(),
      durationMonths: json['durationMonths'] as int,
      billingCycle: PlanBillingCycle.fromValue(
        json['billingCycle'] as int,
      ),
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt:
      updatedAtValue == null ? null : DateTime.parse(updatedAtValue),
    );
  }
}
