import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:avelri_gestao/features/charges/models/payment_details.dart';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';

class StudentFinancialHistoryItem {
  const StudentFinancialHistoryItem({
    required this.enrollmentId,
    required this.planName,
    required this.planPrice,
    required this.startDate,
    required this.endDate,
    required this.enrollmentStatus,
    required this.cancellationDate,
    required this.chargeId,
    required this.chargeAmount,
    required this.discountAmount,
    required this.paidAmount,
    required this.paymentMethod,
    required this.dueDate,
    required this.chargeStatus,
    required this.paidAt,
    required this.createdAt,
  });

  final String enrollmentId;

  final String planName;
  final double planPrice;

  final DateTime startDate;
  final DateTime endDate;

  final EnrollmentStatus enrollmentStatus;
  final DateTime? cancellationDate;

  final String? chargeId;
  final double? chargeAmount;
  final double? discountAmount;
  final double? paidAmount;
  final PaymentMethod? paymentMethod;

  final DateTime? dueDate;
  final ChargeStatus? chargeStatus;
  final DateTime? paidAt;

  final DateTime createdAt;

  factory StudentFinancialHistoryItem.fromJson(
      Map<String, dynamic> json,
      ) {
    final cancellationDateValue =
    json['cancellationDate'] as String?;

    final dueDateValue =
    json['dueDate'] as String?;

    final paidAtValue =
    json['paidAt'] as String?;

    final chargeStatusValue =
    json['chargeStatus'] as int?;

    return StudentFinancialHistoryItem(
      enrollmentId:
      json['enrollmentId'] as String,
      planName:
      json['planName'] as String,
      planPrice:
      (json['planPrice'] as num).toDouble(),
      startDate:
      DateTime.parse(json['startDate'] as String),
      endDate:
      DateTime.parse(json['endDate'] as String),
      enrollmentStatus:
      EnrollmentStatus.fromValue(
        json['enrollmentStatus'] as int,
      ),
      cancellationDate:
      cancellationDateValue == null
          ? null
          : DateTime.parse(
        cancellationDateValue,
      ),
      chargeId:
      json['chargeId'] as String?,
      chargeAmount:
      json['chargeAmount'] == null
          ? null
          : (json['chargeAmount'] as num)
          .toDouble(),
      discountAmount:
      json['discountAmount'] == null
          ? null
          : (json['discountAmount'] as num)
          .toDouble(),
      paidAmount:
      json['paidAmount'] == null
          ? null
          : (json['paidAmount'] as num)
          .toDouble(),
      paymentMethod:
      PaymentMethod.fromValue(
        json['paymentMethod'] as int?,
      ),
      dueDate:
      dueDateValue == null
          ? null
          : DateTime.parse(dueDateValue),
      chargeStatus:
      chargeStatusValue == null
          ? null
          : ChargeStatus.fromValue(
        chargeStatusValue,
      ),
      paidAt:
      paidAtValue == null
          ? null
          : DateTime.parse(paidAtValue),
      createdAt:
      DateTime.parse(
        json['createdAt'] as String,
      ),
    );
  }
}
