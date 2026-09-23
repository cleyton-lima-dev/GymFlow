enum ChargeStatus {
  pending(1),
  paid(2),
  overdue(3),
  cancelled(4);

  const ChargeStatus(this.value);

  final int value;

  static ChargeStatus fromValue(int value) {
    return ChargeStatus.values.firstWhere(
          (status) => status.value == value,
    );
  }
}

class ChargeSummary {
  const ChargeSummary({
    required this.id,
    required this.enrollmentId,
    required this.studentId,
    required this.studentName,
    required this.planName,
    required this.amount,
    required this.dueDate,
    required this.status,
    required this.paidAt,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String enrollmentId;
  final String studentId;
  final String studentName;
  final String planName;
  final double amount;
  final DateTime dueDate;
  final ChargeStatus status;
  final DateTime? paidAt;
  final DateTime createdAt;
  final DateTime? updatedAt;

  factory ChargeSummary.fromJson(
      Map<String, dynamic> json,
      ) {
    final paidAtValue = json['paidAt'] as String?;
    final updatedAtValue = json['updatedAt'] as String?;

    return ChargeSummary(
      id: json['id'] as String,
      enrollmentId: json['enrollmentId'] as String,
      studentId: json['studentId'] as String,
      studentName: json['studentName'] as String,
      planName: json['planName'] as String,
      amount: (json['amount'] as num).toDouble(),
      dueDate: DateTime.parse(json['dueDate'] as String),
      status: ChargeStatus.fromValue(
        json['status'] as int,
      ),
      paidAt: paidAtValue == null
          ? null
          : DateTime.parse(paidAtValue),
      createdAt: DateTime.parse(
        json['createdAt'] as String,
      ),
      updatedAt: updatedAtValue == null
          ? null
          : DateTime.parse(updatedAtValue),
    );
  }
}
