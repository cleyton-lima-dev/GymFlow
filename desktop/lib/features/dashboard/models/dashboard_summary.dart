class DashboardSummary {
  DashboardSummary.fromJson(Map<String, dynamic> json)
    : today = DateTime.parse(json['today'] as String),
      periodStart = json['periodStart'] == null
          ? null
          : DateTime.parse(json['periodStart'] as String),
      expectedRevenue = (json['expectedRevenue'] as num?)?.toDouble(),
      receivedRevenue = (json['receivedRevenue'] as num?)?.toDouble(),
      overdueAmount = (json['overdueAmount'] as num?)?.toDouble(),
      overdueCharges = json['overdueCharges'] as int?,
      activeEnrollments = json['activeEnrollments'] as int?,
      expiringEnrollments = json['expiringEnrollments'] as int?,
      activeStudents = json['activeStudents'] as int,
      studentsWithoutAssessment = json['studentsWithoutAssessment'] as int,
      reassessmentsDue = json['reassessmentsDue'] as int,
      expiringWithinDays = json['expiringWithinDays'] as int?;

  final DateTime today;
  final DateTime? periodStart;
  final double? expectedRevenue, receivedRevenue, overdueAmount;
  final int? overdueCharges,
      activeEnrollments,
      expiringEnrollments,
      expiringWithinDays;
  final int activeStudents, studentsWithoutAssessment, reassessmentsDue;

  bool get hasFinancial =>
      periodStart != null &&
      expectedRevenue != null &&
      receivedRevenue != null &&
      overdueAmount != null &&
      overdueCharges != null &&
      activeEnrollments != null &&
      expiringEnrollments != null &&
      expiringWithinDays != null;
}
