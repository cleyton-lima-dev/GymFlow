import '../../plans/models/plan_summary.dart';

enum EnrollmentStatus {
  active(1),
  cancelled(2),
  expired(3),
  pendingPayment(4);

  const EnrollmentStatus(this.value);

  final int value;

  static EnrollmentStatus fromValue(int value) {
    return EnrollmentStatus.values.firstWhere(
          (status) => status.value == value,
    );
  }
}

class EnrollmentSummary {
  const EnrollmentSummary({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.planId,
    required this.planName,
    required this.planPrice,
    required this.planDurationMonths,
    required this.planBillingCycle,
    required this.startDate,
    required this.endDate,
    required this.status,
    required this.cancellationDate,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;

  final String studentId;
  final String studentName;

  final String planId;
  final String planName;
  final double planPrice;
  final int planDurationMonths;
  final PlanBillingCycle planBillingCycle;

  final DateTime startDate;
  final DateTime endDate;

  final EnrollmentStatus status;
  final DateTime? cancellationDate;

  final DateTime createdAt;
  final DateTime? updatedAt;

  factory EnrollmentSummary.fromJson(
      Map<String, dynamic> json,
      ) {
    final cancellationDateValue =
    json['cancellationDate'] as String?;

    final updatedAtValue =
    json['updatedAt'] as String?;

    return EnrollmentSummary(
      id: json['id'] as String,

      studentId: json['studentId'] as String,
      studentName: json['studentName'] as String,

      planId: json['planId'] as String,
      planName: json['planName'] as String,
      planPrice:
      (json['planPrice'] as num).toDouble(),
      planDurationMonths:
      json['planDurationMonths'] as int,
      planBillingCycle:
      PlanBillingCycle.fromValue(
        json['planBillingCycle'] as int,
      ),

      startDate:
      DateTime.parse(json['startDate'] as String),
      endDate:
      DateTime.parse(json['endDate'] as String),

      status: EnrollmentStatus.fromValue(
        json['status'] as int,
      ),

      cancellationDate:
      cancellationDateValue == null
          ? null
          : DateTime.parse(
        cancellationDateValue,
      ),

      createdAt:
      DateTime.parse(json['createdAt'] as String),

      updatedAt:
      updatedAtValue == null
          ? null
          : DateTime.parse(updatedAtValue),
    );
  }
}
